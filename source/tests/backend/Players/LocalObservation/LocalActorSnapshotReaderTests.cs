using System.IO;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Backend.Tests;

public static class LocalActorSnapshotReaderTests {
    public static void BoundActorSnapshotIncludesLifeAndNamedSessionHostRole() {
        foreach (int member in new[] { 0, 1, 2, 3 }) {
            var fixture = new LocalActorMemoryFixture(member);
            LocalActorSnapshot snapshot = fixture.Read();
            TestAssert.Equal(member, snapshot.Binding.MemberIndex);
            TestAssert.Equal(LocalBindingMemoryFixture.Server(member), snapshot.Binding.Identity.ServerClient);
            TestAssert.Equal(100, snapshot.SignedHealth);
            TestAssert.Equal(0, snapshot.NativeSessionState);
            TestAssert.Equal(LocalPlayerLife.Alive, snapshot.Life);
            TestAssert.True(snapshot.IsSessionHost, "The role is the named native session comparison");
            TestAssert.True(fixture.Memory.Reads.Count < 512, "The complete binding and consistency passes remain bounded");
        }
        var nonhost = new LocalActorMemoryFixture(1, host: false);
        TestAssert.True(!nonhost.Read().IsSessionHost, "A verified nonhost remains distinct from life");
        TestAssert.Throws<ArgumentNullException>(() => new LocalActorSnapshot(null!, 100, 0, true));
    }

    public static void SignedLifeAndUnsupportedSessionsPreserveTheExactNativeInterpretation() {
        var cases = new (int Health, int Session, LocalPlayerLife Life)[] {
            (1, 0, LocalPlayerLife.Alive), (100, 1, LocalPlayerLife.Alive),
            (100, 3, LocalPlayerLife.Alive), (0, 0, LocalPlayerLife.Dead),
            (int.MinValue, 1, LocalPlayerLife.Dead), (int.MaxValue, 2, LocalPlayerLife.Dead),
            (0, -1, LocalPlayerLife.Unknown), (0, 4, LocalPlayerLife.Unknown),
            (100, int.MaxValue, LocalPlayerLife.Unknown)
        };
        foreach (var item in cases) {
            var snapshot = new LocalActorMemoryFixture(health: item.Health, session: item.Session).Read();
            TestAssert.Equal(item.Life, snapshot.Life);
            TestAssert.Equal(item.Health, snapshot.SignedHealth);
            TestAssert.Equal(item.Session, snapshot.NativeSessionState);
        }
    }

    public static void TwoCompleteActorBindingsMustAgreeAcrossTheScalarRead() {
        var changes = new Action<LocalActorMemoryFixture>[] {
            fixture => {
                fixture.Memory.UInt64(LocalBindingMemoryFixture.AccountAddress, LocalBindingMemoryFixture.Account + 1);
                fixture.Memory.Registered(fixture.Memory.Session, 0, LocalBindingMemoryFixture.Account + 1);
            },
            fixture => {
                fixture.Memory.Registered(LocalBindingMemoryFixture.TrueSession, 0, LocalBindingMemoryFixture.Account);
                fixture.Memory.Bytes(LocalBindingMemoryFixture.Selector + 0x10, new byte[] { 1 });
            },
            fixture => {
                fixture.Memory.Text(LocalBindingMemoryFixture.Client(0) + 0x4BB0, "NewName", 32);
                fixture.Memory.Text(LocalBindingMemoryFixture.Client(0) + 0x4C50, "NewName", 32);
                fixture.Memory.Text(LocalBindingMemoryFixture.Server(0) + 1188, "NewName", 32);
            },
            fixture => fixture.Memory.Text(LocalBindingMemoryFixture.Server(0) + 0x9F340, "1234567890ABCDEF", 21),
            fixture => fixture.ReplaceServerRecord(LocalBindingMemoryFixture.Server(0) + 0x100000),
            fixture => fixture.ReplaceClient(LocalBindingMemoryFixture.Client(0) + 0x100000)
        };
        foreach (var change in changes) {
            var fixture = new LocalActorMemoryFixture();
            fixture.Memory.BeforeRead = (address, _, count) => {
                if (address == LocalBindingMemoryFixture.ControllerAddress && count == 3) change(fixture);
            };
            TestAssert.Throws<InvalidOperationException>(() => fixture.Read());
        }
    }

    public static void HealthAndSessionBytesMustAgreeOnTheirFinalRecheck() {
        foreach (bool changeHealth in new[] { true, false }) {
            var fixture = new LocalActorMemoryFixture();
            long field = changeHealth ? fixture.HealthAddress : fixture.SessionStateAddress;
            fixture.Memory.BeforeRead = (address, _, count) => {
                if (address == field && count == 2) fixture.Memory.Int32(field, changeHealth ? 0 : 2);
            };
            TestAssert.Throws<InvalidOperationException>(() => fixture.Read());
        }
    }

    public static void SessionHostSourceFieldsShareTheFinalScalarConsistencyPass() {
        foreach (long field in new[] { LocalActorMemoryFixture.CurrentParty,
            LocalActorMemoryFixture.CurrentParty + 0x2D10, LocalActorMemoryFixture.CurrentParty + 0x2C50 }) {
            var fixture = new LocalActorMemoryFixture();
            fixture.Memory.BeforeRead = (address, _, count) => {
                if (address != field || count != 2) return;
                if (field == LocalActorMemoryFixture.CurrentParty) fixture.Memory.Int64(field, 0);
                else fixture.Memory.Int32(field, field == LocalActorMemoryFixture.CurrentParty + 0x2C50 ? 1 : 0);
            };
            TestAssert.Throws<InvalidOperationException>(() => fixture.Read());
        }
    }

    public static void FailedAccountBindingStopsBeforeActorScalarsOrHostFieldsAreRead() {
        var fixture = new LocalActorMemoryFixture();
        fixture.Memory.UInt64(LocalBindingMemoryFixture.AccountAddress, 0);
        TestAssert.Throws<InvalidOperationException>(() => fixture.Read());
        TestAssert.True(!fixture.Memory.Reads.Any(item => item.Address == fixture.HealthAddress
            || item.Address == fixture.SessionStateAddress || item.Address == LocalActorMemoryFixture.CurrentParty),
            "Unbound account failure does not read actor life or host role");
    }

    public static void MissingIncompleteAndFailedActorFieldsNeverYieldASnapshot() {
        TestAssert.Throws<ArgumentNullException>(() => LocalActorSnapshotReader.Read(LocalBindingMemoryFixture.ModuleBase, null!));
        var template = new LocalActorMemoryFixture();
        foreach (long field in new[] { template.HealthAddress, template.SessionStateAddress,
            LocalActorMemoryFixture.CurrentParty, LocalActorMemoryFixture.CurrentParty + 0x2D10,
            LocalActorMemoryFixture.CurrentParty + 0x2C50 }) {
            foreach (int failure in new[] { 0, 1, 2, 3 }) {
                var fixture = new LocalActorMemoryFixture();
                TestAssert.Throws<IOException>(() => LocalActorSnapshotReader.Read(LocalBindingMemoryFixture.ModuleBase,
                    (address, width) => {
                        if (address != field) return fixture.Memory.ReadBytes(address, width);
                        if (failure == 0) return null;
                        if (failure == 1) return new byte[width - 1];
                        if (failure == 2) throw new IOException("Owned actor read failure");
                        byte[]? bytes = fixture.Memory.ReadBytes(address, width);
                        return fixture.Memory.Reads.Count(item => item.Address == field) == 2 ? null : bytes;
                    }));
            }
        }
    }

    public static void ReusedReadBuffersCannotChangeTheCopiedActorFacts() {
        var fixture = new LocalActorMemoryFixture(health: 1, session: 3);
        var reused = new Dictionary<int, byte[]>();
        LocalActorSnapshot snapshot = LocalActorSnapshotReader.Read(LocalBindingMemoryFixture.ModuleBase, (address, width) => {
            byte[] bytes = fixture.Memory.ReadBytes(address, width)!;
            if (!reused.TryGetValue(width, out byte[]? buffer)) {
                buffer = new byte[width];
                reused.Add(width, buffer);
            }
            bytes.CopyTo(buffer, 0);
            return buffer;
        });
        TestAssert.Equal(1, snapshot.SignedHealth);
        TestAssert.Equal(3, snapshot.NativeSessionState);
        TestAssert.Equal(LocalPlayerLife.Alive, snapshot.Life);
        TestAssert.True(snapshot.IsSessionHost, "Same-width host reads do not replace copied scalar storage");
    }
}
