using syndishanx_iw_tool.Features.Godmode;

namespace Features.Tests.Godmode;

internal static class GodmodeCancellationTests {
    public static async Task CancellationDuringOffRestoresPriorOnWithoutTouchingRemainingMembers() {
        var fixture = new GodmodeRegistryFixture(2);
        var registry = fixture.Create();
        using var cancellation = new CancellationTokenSource();
        try {
            TestAssert.Equal(true, (await registry.SetBatchAsync(fixture.Players, true)).Succeeded);
            fixture.Transports[fixture.Players[0]].OnWrite = (count, _, _) => {
                if (count == 2) {
                    cancellation.Cancel();
                }
                return true;
            };
            var cancelled = await registry.SetBatchAsync(fixture.Players, false, cancellation.Token);
            TestAssert.Equal(false, cancelled.Succeeded);
            TestAssert.Equal(true, cancelled.Cancelled);
            TestAssert.Equal(GodmodeValue.On, cancelled.Snapshot.Value);
            TestAssert.Equal(3, fixture.Transports[fixture.Players[0]].Writes);
            TestAssert.Equal(1, fixture.Transports[fixture.Players[1]].Writes);
            TestAssert.Equal(false, cancelled.Snapshot.HasPendingCleanup);
        } finally {
            await registry.CloseAsync();
        }
    }

    public static async Task CancellationAfterAppliedWriteRestoresEveryChangedMemberWithoutCancellation() {
        var fixture = new GodmodeRegistryFixture(3);
        var registry = fixture.Create();
        using var cancellation = new CancellationTokenSource();
        fixture.Transports[fixture.Players[1]].OnWrite = (count, _, _) => {
            if (count == 1) {
                cancellation.Cancel();
            }
            return true;
        };
        try {
            var result = await registry.SetBatchAsync(fixture.Players.Take(2).ToArray(), true, cancellation.Token);
            TestAssert.Equal(false, result.Succeeded);
            TestAssert.Equal(true, result.Cancelled);
            TestAssert.Equal(0x200U, fixture.Transports[fixture.Players[0]].Flags);
            TestAssert.Equal(0x200U, fixture.Transports[fixture.Players[1]].Flags);
            TestAssert.Equal(0, fixture.Transports[fixture.Players[2]].Writes);
            TestAssert.Equal(GodmodeValue.Off, result.Snapshot.Value);
        } finally {
            await registry.CloseAsync();
        }
    }

    public static async Task CancelledWaitingBatchNeverBindsOrWrites() {
        var fixture = new GodmodeRegistryFixture(2);
        var registry = fixture.Create();
        var entered = TestAssert.Barrier();
        var release = TestAssert.Barrier();
        fixture.Transports[fixture.Players[0]].OnWrite = (count, _, _) => {
            if (count == 1) {
                entered.TrySetResult();
                release.Task.GetAwaiter().GetResult();
            }
            return true;
        };
        try {
            Task<GodmodeOperationResult> first = Task.Run(() => registry.SetAsync(fixture.Players[0], true));
            await entered.Task;
            using var cancellation = new CancellationTokenSource();
            var queued = registry.SetAsync(fixture.Players[1], true, cancellation.Token);
            cancellation.Cancel();
            TestAssert.Equal(true, (await queued).Cancelled);
            TestAssert.Equal(1, fixture.Factories);
            TestAssert.Equal(0, fixture.Transports[fixture.Players[1]].Writes);
            release.TrySetResult();
            TestAssert.Equal(true, (await first).Succeeded);
        } finally {
            release.TrySetResult();
            await registry.CloseAsync();
        }
    }
}
