using syndishanx_iw_tool.Features.Godmode;
using syndishanx_iw_tool.Players;

namespace Features.Tests.Godmode;

// These adapters use the real flag feature against owned synthetic flags
internal static class PlayerGodmodeRegistryTests {
    internal static PlayerIdentity Identity(int slot) => new(slot, 0x1000 + slot * 0x100,
        0x2000 + slot * 0x100, 0x3000, 4, $"Player {slot}", $"{slot + 1:X16}");

    public static async Task BatchAndIndividualShareOneActualStatePerIdentity() {
        var players = new[] { Identity(0), Identity(1), Identity(2), Identity(3) };
        var transports = players.ToDictionary(identity => identity, _ => new RecordingFlagsTransport());
        int factories = 0;
        var registry = new PlayerGodmodeRegistry(identity => {
            factories++;
            return new FlagsPlayerGodmodeFeature(transports[identity], TimeSpan.FromSeconds(10));
        }, identity => transports.ContainsKey(identity));
        try {
            TestAssert.Equal(GodmodeValue.Unknown, registry.Snapshot(players).Value);
            TestAssert.Equal(true, (await registry.SetBatchAsync(players, true)).Succeeded);
            TestAssert.Equal(GodmodeValue.On, registry.Snapshot(players).Value);
            TestAssert.Equal(4, factories);
            TestAssert.Equal(true, (await registry.SetAsync(players[1], false)).Succeeded);
            TestAssert.Equal(GodmodeValue.Mixed, registry.Snapshot(players).Value);
            TestAssert.Equal(0x200U, transports[players[1]].Flags);
            TestAssert.Equal(true, (await registry.SetBatchAsync(players, true)).Succeeded);
            TestAssert.Equal(4, factories);
            TestAssert.Equal(true, (await registry.SetBatchAsync(players, false)).Succeeded);
            TestAssert.Equal(GodmodeValue.Off, registry.Snapshot(players).Value);
        } finally {
            await registry.CloseAsync();
        }
    }

    public static async Task ForeignFlagAndInvalidRosterFailBeforeAnyWrite() {
        var first = Identity(1);
        var second = Identity(2);
        var firstTransport = new RecordingFlagsTransport();
        var secondTransport = new RecordingFlagsTransport { Flags = 0x201 };
        var transports = new Dictionary<PlayerIdentity, RecordingFlagsTransport> {
            [first] = firstTransport, [second] = secondTransport
        };
        var registry = new PlayerGodmodeRegistry(identity => new FlagsPlayerGodmodeFeature(transports[identity]),
            identity => transports.ContainsKey(identity));
        try {
            TestAssert.Equal(false, (await registry.SetBatchAsync(new[] { first, second }, true)).Succeeded);
            TestAssert.Equal(0, firstTransport.Writes);
            TestAssert.Equal(0, secondTransport.Writes);
            TestAssert.Equal(false, (await registry.SetBatchAsync(new[] { first, first }, true)).Succeeded);
            TestAssert.Equal(false, (await registry.SetBatchAsync(Array.Empty<PlayerIdentity>(), true)).Succeeded);
            TestAssert.Equal(false, (await registry.SetAsync(Identity(0), true)).Succeeded);
            TestAssert.Equal(0, firstTransport.Writes);
            TestAssert.Equal(0, secondTransport.Writes);
        } finally {
            await registry.CloseAsync();
        }
    }

    public static async Task FailedEnableRollsBackOnlyChangesAndPreservesEarlierOnMember() {
        var players = new[] { Identity(1), Identity(2), Identity(3) };
        var transports = players.ToDictionary(identity => identity, _ => new RecordingFlagsTransport());
        var registry = new PlayerGodmodeRegistry(identity => new FlagsPlayerGodmodeFeature(transports[identity],
            TimeSpan.FromSeconds(10)), identity => transports.ContainsKey(identity));
        try {
            TestAssert.Equal(true, (await registry.SetAsync(players[0], true)).Succeeded);
            transports[players[2]].OnWrite = (_, _, desired) => {
                // A rejected write can still have applied the target flags
                transports[players[2]].Flags = desired;
                return false;
            };
            var result = await registry.SetBatchAsync(players, true);
            TestAssert.Equal(false, result.Succeeded);
            TestAssert.Equal(0x201U, transports[players[0]].Flags);
            TestAssert.Equal(1, transports[players[0]].Writes);
            TestAssert.Equal(0x200U, transports[players[1]].Flags);
            TestAssert.Equal(0x200U, transports[players[2]].Flags);
            TestAssert.Equal(GodmodeValue.Mixed, registry.Snapshot(players).Value);
        } finally {
            await registry.CloseAsync();
        }
    }
}
