using syndishanx_iw_tool.Features.Godmode;

namespace Features.Tests.Godmode;

internal static class GodmodeSnapshotTests {
    public static async Task FlagsChangingBeforeFirstEnablePreserveTheActuallyOwnedBaselineDuringOff() {
        var player = PlayerGodmodeRegistryTests.Identity(1);
        var transport = new RecordingFlagsTransport();
        transport.OnRead = count => {
            if (count == 2) {
                // The primitive captures fresh unrelated flags after group preflight
                transport.Flags = 0x220;
            }
            return transport.Flags;
        };
        var registry = new PlayerGodmodeRegistry(_ => new FlagsPlayerGodmodeFeature(transport,
            TimeSpan.FromSeconds(10)), _ => true);
        try {
            TestAssert.Equal(true, (await registry.SetAsync(player, true)).Succeeded);
            TestAssert.Equal(0x221U, transport.Flags);
            TestAssert.Equal(true, (await registry.SetAsync(player, false)).Succeeded);
            TestAssert.Equal(0x220U, transport.Flags);
        } finally {
            await registry.CloseAsync();
        }
    }

    public static async Task CloseRetainsEveryOwnedIdentityAfterCleanRetargetIntoAnotherMatch() {
        var fixture = new GodmodeRegistryFixture(2);
        var registry = fixture.Create();
        TestAssert.Equal(true, (await registry.SetBatchAsync(fixture.Players, false)).Succeeded);
        var nextMatch = fixture.Players[0] with { Entity = 0x6000, Client = 0x7000, ServerClients = 0x8000 };
        fixture.Valid.Clear();
        fixture.Valid.Add(nextMatch);
        fixture.Transports.Add(nextMatch, new());
        TestAssert.Equal(true, (await registry.SetAsync(nextMatch, true)).Succeeded);
        var closed = await registry.CloseAsync();
        TestAssert.Equal(true, closed.SafeToRelease);
        TestAssert.Equal(2, closed.Snapshot.Members.Count);
        TestAssert.Equal(true, closed.Snapshot.Members.Any(member => member.Identity == fixture.Players[1]));
        TestAssert.Equal(true, closed.Snapshot.Members.Any(member => member.Identity == nextMatch));
        TestAssert.Equal(0x200U, fixture.Transports[nextMatch].Flags);
    }

    public static async Task SnapshotIsImmutableAndActiveOwnershipDoesNotBlockOtherPlayers() {
        var fixture = new GodmodeRegistryFixture(2);
        var registry = fixture.Create();
        try {
            var first = await registry.SetAsync(fixture.Players[0], true);
            TestAssert.Equal(true, first.Succeeded);
            TestAssert.Equal(true, first.Snapshot.Members[0].HasPendingRestore);
            TestAssert.Equal(false, first.Snapshot.HasPendingCleanup);
            TestAssert.Equal(true, (await registry.SetAsync(fixture.Players[1], true)).Succeeded);
            var before = registry.Snapshot(fixture.Players);
            TestAssert.Equal(true, (await registry.SetAsync(fixture.Players[0], false)).Succeeded);
            TestAssert.Equal(GodmodeValue.On, before.Value);
            TestAssert.Equal(GodmodeValue.On, before.Members[0].Value);
            TestAssert.Equal(GodmodeValue.Mixed, registry.Snapshot(fixture.Players).Value);
            TestAssert.Equal(true, ((IList<PlayerGodmodeSnapshot>)before.Members).IsReadOnly);
        } finally {
            await registry.CloseAsync();
        }
    }

    public static async Task ProcessExitAllowsCleanupButNeverClaimsConnectedOffOrOn() {
        var fixture = new GodmodeRegistryFixture(1);
        var registry = fixture.Create();
        TestAssert.Equal(true, (await registry.SetAsync(fixture.Players[0], true)).Succeeded);
        fixture.Transports[fixture.Players[0]].Connected = false;
        TestAssert.Equal(GodmodeValue.Unknown, registry.Snapshot(fixture.Players).Value);
        var close = await registry.CloseAsync();
        TestAssert.Equal(true, close.SafeToRelease);
        TestAssert.Equal(false, close.Snapshot.HasPendingCleanup);
        TestAssert.Equal(GodmodeValue.Unknown, close.Snapshot.Value);
        TestAssert.Equal(1, fixture.Transports[fixture.Players[0]].Writes);
    }

    public static async Task ClearedOwnedFlagCannotSilentlyReportSuccessfulEnable() {
        var fixture = new GodmodeRegistryFixture(1);
        var registry = fixture.Create();
        try {
            TestAssert.Equal(true, (await registry.SetAsync(fixture.Players[0], true)).Succeeded);
            fixture.Transports[fixture.Players[0]].Flags = 0x200;
            TestAssert.Equal(false, (await registry.SetAsync(fixture.Players[0], true)).Succeeded);
            TestAssert.Equal(1, fixture.Transports[fixture.Players[0]].Writes);
        } finally {
            await registry.CloseAsync();
        }
    }
}
