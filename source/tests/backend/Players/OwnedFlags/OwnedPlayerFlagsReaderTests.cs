using syndishanx_iw_tool.Players;

namespace Backend.Tests;

// The same sparse bytes exercise strict capture and retained cleanup without a game process
internal static class OwnedPlayerFlagsReaderTests {
    private static OwnedPlayerFlagsState Read(PlayerStateMemoryFixture memory, PlayerIdentity identity) =>
        PlayerStateReader.ReadOwnedFlags(PlayerStateMemoryFixture.ModuleBase, identity, memory.ReadBytes);

    public static void EverySlotKeepsItsExactFullIdentityAndUnsignedFlags() {
        for (int slot = 0; slot < 4; slot++) {
            var memory = new PlayerStateMemoryFixture();
            var identity = memory.Read(slot).Identity;
            memory.Int32(identity.Entity + 0x1C8, unchecked((int)0xF1234500U));
            var state = Read(memory, identity);
            TestAssert.Equal(identity, state.Identity);
            TestAssert.Equal(100, state.CommandTime);
            TestAssert.Equal(0xF1234500U, state.Flags);
            TestAssert.True(state.IsEnableEligible, "Normal play must retain strict enable eligibility");
        }
    }

    public static void DeathDownedSessionAndMovementKeepCleanupReadableButNeverAuthorizeOn() {
        foreach (int slot in Enumerable.Range(0, 4)) {
            foreach (var change in new (int Health, int Session, int Movement)[] {
                (0, 0, 0), (1, 0, 0), (-1, 0, 0), (10001, 0, 0),
                (100, 1, 0), (100, 2, 0), (100, 3, 0), (100, 99, 0), (100, 0, 1) }) {
                var memory = new PlayerStateMemoryFixture();
                var identity = memory.Read(slot).Identity;
                memory.Int32(identity.Entity + 0x1E0, change.Health);
                memory.Int32(identity.Client + 0x4A94, change.Session);
                memory.Int32(identity.Client + 4, change.Movement);
                var state = Read(memory, identity);
                TestAssert.Equal(identity, state.Identity);
                TestAssert.True(!state.IsEnableEligible, "Ineligible life must remain an owned cleanup fact");
                TestAssert.Throws<InvalidOperationException>(() => memory.Read(slot));
            }
        }
    }

    public static void ChangedIdentityGlobalsLinksNamesAndUnreadableFieldsCannotBecomeCleanupProof() {
        var mutations = new Action<PlayerStateMemoryFixture, PlayerIdentity>[] {
            (m, i) => m.Int64(i.Entity + 0x170, i.Client + 8),
            (m, i) => m.Int64(PlayerStateMemoryFixture.Table + i.Slot * 8, i.ServerClient + 8),
            (m, i) => m.Text(i.Client + 0x4BB0, "Replacement", 32),
            (m, i) => m.Text(i.ServerClient + 0x9F340, "FFFFFFFFFFFFFFFF", 21),
            (m, i) => m.Int64(i.ServerClient + 136, i.Entity + 8),
            (m, i) => m.Int32(i.Client + 0x4A90, 0),
            (m, i) => m.Int32(PlayerStateMemoryFixture.CountAddress, 3),
            (m, i) => m.Int32(PlayerStateMemoryFixture.ModuleBase + 0x6B2C9D4, 1),
            (m, i) => m.Int32(PlayerStateMemoryFixture.ModuleBase + 0x6B2C9D8, 1),
            (m, i) => m.Int32(PlayerStateMemoryFixture.ModuleBase + 0x6B2C9DC, 1),
            (m, i) => m.Int32(i.Client, -1)
        };
        foreach (var change in mutations) {
            var memory = new PlayerStateMemoryFixture();
            var identity = memory.Read().Identity;
            change(memory, identity);
            Rejects(() => Read(memory, identity));
        }
        var source = new PlayerStateMemoryFixture();
        var original = source.Read().Identity;
        Rejects(() => PlayerStateReader.ReadOwnedFlags(
            PlayerStateMemoryFixture.ModuleBase, original, (_, _) => null));
        TestAssert.Throws<InvalidOperationException>(() => Read(source, original with { Guid = "FFFFFFFFFFFFFFFF" }));
    }

    public static void IdentityAndEligibilityAreRecheckedAfterAnObservedReadTransition() {
        var changed = new PlayerStateMemoryFixture();
        var identity = changed.Read().Identity;
        changed.BeforeRead = (address, _, _) => {
            if (address == identity.Entity + 0x1C8) changed.Int64(identity.ServerClient + 136, identity.Entity + 8);
        };
        TestAssert.Throws<InvalidOperationException>(() => Read(changed, identity));
        var dead = new PlayerStateMemoryFixture();
        var retained = dead.Read().Identity;
        dead.BeforeRead = (address, _, _) => {
            if (address == retained.Entity + 0x1C8) dead.Int32(retained.Entity + 0x1E0, 0);
        };
        TestAssert.True(!Read(dead, retained).IsEnableEligible, "Late observed death cannot leave enable eligibility true");
    }
    private static void Rejects(Action read) {
        try { read(); }
        catch (Exception error) when (error is InvalidOperationException or System.IO.IOException) { return; }
        throw new InvalidOperationException("Unsupported ownership facts must reject");
    }
}
