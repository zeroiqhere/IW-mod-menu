using syndishanx_iw_tool.Features;

namespace Features.Tests;

internal static class GodmodeFeatureTests {
    private static GodmodeFeature Create(RecordingTransport transport) =>
        new(transport, 10000, TimeSpan.FromMilliseconds(2));

    public static async Task EnableThenStopLeavesHealthUntouchedAfterLastWrite() {
        var transport = new RecordingTransport();
        await using var feature = Create(transport);
        TestAssert.Equal(true, await feature.SetAsync(true));
        TestAssert.Equal(true, feature.Active);
        TestAssert.Equal(10000, transport.Health);
        TestAssert.Equal(true, await feature.SetAsync(false));
        int stoppedCount = transport.HealthWrites;
        await Task.Delay(20);
        TestAssert.Equal(stoppedCount, transport.HealthWrites);
        TestAssert.Equal(false, feature.Active);
        TestAssert.Equal(10000, transport.Health);
    }

    public static async Task InvalidLivingHealthNeverWrites() {
        foreach (int health in new[] { -1, 0, 1, 10001 }) {
            var transport = new RecordingTransport { Health = health };
            await using var feature = Create(transport);
            TestAssert.Equal(false, await feature.SetAsync(true));
            TestAssert.Equal(false, feature.Active);
            TestAssert.Equal(0, transport.HealthWrites);
        }
    }

    public static async Task DisconnectedAndFailedInitialWriteNeverStartLoop() {
        foreach (bool connected in new[] { false, true }) {
            var transport = new RecordingTransport {
                Connected = connected,
                OnHealthWrite = _ => false
            };
            await using var feature = Create(transport);
            TestAssert.Equal(false, await feature.SetAsync(true));
            TestAssert.Equal(false, feature.Active);
            TestAssert.Equal(connected ? 1 : 0, transport.HealthWrites);
        }
    }

    public static async Task UnconfirmedInitialHealthWriteNeverStartsLoop() {
        var transport = new RecordingTransport { OnHealthRead = count => count == 2 ? 100 : 10000 };
        await using var feature = Create(transport);
        TestAssert.Equal(false, await feature.SetAsync(true));
        TestAssert.Equal(false, feature.Active);
        TestAssert.Equal(1, transport.HealthWrites);
    }

    public static async Task LoopWriteFailureStopsAndRecordsReason() {
        var transport = new RecordingTransport { OnHealthWrite = count => count < 2 };
        await using var feature = Create(transport);
        TestAssert.Equal(true, await feature.SetAsync(true));
        await TestAssert.WaitUntilAsync(() => !feature.Active);
        TestAssert.Equal(2, transport.HealthWrites);
        TestAssert.Equal(true, feature.FailureReason is not null);
        await feature.SetAsync(false);
        TestAssert.Equal(2, transport.HealthWrites);
    }

    public static async Task LoopDisconnectStopsBeforeAnotherWrite() {
        var transport = new RecordingTransport();
        await using var feature = Create(transport);
        TestAssert.Equal(true, await feature.SetAsync(true));
        transport.Connected = false;
        await TestAssert.WaitUntilAsync(() => !feature.Active);
        TestAssert.Equal(1, transport.HealthWrites);
        TestAssert.Equal(true, feature.FailureReason is not null);
    }

    public static async Task LoopDeadHealthStopsWithoutReviving() {
        var transport = new RecordingTransport();
        await using var feature = Create(transport);
        await feature.SetAsync(true);
        transport.Health = 0;
        await TestAssert.WaitUntilAsync(() => !feature.Active);
        TestAssert.Equal(1, transport.HealthWrites);
        TestAssert.Equal(0, transport.Health);
    }

    public static async Task LoopReadExceptionStopsAndCanRestartAfterRecovery() {
        var transport = new RecordingTransport {
            OnHealthRead = count => count > 2 ? throw new InvalidOperationException("Read failed")
                                                : count == 1 ? 100 : 10000
        };
        await using var feature = Create(transport);
        await feature.SetAsync(true);
        await TestAssert.WaitUntilAsync(() => !feature.Active);
        TestAssert.Equal("Read failed", feature.FailureReason);
        transport.OnHealthRead = null;
        TestAssert.Equal(true, await feature.SetAsync(true));
        TestAssert.Equal(true, feature.Active);
    }

    public static async Task CloseWaitsForInFlightWriteAndRejectsFurtherRequests() {
        var entered = TestAssert.Barrier();
        using var release = new ManualResetEventSlim();
        var transport = new RecordingTransport {
            OnHealthWrite = count => {
                if (count == 2) {
                    entered.SetResult();
                    if (!release.Wait(TimeSpan.FromSeconds(5))) {
                        throw new InvalidOperationException("The write barrier was not released");
                    }
                }
                return true;
            }
        };
        await using var feature = Create(transport);
        await feature.SetAsync(true);
        await entered.Task;
        // Close must wait for an already running backend call to return
        Task<bool> close = feature.CloseAsync();
        TestAssert.Equal(false, close.IsCompleted);
        release.Set();
        TestAssert.Equal(true, await close);
        TestAssert.Equal(false, feature.Active);
        TestAssert.Equal(2, transport.HealthWrites);
        TestAssert.Equal(false, await feature.SetAsync(true));
        await Task.Delay(20);
        TestAssert.Equal(2, transport.HealthWrites);
    }

    public static async Task LoopLastStandHealthStopsWithoutReviving() {
        var transport = new RecordingTransport {
            // The first read and confirmation are living; the worker observes last stand next
            OnHealthRead = count => count == 1 ? 100 : count == 2 ? 10000 : 1
        };
        await using var feature = Create(transport);
        TestAssert.Equal(true, await feature.SetAsync(true));
        await TestAssert.WaitUntilAsync(() => !feature.Active);
        TestAssert.Equal(1, transport.HealthWrites);
        TestAssert.Equal(true, feature.FailureReason is not null);
    }

    public static async Task MinimumStandingHealthCanEnable() {
        var transport = new RecordingTransport { Health = 2 };
        await using var feature = Create(transport);
        TestAssert.Equal(true, await feature.SetAsync(true));
        TestAssert.Equal(true, feature.Active);
        TestAssert.Equal(true, await feature.SetAsync(false));
    }

    public static async Task ConstructorRejectsNonLivingFreezeValuesAndAcceptsTheMinimumLivingValue() {
        foreach (int frozen in new[] { int.MinValue, -1, 0, 1 }) {
            var transport = new RecordingTransport();
            bool rejected = false;
            try {
                await using var invalid = new GodmodeFeature(transport, frozen);
            } catch (ArgumentOutOfRangeException exception) when (exception.ParamName == "frozenHealth") {
                rejected = true;
            }
            TestAssert.Equal(true, rejected);
            TestAssert.Equal(0, transport.HealthReads);
            TestAssert.Equal(0, transport.HealthWrites);
        }
        // The lowest supported standing value can form a valid configuration without starting a worker
        var supported = new RecordingTransport { Health = 2 };
        await using var valid = new GodmodeFeature(supported, 2);
        TestAssert.Equal(false, valid.Active);
        TestAssert.Equal(0, supported.HealthReads);
        TestAssert.Equal(0, supported.HealthWrites);
    }

    public static async Task CancelledStartupNeverWrites() {
        var transport = new RecordingTransport();
        await using var feature = Create(transport);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        TestAssert.Equal(false, await feature.SetAsync(true, cancellation.Token));
        TestAssert.Equal(0, transport.HealthWrites);
    }
}
