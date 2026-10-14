using System.IO;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Backend.Tests;

public static class LocalActorFlagsReaderTests {
    public static void VerifiedAccountMembersCanReadFlagsIncludingNonzeroLocalSlots() {
        foreach (int member in new[] { 0, 1, 2, 3 }) {
            var fixture = new LocalActorFlagsMemoryFixture(member, flags: uint.MaxValue);
            LocalActorFlagsSnapshot snapshot = fixture.Read();
            TestAssert.Equal(member, snapshot.Binding.MemberIndex);
            TestAssert.Equal(member, snapshot.Binding.Identity.Slot);
            TestAssert.Equal(LocalBindingMemoryFixture.Server(member), snapshot.Binding.Identity.ServerClient);
            TestAssert.Equal(100, snapshot.Health);
            TestAssert.Equal(12345, snapshot.CommandTime);
            TestAssert.Equal(0, snapshot.MovementType);
            TestAssert.Equal(uint.MaxValue, snapshot.Flags);
            TestAssert.Equal(LocalPlayerLife.Alive, snapshot.Actor.Life);
            TestAssert.True(fixture.Memory.Reads.Count < 1024 && fixture.Memory.Reads.All(item => item.Width <= 32),
                "Full proofs and copied feature fields stay bounded");
        }
        foreach (int health in new[] { 2, 10000 }) {
            var snapshot = new LocalActorFlagsMemoryFixture(health: health, commandTime: 0, flags: 0).Read();
            TestAssert.Equal(health, snapshot.Health);
            TestAssert.Equal(0, snapshot.CommandTime);
            TestAssert.Equal(0u, snapshot.Flags);
        }
    }

    public static void DownedUnsupportedAndNonplayingActorsRejectBeforeFlagReads() {
        var states = new (int Health, int Session)[] {
            (1, 0), (0, 0), (-1, 0), (10001, 0), (int.MaxValue, 0),
            (100, 1), (100, 2), (100, 3), (100, -1), (100, 4)
        };
        foreach (var state in states) {
            var fixture = new LocalActorFlagsMemoryFixture(health: state.Health, session: state.Session);
            TestAssert.Throws<InvalidOperationException>(() => fixture.Read());
            TestAssert.True(!fixture.Memory.Reads.Any(item => item.Address == fixture.FlagsAddress),
                "Feature eligibility must reject before reading local flags");
        }
        TestAssert.Equal(LocalPlayerLife.Alive, new LocalActorMemoryFixture(health: 1).Read().Life);
        TestAssert.Equal(LocalPlayerLife.Unknown, new LocalActorMemoryFixture(session: 4).Read().Life);
    }

    public static void NonNormalMovementAndNegativeCommandTimeRejectBeforeFlagReads() {
        var states = new (int Command, int Movement)[] { (-1, 0), (int.MinValue, 0), (12345, 1), (12345, -1) };
        foreach (var state in states) {
            var fixture = new LocalActorFlagsMemoryFixture(commandTime: state.Command, movementType: state.Movement);
            TestAssert.Throws<InvalidOperationException>(() => fixture.Read());
            TestAssert.True(!fixture.Memory.Reads.Any(item => item.Address == fixture.FlagsAddress),
                "Movement and command time must pass before flags are observed");
        }
    }

    public static void AdvancingCommandTimeDuringTheFinalReadReturnsTheFinalWatermark() {
        var fixture = new LocalActorFlagsMemoryFixture(commandTime: 12345);
        fixture.Memory.BeforeRead = (address, _, count) => {
            if (address == fixture.CommandTimeAddress && count == 2) {
                fixture.Memory.Int32(fixture.CommandTimeAddress, 12346);
            }
        };

        LocalActorFlagsSnapshot snapshot = fixture.Read();

        TestAssert.Equal(12346, snapshot.CommandTime);
    }

    public static void CommandTimeRollbackAndOtherFieldChangesRejectTheFinalConsistencyPass() {
        var template = new LocalActorFlagsMemoryFixture();
        foreach (long field in new[] { template.FlagsAddress, template.CommandTimeAddress, template.MovementAddress }) {
            var fixture = new LocalActorFlagsMemoryFixture();
            fixture.Memory.BeforeRead = (address, _, count) => {
                if (address == field && count == 2) fixture.Memory.Int32(field, 1);
            };
            TestAssert.Throws<InvalidOperationException>(() => fixture.Read());
        }
        var negativeTime = new LocalActorFlagsMemoryFixture();
        negativeTime.Memory.BeforeRead = (address, _, count) => {
            if (address == negativeTime.CommandTimeAddress && count == 2) {
                negativeTime.Memory.Int32(negativeTime.CommandTimeAddress, -1);
            }
        };
        TestAssert.Throws<InvalidOperationException>(() => negativeTime.Read());
    }

    public static void CompleteActorReplacementAfterFlagAdmissionRejectsTheSnapshot() {
        var changes = new Action<LocalActorFlagsMemoryFixture>[] {
            fixture => {
                fixture.Memory.UInt64(LocalBindingMemoryFixture.AccountAddress, LocalBindingMemoryFixture.Account + 1);
                fixture.Memory.Registered(fixture.Memory.Session, 0, LocalBindingMemoryFixture.Account + 1);
            },
            fixture => {
                fixture.Memory.Registered(LocalBindingMemoryFixture.TrueSession, 0, LocalBindingMemoryFixture.Account);
                fixture.Memory.Bytes(LocalBindingMemoryFixture.Selector + 0x10, new byte[] { 1 });
                fixture.Memory.Int64(LocalActorMemoryFixture.CurrentParty, LocalBindingMemoryFixture.TrueSession);
            },
            fixture => fixture.Actor.ReplaceServerRecord(LocalBindingMemoryFixture.Server(0) + 0x100000),
            fixture => {
                long client = LocalBindingMemoryFixture.Client(0) + 0x100000;
                fixture.Actor.ReplaceClient(client);
                fixture.Memory.Int32(client + 0x4A94, 0);
            }
        };
        foreach (var change in changes) {
            var fixture = new LocalActorFlagsMemoryFixture();
            fixture.Memory.BeforeRead = (address, _, count) => {
                if (address == fixture.FlagsAddress && count == 1) change(fixture);
            };
            TestAssert.Throws<InvalidOperationException>(() => fixture.Read());
        }
    }

    public static void ActorLifeSessionAndHostChangesRejectFlagPublication() {
        var changes = new Action<LocalActorFlagsMemoryFixture>[] {
            fixture => fixture.Memory.Int32(fixture.Actor.HealthAddress, 50),
            fixture => fixture.Memory.Int32(fixture.Actor.SessionStateAddress, 1),
            fixture => fixture.Memory.Int32(fixture.Actor.SessionStateAddress, 4),
            fixture => fixture.Memory.Int32(LocalActorMemoryFixture.CurrentParty + 0x2C50, 1)
        };
        foreach (var change in changes) {
            var fixture = new LocalActorFlagsMemoryFixture();
            fixture.Memory.BeforeRead = (address, _, count) => {
                if (address == fixture.FlagsAddress && count == 1) change(fixture);
            };
            TestAssert.Throws<InvalidOperationException>(() => fixture.Read());
        }
        foreach (bool health in new[] { true, false }) {
            var early = new LocalActorFlagsMemoryFixture();
            long field = health ? early.Actor.HealthAddress : early.Actor.SessionStateAddress;
            early.Memory.BeforeRead = (address, _, count) => {
                if (address == field && count == 3) early.Memory.Int32(address, health ? 50 : 1);
            };
            TestAssert.Throws<InvalidOperationException>(() => early.Read());
            TestAssert.True(!early.Memory.Reads.Any(item => item.Address == early.FlagsAddress),
                "Life changes after the first actor proof reject before flags are read");

            // The outer field pass must also reject a late change after the second actor proof
            var late = new LocalActorFlagsMemoryFixture();
            field = health ? late.Actor.HealthAddress : late.Actor.SessionStateAddress;
            late.Memory.BeforeRead = (address, _, count) => {
                if (address == field && count == 6) late.Memory.Int32(address, 1);
            };
            TestAssert.Throws<InvalidOperationException>(() => late.Read());
        }
    }

    public static void UnboundOrOutOfRangePointersNeverReachFeatureFields() {
        foreach (long client in new[] { 0L, 0x10001L, 0x7FFFFFFF0000L }) {
            var fixture = new LocalActorFlagsMemoryFixture();
            fixture.Memory.Int64(LocalBindingMemoryFixture.Entity(0) + 0x170, client);
            TestAssert.Throws<InvalidOperationException>(() => fixture.Read());
            TestAssert.True(!fixture.Memory.Reads.Any(item => item.Address == fixture.FlagsAddress
                || item.Address == fixture.CommandTimeAddress || item.Address == fixture.MovementAddress),
                "Invalid client links cannot supply local feature scalars");
        }
        var unbound = new LocalActorFlagsMemoryFixture();
        unbound.Memory.UInt64(LocalBindingMemoryFixture.AccountAddress, 0);
        TestAssert.Throws<InvalidOperationException>(() => unbound.Read());
        TestAssert.True(!unbound.Memory.Reads.Any(item => item.Address == unbound.FlagsAddress),
            "Account proof remains required even when an entity exists");
        var invalidModule = new LocalActorFlagsMemoryFixture();
        TestAssert.Throws<InvalidOperationException>(() => LocalActorFlagsReader.Read(0x7FFFFFFF0000, invalidModule.Memory.ReadBytes));
        TestAssert.Equal(0, invalidModule.Memory.Reads.Count);
    }

    public static void MissingPartialAndFailedFeatureFieldsNeverYieldAFlagSnapshot() {
        TestAssert.Throws<ArgumentNullException>(() => LocalActorFlagsReader.Read(LocalBindingMemoryFixture.ModuleBase, null!));
        var template = new LocalActorFlagsMemoryFixture();
        foreach (long field in new[] { template.FlagsAddress, template.CommandTimeAddress, template.MovementAddress }) {
            foreach (int failure in new[] { 0, 1, 2, 3 }) {
                var fixture = new LocalActorFlagsMemoryFixture();
                TestAssert.Throws<IOException>(() => LocalActorFlagsReader.Read(LocalBindingMemoryFixture.ModuleBase,
                    (address, width) => {
                        if (address != field) return fixture.Memory.ReadBytes(address, width);
                        if (failure == 0) return null;
                        if (failure == 1) return new byte[width - 1];
                        if (failure == 2) throw new IOException("Owned feature read failure");
                        byte[]? bytes = fixture.Memory.ReadBytes(address, width);
                        return fixture.Memory.Reads.Count(item => item.Address == field) == 2 ? null : bytes;
                    }));
            }
        }
    }

    public static void ReusedReadBuffersCannotChangeCopiedLocalFlagValues() {
        var fixture = new LocalActorFlagsMemoryFixture(2, health: 2, commandTime: 7, flags: 0x80000201);
        var buffers = new Dictionary<int, byte[]>();
        LocalActorFlagsSnapshot snapshot = LocalActorFlagsReader.Read(LocalBindingMemoryFixture.ModuleBase, (address, width) => {
            byte[] exact = fixture.Memory.ReadBytes(address, width)!;
            if (!buffers.TryGetValue(width, out byte[]? buffer)) {
                buffer = new byte[width];
                buffers.Add(width, buffer);
            }
            exact.CopyTo(buffer, 0);
            return buffer;
        });
        TestAssert.Equal(2, snapshot.Binding.MemberIndex);
        TestAssert.Equal(2, snapshot.Health);
        TestAssert.Equal(7, snapshot.CommandTime);
        TestAssert.Equal(0x80000201u, snapshot.Flags);
    }

    public static void ConstructedFlagSnapshotsCannotBypassFeatureScalarEligibility() {
        TestAssert.Throws<ArgumentNullException>(() => new LocalActorFlagsSnapshot(null!, 1, 0, 0));
        foreach (var state in new[] { (Health: 1, Session: 0), (Health: 10001, Session: 0),
            (Health: 100, Session: 1), (Health: 100, Session: 4) }) {
            LocalActorSnapshot actor = new LocalActorMemoryFixture(health: state.Health, session: state.Session).Read();
            TestAssert.Throws<ArgumentException>(() => new LocalActorFlagsSnapshot(actor, 1, 0, 0));
        }
        LocalActorSnapshot valid = new LocalActorMemoryFixture().Read();
        TestAssert.Throws<ArgumentException>(() => new LocalActorFlagsSnapshot(valid, -1, 0, 0));
        TestAssert.Throws<ArgumentException>(() => new LocalActorFlagsSnapshot(valid, 1, 1, 0));
        TestAssert.Equal(100, new LocalActorFlagsSnapshot(valid, 1, 0, uint.MaxValue).Health);
    }
}
