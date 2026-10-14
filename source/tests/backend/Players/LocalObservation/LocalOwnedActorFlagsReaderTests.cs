using System.IO;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Backend.Tests;

public static class LocalOwnedActorFlagsReaderTests {
    public static void DeadNonplayingAndChangedMovementActorsRetainReadableOwnedFlags() {
        var states = new (int Health, int Session, int Movement)[] {
            (0, 1, 1), (1, 0, 0), (100, 2, 2), (100, 3, 1),
            (-1, 0, -1), (10001, 0, 0), (0, 4, 0), (100, -1, 0)
        };
        foreach (var state in states) {
            var fixture = new LocalActorFlagsMemoryFixture(2, state.Health, state.Session,
                movementType: state.Movement, flags: 0x201);
            LocalOwnedActorFlagsSnapshot snapshot = LocalOwnedActorFlagsReader.Read(
                LocalBindingMemoryFixture.ModuleBase, fixture.Memory.ReadBytes);
            TestAssert.Equal(2, snapshot.Binding.MemberIndex);
            TestAssert.Equal(state.Health, snapshot.Health);
            TestAssert.Equal(state.Session, snapshot.Actor.NativeSessionState);
            TestAssert.Equal(state.Movement, snapshot.MovementType);
            TestAssert.Equal(0x201u, snapshot.Flags);
            TestAssert.True(!snapshot.IsEnableEligible, "Readable ownership fields do not authorize enabling");
        }
    }

    public static void OwnedSnapshotEnableEligibilityPreservesTheStrictFeatureBoundary() {
        var states = new (int Health, int Session, int Movement, bool Eligible)[] {
            (2, 0, 0, true), (10000, 0, 0, true), (1, 0, 0, false),
            (0, 0, 0, false), (10001, 0, 0, false), (100, 1, 0, false),
            (100, 4, 0, false), (100, 0, 1, false)
        };
        foreach (var state in states) {
            var fixture = new LocalActorFlagsMemoryFixture(health: state.Health, session: state.Session,
                commandTime: 0, movementType: state.Movement);
            var snapshot = LocalOwnedActorFlagsReader.Read(LocalBindingMemoryFixture.ModuleBase, fixture.Memory.ReadBytes);
            TestAssert.Equal(state.Eligible, snapshot.IsEnableEligible);
        }
    }

    public static void AdvancingCommandTimeDuringTheFinalReadReturnsTheFinalWatermark() {
        var fixture = new LocalActorFlagsMemoryFixture(health: 0, session: 1,
            commandTime: 12345, movementType: 1, flags: 0x201);
        fixture.Memory.BeforeRead = (address, _, count) => {
            if (address == fixture.CommandTimeAddress && count == 2) {
                fixture.Memory.Int32(fixture.CommandTimeAddress, 12346);
            }
        };

        LocalOwnedActorFlagsSnapshot snapshot = LocalOwnedActorFlagsReader.Read(
            LocalBindingMemoryFixture.ModuleBase, fixture.Memory.ReadBytes);

        TestAssert.Equal(12346, snapshot.CommandTime);
        TestAssert.Equal(0x201U, snapshot.Flags);
        TestAssert.True(!snapshot.IsEnableEligible, "Command-time progress cannot make an ineligible owner enable-eligible");
    }

    public static void DeathPreservesTheOriginalBindingAndFlagWordForOwnedCleanup() {
        var fixture = new LocalActorFlagsMemoryFixture(3, flags: 0x201);
        LocalActorFlagsSnapshot enabled = fixture.Read();
        fixture.Memory.Int32(fixture.Actor.HealthAddress, 0);
        fixture.Memory.Int32(fixture.Actor.SessionStateAddress, 1);
        fixture.Memory.Int32(fixture.MovementAddress, 1);
        LocalOwnedActorFlagsSnapshot current = LocalOwnedActorFlagsReader.Read(
            LocalBindingMemoryFixture.ModuleBase, fixture.Memory.ReadBytes);
        TestAssert.Equal(enabled.Binding, current.Binding);
        TestAssert.Equal(enabled.Flags, current.Flags);
        TestAssert.Equal(LocalPlayerLife.Dead, current.Actor.Life);
        TestAssert.True(!current.IsEnableEligible, "Death retains cleanup facts without permitting a new enable");
        TestAssert.Throws<InvalidOperationException>(() => fixture.Read());
    }

    public static void OwnedReaderRejectsCompleteBindingAndHostChangesAcrossTheFlagRead() {
        var changes = new Action<LocalActorFlagsMemoryFixture>[] {
            fixture => {
                fixture.Memory.UInt64(LocalBindingMemoryFixture.AccountAddress, LocalBindingMemoryFixture.Account + 1);
                fixture.Memory.Registered(fixture.Memory.Session, 0, LocalBindingMemoryFixture.Account + 1);
            },
            fixture => fixture.Actor.ReplaceServerRecord(LocalBindingMemoryFixture.Server(0) + 0x100000),
            fixture => {
                long client = LocalBindingMemoryFixture.Client(0) + 0x100000;
                fixture.Actor.ReplaceClient(client);
                fixture.Memory.Int32(client + 0x4A94, 1);
            },
            fixture => fixture.Memory.Int32(LocalActorMemoryFixture.CurrentParty + 0x2C50, 1)
        };
        foreach (var change in changes) {
            var fixture = new LocalActorFlagsMemoryFixture(health: 0, session: 1, movementType: 1);
            fixture.Memory.BeforeRead = (address, _, count) => {
                if (address == fixture.FlagsAddress && count == 1) change(fixture);
            };
            TestAssert.Throws<InvalidOperationException>(() => LocalOwnedActorFlagsReader.Read(
                LocalBindingMemoryFixture.ModuleBase, fixture.Memory.ReadBytes));
        }
    }

    public static void OwnedReaderRejectsCommandTimeRollbackAndOtherFieldChangesOnRecheck() {
        var template = new LocalActorFlagsMemoryFixture();
        var fields = new (long Address, int Count)[] {
            (template.Actor.HealthAddress, 6), (template.Actor.SessionStateAddress, 6),
            (template.CommandTimeAddress, 2), (template.MovementAddress, 2), (template.FlagsAddress, 2)
        };
        foreach (var field in fields) {
            var fixture = new LocalActorFlagsMemoryFixture(health: 0, session: 1, movementType: 1, flags: 0x201);
            fixture.Memory.BeforeRead = (address, _, count) => {
                if (address == field.Address && count == field.Count) fixture.Memory.Int32(address, 7);
            };
            TestAssert.Throws<InvalidOperationException>(() => LocalOwnedActorFlagsReader.Read(
                LocalBindingMemoryFixture.ModuleBase, fixture.Memory.ReadBytes));
        }
        var negativeTime = new LocalActorFlagsMemoryFixture(health: 0, session: 1, movementType: 1);
        negativeTime.Memory.BeforeRead = (address, _, count) => {
            if (address == negativeTime.CommandTimeAddress && count == 2) {
                negativeTime.Memory.Int32(negativeTime.CommandTimeAddress, -1);
            }
        };
        TestAssert.Throws<InvalidOperationException>(() => LocalOwnedActorFlagsReader.Read(
            LocalBindingMemoryFixture.ModuleBase, negativeTime.Memory.ReadBytes));
        // Life may be ineligible, but an observed change still rejects the entire copied result
        foreach (bool health in new[] { true, false }) {
            var fixture = new LocalActorFlagsMemoryFixture(health: 0, session: 1, movementType: 1);
            long field = health ? fixture.Actor.HealthAddress : fixture.Actor.SessionStateAddress;
            fixture.Memory.BeforeRead = (address, _, count) => {
                if (address == field && count == 3) fixture.Memory.Int32(address, health ? 50 : 2);
            };
            TestAssert.Throws<InvalidOperationException>(() => LocalOwnedActorFlagsReader.Read(
                LocalBindingMemoryFixture.ModuleBase, fixture.Memory.ReadBytes));
            TestAssert.True(!fixture.Memory.Reads.Any(item => item.Address == fixture.FlagsAddress),
                "A changed life field cannot survive into flag ownership inspection");
        }
    }

    public static void OwnedReaderNeverReturnsMissingPartialOrFailedFeatureFields() {
        TestAssert.Throws<ArgumentNullException>(() => LocalOwnedActorFlagsReader.Read(LocalBindingMemoryFixture.ModuleBase, null!));
        var template = new LocalActorFlagsMemoryFixture();
        foreach (long field in new[] { template.CommandTimeAddress, template.MovementAddress, template.FlagsAddress }) {
            foreach (int failure in new[] { 0, 1, 2, 3 }) {
                var fixture = new LocalActorFlagsMemoryFixture(health: 0, session: 1, movementType: 1);
                TestAssert.Throws<IOException>(() => LocalOwnedActorFlagsReader.Read(LocalBindingMemoryFixture.ModuleBase,
                    (address, width) => {
                        if (address != field) return fixture.Memory.ReadBytes(address, width);
                        if (failure == 0) return null;
                        if (failure == 1) return new byte[width - 1];
                        if (failure == 2) throw new IOException("Owned restore read failure");
                        byte[]? bytes = fixture.Memory.ReadBytes(address, width);
                        return fixture.Memory.Reads.Count(item => item.Address == field) == 2 ? null : bytes;
                    }));
            }
        }
    }

    public static void OwnedReaderCopiesReusedCallbackStorageWithoutReplacingFlagBits() {
        var fixture = new LocalActorFlagsMemoryFixture(1, health: 0, session: 1, movementType: 1, flags: uint.MaxValue);
        var buffers = new Dictionary<int, byte[]>();
        var snapshot = LocalOwnedActorFlagsReader.Read(LocalBindingMemoryFixture.ModuleBase, (address, width) => {
            byte[] exact = fixture.Memory.ReadBytes(address, width)!;
            if (!buffers.TryGetValue(width, out byte[]? buffer)) {
                buffer = new byte[width];
                buffers.Add(width, buffer);
            }
            exact.CopyTo(buffer, 0);
            return buffer;
        });
        TestAssert.Equal(1, snapshot.Binding.MemberIndex);
        TestAssert.Equal(0, snapshot.Health);
        TestAssert.Equal(12345, snapshot.CommandTime);
        TestAssert.Equal(uint.MaxValue, snapshot.Flags);
        TestAssert.True(!snapshot.IsEnableEligible, "Copied dead-state fields remain enable-ineligible");
    }

    public static void OwnedReaderRejectsNegativeTimeAndUnboundActorsBeforeReadingFlags() {
        foreach (bool unbound in new[] { true, false }) {
            var fixture = new LocalActorFlagsMemoryFixture(health: 0, session: 1, movementType: 1);
            if (unbound) fixture.Memory.UInt64(LocalBindingMemoryFixture.AccountAddress, 0);
            else fixture.Memory.Int32(fixture.CommandTimeAddress, -1);
            TestAssert.Throws<InvalidOperationException>(() => LocalOwnedActorFlagsReader.Read(
                LocalBindingMemoryFixture.ModuleBase, fixture.Memory.ReadBytes));
            TestAssert.True(!fixture.Memory.Reads.Any(item => item.Address == fixture.FlagsAddress),
                "Ownership and time guards still run before flags are read");
        }
    }

    public static void OwnedSnapshotConstructorRetainsUnknownLifeButRejectsNullActorAndNegativeTime() {
        TestAssert.Throws<ArgumentNullException>(() => new LocalOwnedActorFlagsSnapshot(null!, 0, 0, 0));
        LocalActorSnapshot actor = new LocalActorMemoryFixture(health: 0, session: 4).Read();
        TestAssert.Throws<ArgumentOutOfRangeException>(() => new LocalOwnedActorFlagsSnapshot(actor, -1, 1, 0));
        var snapshot = new LocalOwnedActorFlagsSnapshot(actor, 0, int.MinValue, 0x201);
        TestAssert.Equal(LocalPlayerLife.Unknown, snapshot.Actor.Life);
        TestAssert.True(!snapshot.IsEnableEligible, "Unsupported copied session state never grants enable eligibility");
    }
}
