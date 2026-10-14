using syndishanx_iw_tool.Features.Godmode;

namespace Features.Tests.Godmode;

internal static class GodmodeIdentityTests {
    public static async Task ChangedRosterBeforeBatchFailsAndSnapshotDoesNotClaimOn() {
        var fixture = new GodmodeRegistryFixture(2);
        var registry = fixture.Create();
        try {
            TestAssert.Equal(true, (await registry.SetAsync(fixture.Players[0], true)).Succeeded);
            fixture.Valid.Remove(fixture.Players[0]);
            TestAssert.Equal(GodmodeValue.Unknown, registry.Snapshot(fixture.Players).Value);
            TestAssert.Equal(GodmodeValue.Unknown, registry.Snapshot(new[] { fixture.Players[0] }).Value);
            TestAssert.Equal(false, (await registry.SetBatchAsync(fixture.Players, true)).Succeeded);
            TestAssert.Equal(1, fixture.Transports[fixture.Players[0]].Writes);
            TestAssert.Equal(0, fixture.Transports[fixture.Players[1]].Writes);
        } finally {
            await registry.CloseAsync();
        }
    }

    public static async Task FullIdentityChangeCannotRetargetAnActiveSlotEvenWithSameName() {
        var fixture = new GodmodeRegistryFixture(1);
        var registry = fixture.Create();
        try {
            TestAssert.Equal(true, (await registry.SetAsync(fixture.Players[0], true)).Succeeded);
            var changed = fixture.Players[0] with { Guid = "FFFFFFFFFFFFFFFF", Client = 0x5000 };
            fixture.Valid.Add(changed);
            fixture.Transports.Add(changed, new());
            TestAssert.Equal(false, (await registry.SetAsync(changed, true)).Succeeded);
            TestAssert.Equal(1, fixture.Factories);
            TestAssert.Equal(0, fixture.Transports[changed].Writes);
            TestAssert.Equal(0x201U, fixture.Transports[fixture.Players[0]].Flags);
            TestAssert.Equal(true, (await registry.SetAsync(fixture.Players[0], false)).Succeeded);
            TestAssert.Equal(true, (await registry.SetAsync(changed, true)).Succeeded);
            TestAssert.Equal(2, fixture.Factories);
            TestAssert.Equal(GodmodeValue.Unknown, registry.Snapshot(new[] { fixture.Players[0] }).Value);
        } finally {
            await registry.CloseAsync();
        }
    }

    public static async Task RosterChangedBetweenWritesRollsBackOnlyTheOriginalBoundMember() {
        var fixture = new GodmodeRegistryFixture(2);
        var registry = fixture.Create();
        fixture.Transports[fixture.Players[0]].OnWrite = (count, _, _) => {
            if (count == 1) {
                fixture.Valid.Remove(fixture.Players[1]);
            }
            return true;
        };
        try {
            TestAssert.Equal(false, (await registry.SetBatchAsync(fixture.Players, true)).Succeeded);
            TestAssert.Equal(0x200U, fixture.Transports[fixture.Players[0]].Flags);
            TestAssert.Equal(0, fixture.Transports[fixture.Players[1]].Writes);
        } finally {
            await registry.CloseAsync();
        }
    }
}
