using syndishanx_iw_tool.Players;

namespace Backend.Tests;

// Sparse fixture bytes exercise the same checked fields as the retail reader
internal static class PlayerCohortReaderTests {
    private const int ProcessId = 123;
    private static PlayerCohortObservation Read(PlayerStateMemoryFixture fixture) =>
        PlayerStateReader.ReadCohort(PlayerStateMemoryFixture.ModuleBase, ProcessId, fixture.ReadBytes);

    public static void CompleteReadsFollowActualSlotPointersAndReturnFourPresentActors() {
        var fixture = new PlayerStateMemoryFixture();
        fixture.AddLegacyLookalike(2);
        var observed = Read(fixture);
        TestAssert.Equal(new PlayerCohortSession(ProcessId, PlayerStateMemoryFixture.ModuleBase,
            PlayerStateMemoryFixture.Anchor, 4), observed.Session);
        TestAssert.Equal(4, observed.Slots.Count);
        for (int slot = 0; slot < 4; slot++) {
            var item = observed.Slots[slot];
            TestAssert.Equal(slot, item.Slot);
            TestAssert.Equal(PlayerSlotPresence.Present, item.Presence);
            TestAssert.True(item.Player is not null, "Present must carry the full linked actor");
            TestAssert.Equal(PlayerStateMemoryFixture.Server(slot), item.Player!.Identity.ServerClient);
            TestAssert.Equal(PlayerStateMemoryFixture.Entity(slot), item.Player.Identity.Entity);
            TestAssert.Equal(PlayerStateMemoryFixture.Guid(slot), item.Player.Identity.Guid);
            TestAssert.Equal(100, item.Player.CommandTime);
            TestAssert.Equal(0x200U, item.Player.Flags);
            TestAssert.True(item.Player.IsEnableEligible, "Normal fixture play should remain enable eligible");
        }
        TestAssert.True(!fixture.Reads.Any(span => span.Address == PlayerStateMemoryFixture.LegacyServer(2) + 8),
            "The cohort reader followed an invented contiguous record");
    }

    public static void DeadDownedAndNonplayingActorsRemainPresentWithoutEnableEligibility() {
        var fixture = new PlayerStateMemoryFixture();
        fixture.Int32(PlayerStateMemoryFixture.Entity(0) + 0x1E0, 0);
        fixture.Int32(PlayerStateMemoryFixture.Entity(1) + 0x1E0, 1);
        fixture.Int32(PlayerStateMemoryFixture.Client(2) + 0x4A94, 2);
        fixture.Int32(PlayerStateMemoryFixture.Client(3) + 4, 3);
        var observed = Read(fixture);
        TestAssert.True(observed.Session is not null, "Nonliving actors cannot invalidate an otherwise proved world");
        TestAssert.True(observed.Slots.All(slot => slot.Presence == PlayerSlotPresence.Present
            && slot.Player?.IsEnableEligible == false), "Life and movement must not be confused with membership absence");
        // Every recognized or unsupported session value remains an occupied linked actor
        foreach (int state in new[] { 1, 3, -1, int.MaxValue }) {
            fixture = new();
            fixture.Int32(PlayerStateMemoryFixture.Client(1) + 0x4A94, state);
            var slot = Read(fixture).Slots[1];
            TestAssert.Equal(PlayerSlotPresence.Present, slot.Presence);
            TestAssert.True(slot.Player?.IsEnableEligible == false, "Unsupported life cannot grant new ON");
        }
    }

    public static void UnprovedAbsenceStatesAndInvalidLinksRemainUnknown() {
        foreach (int state in new[] { 0, 1, 2, 3, 4, -1, 6, int.MaxValue }) {
            var fixture = new PlayerStateMemoryFixture();
            fixture.Int32(PlayerStateMemoryFixture.Server(2) + 8, state);
            RequireUnknownSlot(Read(fixture), 2);
        }
        foreach (long link in new[] { 0L, 0x10001L, long.MaxValue }) {
            var fixture = new PlayerStateMemoryFixture();
            fixture.Int64(PlayerStateMemoryFixture.Table + 16, link);
            RequireUnknownSlot(Read(fixture), 2);
            TestAssert.True(fixture.Reads.All(span => span.Address is >= 0x10000 and <= 0x7FFFFFFF0000),
                "An unsupported selected pointer must be rejected before following it");
        }
        var disconnected = new PlayerStateMemoryFixture();
        disconnected.Int32(PlayerStateMemoryFixture.Client(2) + 0x4A90, 0);
        RequireUnknownSlot(Read(disconnected), 2);
    }

    public static void PerSlotFailuresPreserveOtherKnownActorsWithoutInventingAbsence() {
        foreach (bool throws in new[] { false, true }) {
            var fixture = new PlayerStateMemoryFixture();
            int failures = 0;
            byte[]? ReadBytes(long address, int width) {
                // A transient first-pass failure still cannot prove a complete cohort
                if (address == PlayerStateMemoryFixture.Entity(1) + 0x170 && failures++ == 0) {
                    if (throws) throw new IOException("Slot read unavailable");
                    return new byte[3];
                }
                return fixture.ReadBytes(address, width);
            }
            RequireUnknownSlot(PlayerStateReader.ReadCohort(PlayerStateMemoryFixture.ModuleBase, ProcessId, ReadBytes), 1);
        }
        var malformed = new PlayerStateMemoryFixture();
        malformed.Text(PlayerStateMemoryFixture.Client(1) + 0x4BB0, "", 32);
        RequireUnknownSlot(Read(malformed), 1);
    }

    public static void GlobalFailuresAndLateMapTransitionsWithdrawTheWholeCohort() {
        TestAssert.True(Read(new()).Slots.All(slot => slot.Presence == PlayerSlotPresence.Present),
            "The valid control must publish actors before world failures withdraw them");
        foreach (Action<PlayerStateMemoryFixture> change in new Action<PlayerStateMemoryFixture>[] {
            fixture => fixture.Int32(PlayerStateMemoryFixture.CountAddress, 3),
            fixture => fixture.Int64(PlayerStateMemoryFixture.Table, 0),
            fixture => fixture.Int32(PlayerStateMemoryFixture.ModuleBase + 0x2246C30 + 8, 0),
            fixture => fixture.Int32(PlayerStateMemoryFixture.ModuleBase + 0x6B2C9D4, 1)
        }) {
            var fixture = new PlayerStateMemoryFixture();
            change(fixture);
            RequireUnknownWorld(Read(fixture));
        }
        foreach (long transition in new[] { 0x6B2C9D4L, 0x6B2C9D8L, 0x6B2C9DCL }) {
            var fixture = new PlayerStateMemoryFixture();
            fixture.BeforeRead = (address, _, count) => {
                if (address == PlayerStateMemoryFixture.Entity(3) + 0x1C8 && count == 2) {
                    fixture.Int32(PlayerStateMemoryFixture.ModuleBase + transition, 1);
                }
            };
            // The transition occurs after final identities were checked, during final scalars
            RequireUnknownWorld(Read(fixture));
        }
    }

    public static void IdentityReplacementAndCommandTimeRollbackWithdrawOnlyTheAffectedSlot() {
        var replacement = new PlayerStateMemoryFixture();
        replacement.BeforeRead = (address, _, count) => {
            if (address == PlayerStateMemoryFixture.Table + 8 && count == 2) {
                replacement.ServerRecord(PlayerStateMemoryFixture.LegacyServer(1), 1);
                replacement.Int64(address, PlayerStateMemoryFixture.LegacyServer(1));
            }
        };
        RequireUnknownSlot(Read(replacement), 1);
        var rollback = new PlayerStateMemoryFixture();
        rollback.BeforeRead = (address, _, count) => {
            if (address == PlayerStateMemoryFixture.Client(1) && count == 2) rollback.Int32(address, 99);
        };
        RequireUnknownSlot(Read(rollback), 1);
        var advancing = new PlayerStateMemoryFixture();
        advancing.BeforeRead = (address, _, count) => {
            if (address == PlayerStateMemoryFixture.Client(1) && count == 2) advancing.Int32(address, 101);
        };
        var fresh = Read(advancing).Slots[1];
        TestAssert.Equal(PlayerSlotPresence.Present, fresh.Presence);
        TestAssert.Equal(101, fresh.Player!.CommandTime);
    }

    public static void InvalidArgumentsAndReusedBuffersCannotInventOrMutateACohort() {
        int reads = 0;
        byte[]? Unexpected(long _, int __) { reads++; throw new InvalidOperationException("Unexpected callback"); }
        RequireUnknownWorld(PlayerStateReader.ReadCohort(0, ProcessId, Unexpected));
        RequireUnknownWorld(PlayerStateReader.ReadCohort(PlayerStateMemoryFixture.ModuleBase, 0, Unexpected));
        RequireUnknownWorld(PlayerStateReader.ReadCohort(PlayerStateMemoryFixture.ModuleBase, ProcessId, null!));
        TestAssert.Equal(0, reads);
        var fixture = new PlayerStateMemoryFixture();
        var reused = new Dictionary<int, byte[]>();
        byte[]? Shared(long address, int width) {
            var copied = fixture.ReadBytes(address, width);
            if (copied is null) return null;
            if (!reused.TryGetValue(width, out var buffer)) reused[width] = buffer = new byte[width];
            copied.CopyTo(buffer, 0);
            return buffer;
        }
        var observed = PlayerStateReader.ReadCohort(PlayerStateMemoryFixture.ModuleBase, ProcessId, Shared);
        foreach (var buffer in reused.Values) Array.Fill(buffer, (byte)0xFF);
        for (int slot = 0; slot < 4; slot++) {
            TestAssert.Equal(PlayerSlotPresence.Present, observed.Slots[slot].Presence);
            TestAssert.Equal(PlayerStateMemoryFixture.Name(slot), observed.Slots[slot].Player!.Identity.Name);
        }
    }

    private static void RequireUnknownSlot(PlayerCohortObservation observed, int missing) {
        TestAssert.True(observed.Session is not null, "A per-slot failure must preserve the checked session context");
        TestAssert.Equal(4, observed.Slots.Count);
        for (int slot = 0; slot < 4; slot++) {
            TestAssert.Equal(slot, observed.Slots[slot].Slot);
            TestAssert.Equal(slot == missing ? PlayerSlotPresence.Unknown : PlayerSlotPresence.Present,
                observed.Slots[slot].Presence);
        }
        TestAssert.True(observed.Slots[missing].Player is null
            && !string.IsNullOrWhiteSpace(observed.Slots[missing].FailureReason),
            "An unknown slot must carry a reason without inventing a target");
    }

    private static void RequireUnknownWorld(PlayerCohortObservation observed) {
        TestAssert.True(observed.Session is null, "Unproved world context cannot identify a lobby session");
        TestAssert.Equal(4, observed.Slots.Count);
        TestAssert.True(observed.Slots.All(slot => slot.Presence == PlayerSlotPresence.Unknown && slot.Player is null),
            "A failed global proof must withdraw every actor");
    }
}
