using syndishanx_iw_tool.Features.Godmode;

namespace Features.Tests.Godmode;

internal static class GodmodeCleanupTests {
    public static async Task UnconfirmedOldRestorationCannotReturnSuccessfulOffWithoutCleanup() {
        var player = PlayerGodmodeRegistryTests.Identity(1);
        var changed = player with { Guid = "FFFFFFFFFFFFFFFF" };
        var adapter = new ControlledGodmodeFeature();
        var registry = new PlayerGodmodeRegistry(_ => adapter, _ => true);
        TestAssert.Equal(true, (await registry.SetAsync(player, false)).Succeeded);
        adapter.OnClose = () => Task.FromResult(new GodmodeCloseResult(true, false, "Synthetic restoration unconfirmed"));
        try {
            TestAssert.Equal(false, (await registry.SetAsync(changed, false)).Succeeded);
            var originalOff = await registry.SetAsync(player, false);
            TestAssert.Equal(false, originalOff.Succeeded);
            TestAssert.Equal(GodmodeValue.Unknown, originalOff.Snapshot.Value);
            TestAssert.Equal(true, originalOff.Snapshot.HasPendingCleanup);
            TestAssert.Equal(player, originalOff.Snapshot.Members[0].Identity);
            TestAssert.Equal(0, adapter.Sets);
        } finally {
            adapter.OnClose = null;
            await registry.CloseAsync();
        }
    }

    public static async Task UnconfirmedOldWorkerStopCannotReturnSuccessfulOffWithoutCleanup() {
        var player = PlayerGodmodeRegistryTests.Identity(1);
        var changed = player with { Guid = "FFFFFFFFFFFFFFFF" };
        var adapter = new ControlledGodmodeFeature();
        var registry = new PlayerGodmodeRegistry(_ => adapter, _ => true);
        TestAssert.Equal(true, (await registry.SetAsync(player, false)).Succeeded);
        adapter.OnClose = () => Task.FromResult(new GodmodeCloseResult(false, true, "Synthetic stop unconfirmed"));
        try {
            TestAssert.Equal(false, (await registry.SetAsync(changed, false)).Succeeded);
            var originalOff = await registry.SetAsync(player, false);
            TestAssert.Equal(false, originalOff.Succeeded);
            TestAssert.Equal(GodmodeValue.Unknown, originalOff.Snapshot.Value);
            TestAssert.Equal(true, originalOff.Snapshot.HasPendingCleanup);
            TestAssert.Equal(player, originalOff.Snapshot.Members[0].Identity);
            TestAssert.Equal(0, adapter.Sets);
        } finally {
            adapter.OnClose = null;
            await registry.CloseAsync();
        }
    }

    public static async Task ThrownRollbackRetainsAnActiveChangedMemberAndBlocksNewEnables() {
        var players = new[] { PlayerGodmodeRegistryTests.Identity(1), PlayerGodmodeRegistryTests.Identity(2) };
        var original = new ControlledGodmodeFeature();
        int factories = 0;
        var registry = new PlayerGodmodeRegistry(identity => {
            factories++;
            return identity == players[0] ? original : new ControlledGodmodeFeature();
        }, _ => true);
        original.OnSet = (desired, _) => {
            if (desired) {
                original.Active = true;
                original.HasPendingRestore = true;
            }
            throw new IOException("Synthetic change or rollback threw");
        };
        try {
            var failed = await registry.SetAsync(players[0], true);
            TestAssert.Equal(false, failed.Succeeded);
            TestAssert.Equal(true, failed.Snapshot.HasPendingCleanup);
            TestAssert.Equal(GodmodeValue.Unknown, failed.Snapshot.Value);
            TestAssert.Equal(false, (await registry.SetAsync(players[1], true)).Succeeded);
            TestAssert.Equal(1, factories);
            TestAssert.Equal(false, (await registry.SetAsync(players[0], false)).Succeeded);
            TestAssert.Equal(true, registry.Snapshot(new[] { players[0] }).HasPendingCleanup);
            TestAssert.Equal(false, (await registry.SetAsync(players[1], true)).Succeeded);
            original.OnSet = null;
            TestAssert.Equal(true, (await registry.SetAsync(players[0], false)).Succeeded);
            TestAssert.Equal(false, registry.Snapshot(players).HasPendingCleanup);
            TestAssert.Equal(true, (await registry.SetAsync(players[1], true)).Succeeded);
        } finally {
            original.OnSet = null;
            await registry.CloseAsync();
        }
    }

    public static async Task UnconfirmedCloseRestorationRemainsPendingEvenWithoutAnActiveWorker() {
        var player = PlayerGodmodeRegistryTests.Identity(1);
        var adapter = new ControlledGodmodeFeature();
        var registry = new PlayerGodmodeRegistry(_ => adapter, _ => true);
        TestAssert.Equal(true, (await registry.SetAsync(player, true)).Succeeded);
        adapter.OnClose = () => {
            adapter.Active = false;
            adapter.HasPendingRestore = false;
            return Task.FromResult(new GodmodeCloseResult(true, false, "Synthetic restoration unconfirmed"));
        };
        try {
            var failed = await registry.CloseAsync();
            TestAssert.Equal(true, failed.Stopped);
            TestAssert.Equal(false, failed.Restored);
            TestAssert.Equal(true, failed.Snapshot.HasPendingCleanup);
            TestAssert.Equal(GodmodeValue.Unknown, failed.Snapshot.Value);
            adapter.OnClose = null;
            TestAssert.Equal(true, (await registry.CloseAsync()).SafeToRelease);
        } finally {
            adapter.OnClose = null;
            await registry.CloseAsync();
        }
    }

    public static async Task UnconfirmedRollbackBlocksNewBindingButOriginalOffCanRetry() {
        var fixture = new GodmodeRegistryFixture(2);
        var registry = fixture.Create();
        var original = fixture.Transports[fixture.Players[0]];
        original.OnWrite = (count, _, desired) => {
            if (count == 1) {
                original.Flags = desired;
                return false;
            }
            throw new IOException("Synthetic restoration denied");
        };
        try {
            var failed = await registry.SetAsync(fixture.Players[0], true);
            TestAssert.Equal(false, failed.Succeeded);
            TestAssert.Equal(true, failed.Snapshot.HasPendingCleanup);
            TestAssert.Equal(GodmodeValue.Unknown, failed.Snapshot.Value);
            TestAssert.Equal(false, (await registry.SetAsync(fixture.Players[1], true)).Succeeded);
            TestAssert.Equal(false, (await registry.SetAsync(fixture.Players[1], false)).Succeeded);
            TestAssert.Equal(1, fixture.Factories);
            TestAssert.Equal(0, fixture.Transports[fixture.Players[1]].Writes);
            original.OnWrite = null;
            TestAssert.Equal(true, (await registry.SetAsync(fixture.Players[0], false)).Succeeded);
            TestAssert.Equal(false, registry.Snapshot(fixture.Players).HasPendingCleanup);
            TestAssert.Equal(true, (await registry.SetAsync(fixture.Players[1], true)).Succeeded);
        } finally {
            original.OnWrite = null;
            await registry.CloseAsync();
        }
    }

    public static async Task CloseAttemptsEveryMemberAndRetriesUnconfirmedRestoration() {
        var fixture = new GodmodeRegistryFixture(2);
        var registry = fixture.Create();
        TestAssert.Equal(true, (await registry.SetBatchAsync(fixture.Players, true)).Succeeded);
        fixture.Transports[fixture.Players[0]].OnWrite = (_, _, _) => false;
        try {
            var failed = await registry.CloseAsync();
            TestAssert.Equal(true, failed.Stopped);
            TestAssert.Equal(false, failed.Restored);
            TestAssert.Equal(false, failed.SafeToRelease);
            TestAssert.Equal(0x201U, fixture.Transports[fixture.Players[0]].Flags);
            TestAssert.Equal(0x200U, fixture.Transports[fixture.Players[1]].Flags);
            TestAssert.Equal(2, failed.Snapshot.Members.Count);
            TestAssert.Equal(true, failed.Snapshot.Closed);
            fixture.Transports[fixture.Players[0]].OnWrite = null;
            var recovered = await registry.CloseAsync();
            TestAssert.Equal(true, recovered.SafeToRelease);
            TestAssert.Equal(false, recovered.Snapshot.HasPendingCleanup);
            TestAssert.Equal(false, (await registry.SetAsync(fixture.Players[0], true)).Succeeded);
        } finally {
            fixture.Transports[fixture.Players[0]].OnWrite = null;
            await registry.CloseAsync();
        }
    }

    public static async Task ThrownCloseRetainsOriginalIdentityAndStillClosesOtherWorkers() {
        var players = new[] { PlayerGodmodeRegistryTests.Identity(1), PlayerGodmodeRegistryTests.Identity(2) };
        var first = new ControlledGodmodeFeature();
        var second = new ControlledGodmodeFeature();
        var registry = new PlayerGodmodeRegistry(identity => identity == players[0] ? first : second, _ => true);
        TestAssert.Equal(true, (await registry.SetBatchAsync(players, true)).Succeeded);
        first.OnClose = () => throw new IOException("Synthetic worker close failed");
        try {
            var failed = await registry.CloseAsync();
            TestAssert.Equal(false, failed.Stopped);
            TestAssert.Equal(false, failed.Restored);
            TestAssert.Equal(1, second.Closes);
            TestAssert.Equal(false, second.Active);
            TestAssert.Equal(true, failed.Snapshot.HasPendingCleanup);
            TestAssert.Equal(players[0], failed.Snapshot.Members[0].Identity);
            first.OnClose = null;
            TestAssert.Equal(true, (await registry.CloseAsync()).SafeToRelease);
            TestAssert.Equal(2, first.Closes);
            TestAssert.Equal(2, second.Closes);
        } finally {
            first.OnClose = null;
            await registry.CloseAsync();
        }
    }

    public static async Task FalseCloseClaimCannotReleaseAnActiveOrUnrestoredAdapter() {
        var player = PlayerGodmodeRegistryTests.Identity(1);
        var adapter = new ControlledGodmodeFeature();
        var registry = new PlayerGodmodeRegistry(_ => adapter, _ => true);
        TestAssert.Equal(true, (await registry.SetAsync(player, true)).Succeeded);
        adapter.OnClose = () => Task.FromResult(new GodmodeCloseResult(true, true));
        try {
            var outcome = await registry.CloseAsync();
            TestAssert.Equal(false, outcome.SafeToRelease);
            TestAssert.Equal(false, outcome.Stopped);
            TestAssert.Equal(false, outcome.Restored);
        } finally {
            adapter.OnClose = null;
            await registry.CloseAsync();
        }
    }
}
