using syndishanx_iw_tool.Features;

namespace Features.Tests;

internal static class TeammateGodmodeFeatureTests {
    private static TeammateGodmodeFeature Create(RecordingFlagsTransport transport) =>
        new(transport, TimeSpan.FromMilliseconds(2));

    public static async Task EnableAndDisablePreserveOtherFlags() {
        var transport = new RecordingFlagsTransport { Flags = 0xA4 };
        await using var feature = Create(transport);
        TestAssert.Equal(true, await feature.SetAsync(true));
        TestAssert.Equal((uint)0xA5, transport.Flags);
        TestAssert.Equal(true, feature.Active);
        TestAssert.Equal(true, feature.HasPendingRestore);
        TestAssert.Equal(true, await feature.SetAsync(false));
        TestAssert.Equal((uint)0xA4, transport.Flags);
        TestAssert.Equal(false, feature.Active);
        TestAssert.Equal(false, feature.HasPendingRestore);
        TestAssert.Equal(2, transport.Writes);
    }

    public static async Task MonitorOnlyReadsAndRepeatedEnableDoesNotWrite() {
        var transport = new RecordingFlagsTransport();
        await using var feature = Create(transport);
        await feature.SetAsync(true);
        await TestAssert.WaitUntilAsync(() => transport.Reads >= 8);
        TestAssert.Equal(true, await feature.SetAsync(true));
        TestAssert.Equal(1, transport.Writes);
        TestAssert.Equal(true, feature.Active);
    }

    public static async Task RepeatedOnOffCyclesRestoreEachOwnedChange() {
        var transport = new RecordingFlagsTransport();
        await using var feature = Create(transport);
        for (int cycle = 0; cycle < 3; cycle++) {
            TestAssert.Equal(true, await feature.SetAsync(true));
            TestAssert.Equal(true, await feature.SetAsync(false));
            TestAssert.Equal((uint)0x200, transport.Flags);
        }
        TestAssert.Equal(6, transport.Writes);
    }

    public static async Task AlreadySetFlagIsNeverAdoptedOrRestored() {
        var transport = new RecordingFlagsTransport { Flags = 0x201 };
        await using var feature = Create(transport);
        TestAssert.Equal(false, await feature.SetAsync(true));
        TestAssert.Equal(false, feature.HasPendingRestore);
        TestAssert.Equal(true, await feature.CloseAsync());
        TestAssert.Equal(0, transport.Writes);
        TestAssert.Equal((uint)0x201, transport.Flags);
    }

    public static async Task InitialConflictNeverStartsMonitorOrOverwritesForeignFlags() {
        var transport = new RecordingFlagsTransport();
        transport.OnWrite = (_, _, _) => {
            transport.Flags = 0x204;
            return true;
        };
        await using var feature = Create(transport);
        TestAssert.Equal(false, await feature.SetAsync(true));
        TestAssert.Equal(false, feature.Active);
        TestAssert.Equal(true, feature.HasPendingRestore);
        TestAssert.Equal(false, await feature.SetAsync(false));
        TestAssert.Equal(1, transport.Writes);
        TestAssert.Equal((uint)0x204, transport.Flags);
    }

    public static async Task RejectedUnchangedWriteReleasesPendingRestoration() {
        var transport = new RecordingFlagsTransport { OnWrite = (_, _, _) => false };
        await using var feature = Create(transport);
        TestAssert.Equal(false, await feature.SetAsync(true));
        TestAssert.Equal(false, feature.Active);
        TestAssert.Equal(false, feature.HasPendingRestore);
        TestAssert.Equal(1, transport.Writes);
    }

    public static async Task RejectedButAppliedWriteRetainsOwnershipUntilCleanup() {
        var transport = new RecordingFlagsTransport();
        transport.OnWrite = (_, _, desired) => {
            transport.Flags = desired;
            return false;
        };
        await using var feature = Create(transport);
        TestAssert.Equal(false, await feature.SetAsync(true));
        TestAssert.Equal(false, feature.Active);
        TestAssert.Equal(true, feature.HasPendingRestore);
        transport.OnWrite = null;
        TestAssert.Equal(true, await feature.SetAsync(false));
        TestAssert.Equal((uint)0x200, transport.Flags);
        TestAssert.Equal(false, feature.HasPendingRestore);
    }

    public static async Task BadStartupReadbackNeverReportsActive() {
        var transport = new RecordingFlagsTransport { OnRead = count => count == 1 ? 0x200u : 0x205u };
        await using var feature = Create(transport);
        TestAssert.Equal(false, await feature.SetAsync(true));
        TestAssert.Equal(false, feature.Active);
        TestAssert.Equal(true, feature.HasPendingRestore);
        TestAssert.Equal(1, transport.Writes);
        transport.OnRead = null;
        TestAssert.Equal(true, await feature.SetAsync(false));
    }

    public static async Task ForeignFlagsStopMonitoringAndRefuseRestoration() {
        var transport = new RecordingFlagsTransport();
        await using var feature = Create(transport);
        await feature.SetAsync(true);
        transport.Flags = 0x205;
        await TestAssert.WaitUntilAsync(() => !feature.Active);
        string? reason = feature.FailureReason;
        TestAssert.Equal(true, reason is not null);
        TestAssert.Equal(true, feature.HasPendingRestore);
        TestAssert.Equal(false, await feature.SetAsync(false));
        TestAssert.Equal(false, await feature.SetAsync(true));
        TestAssert.Equal(1, transport.Writes);
        TestAssert.Equal((uint)0x205, transport.Flags);
    }

    public static async Task GameClearedFlagIsNotReappliedAndKeepsItsReason() {
        var transport = new RecordingFlagsTransport();
        await using var feature = Create(transport);
        await feature.SetAsync(true);
        transport.Flags = 0x200;
        await TestAssert.WaitUntilAsync(() => !feature.Active);
        string? reason = feature.FailureReason;
        TestAssert.Equal(false, feature.HasPendingRestore);
        TestAssert.Equal(true, await feature.SetAsync(false));
        TestAssert.Equal(reason, feature.FailureReason);
        TestAssert.Equal(1, transport.Writes);
    }

    public static async Task IdentityReadFailureStopsButRetainsPendingRestoration() {
        var transport = new RecordingFlagsTransport();
        await using var feature = Create(transport);
        await feature.SetAsync(true);
        transport.OnRead = _ => throw new IOException("The selected identity changed");
        await TestAssert.WaitUntilAsync(() => !feature.Active);
        TestAssert.Equal("The selected identity changed", feature.FailureReason);
        TestAssert.Equal(true, feature.HasPendingRestore);
        TestAssert.Equal(false, await feature.CloseAsync());
        TestAssert.Equal(1, transport.Writes);
        // A retry cleans up only after the same synthetic identity is available
        transport.OnRead = null;
        TestAssert.Equal(true, await feature.CloseAsync());
        TestAssert.Equal(false, feature.HasPendingRestore);
    }

    public static async Task OriginalProcessDisconnectReleasesOwnershipWithoutWriting() {
        var transport = new RecordingFlagsTransport();
        await using var feature = Create(transport);
        await feature.SetAsync(true);
        transport.Connected = false;
        await TestAssert.WaitUntilAsync(() => !feature.Active);
        TestAssert.Equal(false, feature.HasPendingRestore);
        TestAssert.Equal(true, await feature.CloseAsync());
        TestAssert.Equal(1, transport.Writes);
    }

    public static async Task StartupReadAndWriteExceptionsNeverReportActive() {
        foreach (bool throwOnWrite in new[] { false, true }) {
            var transport = new RecordingFlagsTransport();
            if (throwOnWrite) {
                transport.OnWrite = (_, _, desired) => {
                    transport.Flags = desired;
                    throw new IOException("Write confirmation failed");
                };
            } else {
                transport.OnRead = _ => throw new IOException("Read failed");
            }
            await using var feature = Create(transport);
            TestAssert.Equal(false, await feature.SetAsync(true));
            TestAssert.Equal(false, feature.Active);
            TestAssert.Equal(throwOnWrite, feature.HasPendingRestore);
            transport.OnRead = null;
            transport.OnWrite = null;
        }
    }

    public static async Task FailedOffRemainsPendingAndCloseCanRetry() {
        var transport = new RecordingFlagsTransport();
        await using var feature = Create(transport);
        await feature.SetAsync(true);
        transport.OnWrite = (_, _, _) => false;
        TestAssert.Equal(false, await feature.CloseAsync());
        TestAssert.Equal(false, feature.Active);
        TestAssert.Equal(true, feature.HasPendingRestore);
        transport.OnWrite = null;
        TestAssert.Equal(true, await feature.CloseAsync());
        TestAssert.Equal(false, feature.HasPendingRestore);
        TestAssert.Equal(false, await feature.SetAsync(true));
    }

    public static async Task UnconfirmedOffReadbackRetainsPendingUntilOriginalObserved() {
        var transport = new RecordingFlagsTransport();
        await using var feature = new TeammateGodmodeFeature(transport, TimeSpan.FromSeconds(1));
        await feature.SetAsync(true);
        // The first off read is expected, but its confirmation is unavailable
        int offReads = 0;
        transport.OnRead = _ => Interlocked.Increment(ref offReads) == 1 ? 0x201u : 0x205u;
        TestAssert.Equal(false, await feature.SetAsync(false));
        TestAssert.Equal(true, feature.HasPendingRestore);
        transport.OnRead = null;
        TestAssert.Equal(true, await feature.SetAsync(false));
        TestAssert.Equal(false, feature.HasPendingRestore);
        TestAssert.Equal(2, transport.Writes);
    }

    public static async Task ConflictDuringRestorationNeverOverwritesForeignFlags() {
        var transport = new RecordingFlagsTransport();
        await using var feature = Create(transport);
        await feature.SetAsync(true);
        transport.OnWrite = (_, _, _) => {
            transport.Flags = 0x205;
            return true;
        };
        TestAssert.Equal(false, await feature.SetAsync(false));
        TestAssert.Equal((uint)0x205, transport.Flags);
        TestAssert.Equal(true, feature.HasPendingRestore);
        TestAssert.Equal(false, feature.Active);
    }

    public static async Task DisconnectedStartupNeverReadsOrWrites() {
        var transport = new RecordingFlagsTransport { Connected = false };
        await using var feature = Create(transport);
        TestAssert.Equal(false, await feature.SetAsync(true));
        TestAssert.Equal(false, feature.HasPendingRestore);
        TestAssert.Equal(0, transport.Reads);
        TestAssert.Equal(0, transport.Writes);
    }

    public static async Task OverlappingEnableAndOffFinishWithOriginalFlags() {
        var entered = TestAssert.Barrier();
        using var release = new ManualResetEventSlim();
        var transport = new RecordingFlagsTransport();
        transport.OnWrite = (count, _, _) => {
            if (count == 1) {
                entered.SetResult();
                if (!release.Wait(TimeSpan.FromSeconds(5))) {
                    throw new InvalidOperationException("The write barrier was not released");
                }
            }
            return true;
        };
        await using var feature = Create(transport);
        Task<bool> enable = Task.Run(() => feature.SetAsync(true));
        await entered.Task;
        Task<bool> disable = feature.SetAsync(false);
        TestAssert.Equal(false, disable.IsCompleted);
        release.Set();
        TestAssert.Equal(true, await enable);
        TestAssert.Equal(true, await disable);
        TestAssert.Equal((uint)0x200, transport.Flags);
        TestAssert.Equal(false, feature.Active);
        TestAssert.Equal(false, feature.HasPendingRestore);
        TestAssert.Equal(2, transport.Writes);
    }

    public static async Task CancelledStartupAndQueuedEnableNeverWrite() {
        var transport = new RecordingFlagsTransport();
        await using var feature = Create(transport);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        TestAssert.Equal(false, await feature.SetAsync(true, cancellation.Token));
        TestAssert.Equal(0, transport.Reads);
        TestAssert.Equal(0, transport.Writes);
        var entered = TestAssert.Barrier();
        using var release = new ManualResetEventSlim();
        transport.OnRead = count => {
            if (count == 1) {
                entered.SetResult();
                if (!release.Wait(TimeSpan.FromSeconds(5))) {
                    throw new InvalidOperationException("The startup barrier was not released");
                }
            }
            return transport.Flags;
        };
        Task<bool> first = Task.Run(() => feature.SetAsync(true));
        await entered.Task;
        using var queuedCancellation = new CancellationTokenSource();
        Task<bool> queued = feature.SetAsync(true, queuedCancellation.Token);
        queuedCancellation.Cancel();
        TestAssert.Equal(false, await queued);
        release.Set();
        TestAssert.Equal(true, await first);
        TestAssert.Equal(1, transport.Writes);
    }

    public static async Task CloseWaitsForMonitorReadAndRestoresBeforeReturning() {
        var entered = TestAssert.Barrier();
        using var release = new ManualResetEventSlim();
        var transport = new RecordingFlagsTransport();
        transport.OnRead = count => {
            if (count == 3) {
                entered.SetResult();
                if (!release.Wait(TimeSpan.FromSeconds(5))) {
                    throw new InvalidOperationException("The monitor barrier was not released");
                }
            }
            return transport.Flags;
        };
        await using var feature = Create(transport);
        await feature.SetAsync(true);
        await entered.Task;
        Task<bool> close = feature.CloseAsync();
        TestAssert.Equal(false, close.IsCompleted);
        release.Set();
        TestAssert.Equal(true, await close);
        TestAssert.Equal((uint)0x200, transport.Flags);
        TestAssert.Equal(false, feature.Active);
        TestAssert.Equal(false, feature.HasPendingRestore);
        TestAssert.Equal(false, await feature.SetAsync(true));
        TestAssert.Equal(2, transport.Writes);
    }
}
