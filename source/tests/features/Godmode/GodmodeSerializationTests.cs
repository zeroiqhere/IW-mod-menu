using syndishanx_iw_tool.Features.Godmode;

namespace Features.Tests.Godmode;

internal static class GodmodeSerializationTests {
    public static async Task QueuedBatchCopiesCallerIdentitiesBeforeWaitingAndCannotBeRetargeted() {
        var fixture = new GodmodeRegistryFixture(3);
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
            var first = Task.Run(() => registry.SetAsync(fixture.Players[0], true));
            await entered.Task;
            var captured = new[] { fixture.Players[1] };
            var queued = registry.SetBatchAsync(captured, true);
            captured[0] = fixture.Players[2];
            release.TrySetResult();
            TestAssert.Equal(true, (await first).Succeeded);
            TestAssert.Equal(true, (await queued).Succeeded);
            TestAssert.Equal(0x201U, fixture.Transports[fixture.Players[1]].Flags);
            TestAssert.Equal(0, fixture.Transports[fixture.Players[2]].Writes);
            TestAssert.Equal(2, fixture.Factories);
        } finally {
            release.TrySetResult();
            await registry.CloseAsync();
        }
    }

    public static async Task IndividualOffWaitsForBatchAndObservesItsActualOnState() {
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
            var batch = Task.Run(() => registry.SetBatchAsync(fixture.Players, true));
            await entered.Task;
            var individual = registry.SetAsync(fixture.Players[0], false);
            TestAssert.Equal(false, individual.IsCompleted);
            TestAssert.Equal(0, fixture.Transports[fixture.Players[1]].Writes);
            release.TrySetResult();
            TestAssert.Equal(true, (await batch).Succeeded);
            TestAssert.Equal(true, (await individual).Succeeded);
            TestAssert.Equal(GodmodeValue.Mixed, registry.Snapshot(fixture.Players).Value);
            TestAssert.Equal(0x200U, fixture.Transports[fixture.Players[0]].Flags);
            TestAssert.Equal(0x201U, fixture.Transports[fixture.Players[1]].Flags);
        } finally {
            release.TrySetResult();
            await registry.CloseAsync();
        }
    }

    public static async Task CloseWaitsForRealMonitorReadThenRejectsQueuedEnable() {
        var player = PlayerGodmodeRegistryTests.Identity(1);
        var transport = new RecordingFlagsTransport();
        var entered = TestAssert.Barrier();
        var release = TestAssert.Barrier();
        var registry = new PlayerGodmodeRegistry(_ => new FlagsPlayerGodmodeFeature(transport,
            TimeSpan.FromMilliseconds(1)), _ => true);
        try {
            TestAssert.Equal(true, (await registry.SetAsync(player, true)).Succeeded);
            int blocked = 0;
            transport.OnRead = _ => {
                if (Interlocked.Exchange(ref blocked, 1) == 0) {
                    entered.TrySetResult();
                    release.Task.GetAwaiter().GetResult();
                }
                return transport.Flags;
            };
            await entered.Task;
            var closing = registry.CloseAsync();
            var queued = registry.SetAsync(player, true);
            TestAssert.Equal(false, closing.IsCompleted);
            TestAssert.Equal(false, queued.IsCompleted);
            TestAssert.Equal(1, transport.Writes);
            release.TrySetResult();
            TestAssert.Equal(true, (await closing).SafeToRelease);
            TestAssert.Equal(false, (await queued).Succeeded);
            TestAssert.Equal(0x200U, transport.Flags);
            TestAssert.Equal(2, transport.Writes);
        } finally {
            release.TrySetResult();
            await registry.CloseAsync();
        }
    }
}
