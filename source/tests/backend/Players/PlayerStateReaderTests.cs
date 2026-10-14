using System.IO;
using syndishanx_iw_tool.Players;

namespace Backend.Tests;

// Every callback reads fixture-owned bytes; no process or memory writer is used
internal static class PlayerStateReaderTests {
    public static void NoncontiguousServerRecordsFollowTheSelectedSlotPointer() {
        for (int slot = 0; slot <= 3; slot++) {
            var fixture = new PlayerStateMemoryFixture();
            var state = fixture.Read(slot);

            TestAssert.Equal(PlayerStateMemoryFixture.Server(slot), state.Identity.ServerClient);
            TestAssert.Equal(PlayerStateMemoryFixture.Anchor, state.Identity.ServerClients);
            TestAssert.Equal(PlayerStateMemoryFixture.Entity(slot), state.Identity.Entity);
            TestAssert.Equal(PlayerStateMemoryFixture.Name(slot), state.Identity.Name);
            TestAssert.Equal(PlayerStateMemoryFixture.Guid(slot), state.Identity.Guid);
            TestAssert.Equal(100, state.Health);
            TestAssert.Equal(100, state.CommandTime);
            TestAssert.Equal(0x200U, state.Flags);
            TestAssert.True(!fixture.Reads.Any(read => IsLegacyRecordRead(read.Address)),
                "Valid noncontiguous records cannot borrow the old stride lookup");
        }
    }

    public static void FabricatedContiguousLookalikeIsNeverReadOrAccepted() {
        var fixture = new PlayerStateMemoryFixture();
        fixture.AddLegacyLookalike();
        fixture.Int64(PlayerStateMemoryFixture.Table + 8, 0);

        TestAssert.Throws<InvalidOperationException>(() => fixture.Read());
        TestAssert.True(!fixture.Reads.Any(read => IsLegacyRecordRead(read.Address)),
            "An absent selected pointer must not fall back to a fabricated contiguous record");
    }

    public static void MissingUnalignedAndOutOfRangePointersRejectBeforeDereference() {
        foreach (long pointer in new[] { 0L, 0x10008L + 1, 0xFFF8L, 0x7FFFFFFF0008L, 0x7FFFFFFF0000L }) {
            var fixture = new PlayerStateMemoryFixture();
            fixture.AddLegacyLookalike();
            fixture.Int64(PlayerStateMemoryFixture.Table + 8, pointer);

            TestAssert.Throws<InvalidOperationException>(() => fixture.Read());
            TestAssert.True(!fixture.Reads.Any(read => IsLegacyRecordRead(read.Address)),
                "Invalid selected pointers cannot trigger a contiguous fallback");
            TestAssert.True(!fixture.Reads.Any(read => read.Address == pointer + 8),
                "Selected record bounds are checked before the callback receives a field address");
        }
    }

    public static void SelectedPointerReplacementRejectsEvenWithIdenticalRecordFields() {
        var fixture = new PlayerStateMemoryFixture();
        fixture.AddLegacyLookalike();
        // Equal labels and GUIDs cannot hide a changed record allocation
        long replacement = PlayerStateMemoryFixture.Server(1) + 0x100000;
        fixture.ServerRecord(replacement, 1);
        fixture.BeforeRead = (address, _, count) => {
            if (address == PlayerStateMemoryFixture.Table + 8 && count == 2) fixture.Int64(address, replacement);
        };

        TestAssert.Throws<InvalidOperationException>(() => fixture.Read());
        var original = new PlayerIdentity(1, 0x10000, 0x20000, 0x30000, 4, "Name", "0123456789ABCDEF") {
            ServerClient = PlayerStateMemoryFixture.Server(1)
        };
        TestAssert.True(original != original with { ServerClient = replacement },
            "Record identity must include the selected server record pointer");
    }

    public static void SharedAnchorCountAndMapTransitionsRemainRejected() {
        var mutations = new (long Address, Action<PlayerStateMemoryFixture> Change)[] {
            (PlayerStateMemoryFixture.Table, fixture => fixture.Int64(PlayerStateMemoryFixture.Table, PlayerStateMemoryFixture.Anchor + 8)),
            (PlayerStateMemoryFixture.CountAddress, fixture => fixture.Int32(PlayerStateMemoryFixture.CountAddress, 3)),
            (PlayerStateMemoryFixture.ModuleBase + 0x6B2C9D4, fixture => fixture.Int32(PlayerStateMemoryFixture.ModuleBase + 0x6B2C9D4, 1)),
            (PlayerStateMemoryFixture.ModuleBase + 0x6B2C9D8, fixture => fixture.Int32(PlayerStateMemoryFixture.ModuleBase + 0x6B2C9D8, 1)),
            (PlayerStateMemoryFixture.ModuleBase + 0x6B2C9DC, fixture => fixture.Int32(PlayerStateMemoryFixture.ModuleBase + 0x6B2C9DC, 1))
        };
        foreach (var mutation in mutations) {
            var fixture = new PlayerStateMemoryFixture();
            fixture.AddLegacyLookalike();
            fixture.BeforeRead = (address, _, count) => {
                if (address == mutation.Address && count == 2) mutation.Change(fixture);
            };
            TestAssert.Throws<InvalidOperationException>(() => fixture.Read());
        }
    }

    public static void OriginalEntityClientBacklinkNameAndGuidGuardsRemainFailClosed() {
        var cases = new (Action<PlayerStateMemoryFixture> Change, string Message)[] {
            (fixture => fixture.Int16(PlayerStateMemoryFixture.Entity(1), 2), "selected slot"),
            (fixture => fixture.Int32(PlayerStateMemoryFixture.Entity(1) + 0x9C, 2), "selected slot"),
            (fixture => fixture.Int64(PlayerStateMemoryFixture.Entity(1) + 0x170, 0), "entity client"),
            (fixture => fixture.Int32(PlayerStateMemoryFixture.Client(1) + 0x4A90, 1), "normal active play"),
            (fixture => fixture.Int32(PlayerStateMemoryFixture.Client(1) + 0x4A94, 1), "normal active play"),
            (fixture => fixture.Int32(PlayerStateMemoryFixture.Client(1) + 0x4BDC, 2), "normal active play"),
            (fixture => fixture.Int32(PlayerStateMemoryFixture.Client(1) + 4, 1), "normal active play"),
            (fixture => fixture.Text(PlayerStateMemoryFixture.Client(1) + 0x4C50, "Other", 32), "client names"),
            (fixture => BothServers(fixture, server => fixture.Int32(server + 8, 4)), "host server record"),
            (fixture => BothServers(fixture, server => fixture.Int64(server + 136, PlayerStateMemoryFixture.Entity(2))), "host server record"),
            (fixture => BothServers(fixture, server => fixture.Text(server + 1188, "Other", 32)), "host server record"),
            (fixture => BothServers(fixture, server => fixture.Text(server + 0x9F340, "Z123456789ABCDEF", 21)), "non-hexadecimal"),
            (fixture => BothServers(fixture, server => fixture.Text(server + 0x9F340, "1234", 21)), "sixteen bounded"),
            (fixture => fixture.Bytes(PlayerStateMemoryFixture.Client(1) + 0x4BB0, Enumerable.Repeat((byte)'A', 32).ToArray()), "bounded terminator"),
            (fixture => fixture.Bytes(PlayerStateMemoryFixture.Client(1) + 0x4BB0, new byte[] { 0xFF, 0 }), "UTF-8"),
            (fixture => fixture.Text(PlayerStateMemoryFixture.Client(1) + 0x4BB0, " ", 32), "whitespace"),
            (fixture => fixture.Text(PlayerStateMemoryFixture.Client(1) + 0x4BB0, "A\n", 32), "control characters")
        };
        foreach (var item in cases) {
            var fixture = new PlayerStateMemoryFixture();
            fixture.AddLegacyLookalike();
            item.Change(fixture);
            RejectWithMessage(() => fixture.Read(), item.Message);
        }
    }

    public static void ExistingLivingScalarAndLateDownedGuardsRemainRequired() {
        foreach (int health in new[] { 2, 10000 }) {
            var supported = new PlayerStateMemoryFixture();
            supported.AddLegacyLookalike();
            supported.Int32(PlayerStateMemoryFixture.Entity(1) + 0x1E0, health);
            supported.Int32(PlayerStateMemoryFixture.Client(1), 0);
            TestAssert.Equal(health, supported.Read().Health);
        }
        foreach (int health in new[] { -1, 0, 1, 10001 }) {
            var fixture = new PlayerStateMemoryFixture();
            fixture.AddLegacyLookalike();
            fixture.Int32(PlayerStateMemoryFixture.Entity(1) + 0x1E0, health);
            RejectWithMessage(() => fixture.Read(), "supported living state");
        }
        var command = new PlayerStateMemoryFixture();
        command.AddLegacyLookalike();
        command.Int32(PlayerStateMemoryFixture.Client(1), -1);
        RejectWithMessage(() => command.Read(), "supported living state");

        var late = new PlayerStateMemoryFixture();
        late.AddLegacyLookalike();
        late.BeforeRead = (address, _, count) => {
            if (address == PlayerStateMemoryFixture.Entity(1) + 0x1E0 && count == 2) late.Int32(address, 1);
        };
        RejectWithMessage(() => late.Read(), "supported living state");
    }

    public static void UnsupportedSlotsAndIncompleteReadsNeverProducePlayerState() {
        foreach (int slot in new[] { -1, 4, int.MaxValue }) {
            var fixture = new PlayerStateMemoryFixture();
            TestAssert.Throws<InvalidOperationException>(() => fixture.Read(slot));
            TestAssert.Equal(0, fixture.Reads.Count);
        }
        TestAssert.Throws<InvalidOperationException>(() => PlayerStateReader.Read(PlayerStateMemoryFixture.ModuleBase, 1, null!));
        TestAssert.Throws<InvalidOperationException>(() => PlayerStateReader.Read(0, 1, (_, _) => throw new Exception("No callback expected")));
        TestAssert.Throws<IOException>(() => PlayerStateReader.Read(PlayerStateMemoryFixture.ModuleBase, 1, (_, width) => new byte[width - 1]));
        TestAssert.Throws<IOException>(() => PlayerStateReader.Read(PlayerStateMemoryFixture.ModuleBase, 1, (_, _) => throw new IOException("Owned read failure")));
        var selected = new PlayerStateMemoryFixture();
        selected.AddLegacyLookalike();
        // A truncated selected pointer is a failed read, not permission to use another record
        TestAssert.Throws<IOException>(() => PlayerStateReader.Read(PlayerStateMemoryFixture.ModuleBase, 1,
            (address, width) => address == PlayerStateMemoryFixture.Table + 8
                ? new byte[sizeof(long) - 1] : selected.ReadBytes(address, width)));
        TestAssert.True(!selected.Reads.Any(read => IsLegacyRecordRead(read.Address)),
            "Incomplete selected pointers cannot fall back to contiguous storage");
    }

    private static bool IsLegacyRecordRead(long address) => address >= PlayerStateMemoryFixture.LegacyServer(1)
        && address < PlayerStateMemoryFixture.LegacyServer(1) + PlayerStateMemoryFixture.LegacyStride;

    // Equal old and new records keep retained guard tests focused on the guard under test
    private static void BothServers(PlayerStateMemoryFixture fixture, Action<long> change) {
        change(PlayerStateMemoryFixture.Server(1));
        change(PlayerStateMemoryFixture.LegacyServer(1));
    }

    private static void RejectWithMessage(Action read, string text) {
        try { read(); }
        catch (InvalidOperationException exception) {
            TestAssert.True(exception.Message.Contains(text, StringComparison.Ordinal), "The expected state guard must reject: " + text);
            return;
        }
        throw new InvalidOperationException("Expected state rejection: " + text);
    }
}
