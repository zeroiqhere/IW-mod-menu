using syndishanx_iw_tool.Features.Godmode;
using syndishanx_iw_tool.Players;

namespace Features.Tests.Godmode;

internal static class GodmodeRosterTests {
    public static async Task InvalidAndAliasedCapturedPlayersNeverInvokeFactoryOrWrite() {
        int factories = 0;
        var registry = new PlayerGodmodeRegistry(_ => {
            factories++;
            return new ControlledGodmodeFeature();
        }, _ => true);
        var first = PlayerGodmodeRegistryTests.Identity(1);
        var second = PlayerGodmodeRegistryTests.Identity(2);
        var invalid = new IReadOnlyList<PlayerIdentity>[] {
            Array.Empty<PlayerIdentity>(), new[] { first, first }, new[] { first with { Slot = 4 } },
            new[] { first with { Entity = 0 } }, new[] { first with { Guid = "" } },
            new[] { first, second with { Entity = first.Entity } },
            new[] { first, second with { Client = first.Client } },
            new[] { first, second with { Guid = first.Guid } },
            new[] { first, second with { ServerClients = 0x4000 } },
            new[] { first, second with { ServerCount = 3 } }, new PlayerIdentity[] { null! },
            Enumerable.Range(0, 5).Select(PlayerGodmodeRegistryTests.Identity).ToArray()
        };
        foreach (IReadOnlyList<PlayerIdentity> roster in invalid) {
            TestAssert.Equal(false, (await registry.SetBatchAsync(roster, true)).Succeeded);
            TestAssert.Equal(GodmodeValue.Unknown, registry.Snapshot(roster).Value);
        }
        TestAssert.Equal(0, factories);
        TestAssert.Equal(true, (await registry.CloseAsync()).SafeToRelease);
    }

    public static async Task OneAdapterCannotBeReusedForDifferentFullPlayerIdentities() {
        var adapter = new ControlledGodmodeFeature();
        var registry = new PlayerGodmodeRegistry(_ => adapter, _ => true);
        var players = new[] { PlayerGodmodeRegistryTests.Identity(1), PlayerGodmodeRegistryTests.Identity(2) };
        try {
            TestAssert.Equal(false, (await registry.SetBatchAsync(players, true)).Succeeded);
            TestAssert.Equal(0, adapter.Sets);
            TestAssert.Equal(false, adapter.Active);
        } finally {
            await registry.CloseAsync();
        }
    }

    public static async Task FactoryAndIdentityExceptionsFailWithoutWritingEarlierMembers() {
        var players = new[] { PlayerGodmodeRegistryTests.Identity(1), PlayerGodmodeRegistryTests.Identity(2) };
        var adapter = new ControlledGodmodeFeature();
        var registry = new PlayerGodmodeRegistry(identity => identity == players[0]
            ? adapter : throw new IOException("Synthetic factory failure"), _ => true);
        TestAssert.Equal(false, (await registry.SetBatchAsync(players, true)).Succeeded);
        TestAssert.Equal(0, adapter.Sets);
        TestAssert.Equal(true, (await registry.CloseAsync()).SafeToRelease);
        int factories = 0;
        var invalidIdentity = new PlayerGodmodeRegistry(_ => {
            factories++;
            return new ControlledGodmodeFeature();
        }, _ => throw new IOException("Synthetic read failed"));
        TestAssert.Equal(false, (await invalidIdentity.SetBatchAsync(players, true)).Succeeded);
        TestAssert.Equal(0, factories);
        await invalidIdentity.CloseAsync();
    }
}
