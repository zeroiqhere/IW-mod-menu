using Apparition.Backend.Session;
using syndishanx_iw_tool.Overlay;

namespace Session.Tests;

// Real selected-peer adapters are observed only by retained session work
internal static class RetailTeammatePublicationTests {
    public static async Task CachedMenuFramesNeverEnterSelectedPeerReadCallbacks() {
        var transport = new RecordingRetailTransport { LocalSlot = 0, FailLocalRead = false,
            OnLocalRead = null, RejectLocalFlagRestore = false };
        var session = RetailBackendSessionTests.Create(transport);
        await session.ExecuteAsync(new(MenuActionKind.Connect));
        await session.ExecuteAsync(RetailBackendSessionTests.Select(1));
        int nativeReads = 0;
        transport.OnFlagRead = () => Interlocked.Increment(ref nativeReads);
        try {
            int writes = transport.FlagWrites;
            int localReads = transport.LocalReadCalls;
            for (int frame = 0; frame < 40; frame++) {
                var view = session.ReadMenuState();
                Check(view.Connected && view.TeammateGodmodeValue == "OFF" && view.CanTeammateGodmode,
                    "A cached frame lost its captured peer OFF capability");
                Check(session.ReadSelectedPlayerTarget()?.Identity.Slot == 1,
                    "A cached target read lost its original peer receipt");
            }
            Check(nativeReads == 0 && transport.LocalReadCalls == localReads && transport.FlagWrites == writes,
                "Menu or target publication entered a native flag preflight or changed flags");

            // The production polling entry must refresh peers even without a local provider
            await session.RefreshLocalObservationAsync();
            Check(nativeReads > 0 && transport.FlagWrites == writes,
                "The retained observation worker skipped its selected peer or wrote during refresh");
        } finally {
            transport.OnFlagRead = null;
            await session.ShutdownAsync();
        }
    }

    public static async Task RetainedWorkerRefreshWithdrawsForeignFlagsAndReplacementWithoutAProvider() {
        var transport = new RecordingRetailTransport { LocalSlot = 0, FailLocalRead = false,
            OnLocalRead = null, RejectLocalFlagRestore = false };
        var session = RetailBackendSessionTests.Create(transport);
        await session.ExecuteAsync(new(MenuActionKind.Connect));
        await session.ExecuteAsync(RetailBackendSessionTests.Select(1));
        try {
            var target = session.ReadSelectedPlayerTarget();
            Check(target is not null && session.ReadMenuState().TeammateGodmodeValue == "OFF",
                "Selection did not capture an original OFF peer");
            int writes = transport.FlagWrites;
            transport.SetFlags(1, 0x201);
            // Until the next observation, a frame uses copied proof rather than reading the actor
            Check(session.ReadMenuState().TeammateGodmodeValue == "OFF",
                "A menu frame synchronously observed a foreign flag change");
            await session.RefreshLocalObservationAsync();
            var foreign = session.ReadMenuState();
            Check(foreign.TeammateGodmodeValue == "UNKNOWN" && !foreign.CanTeammateGodmode,
                "Worker refresh presented a foreign ON word as an owned value");
            Check(transport.FlagWrites == writes, "Foreign proof refresh wrote a flag word");

            // An equal roster label cannot retain a replaced full actor identity
            var original = transport.ReadTeammateState(1);
            transport.SetPlayerState(1, original with {
                Identity = original.Identity with { Guid = "1111111111111111" }
            });
            await session.RefreshLocalObservationAsync();
            var replaced = session.ReadMenuState();
            Check(replaced.SelectedPlayerTarget is null && replaced.TeammateGodmodeValue == "UNKNOWN"
                && !replaced.CanTeammateGodmode,
                "Worker refresh retained authority for a same-name replacement actor");
            await session.ExecuteAsync(RetailBackendSessionTests.Toggle(1) with { ExpectedTarget = target });
            Check(transport.FlagWrites == writes && transport.TeammateBindCalls == 1,
                "A stale peer receipt wrote or transferred its adapter after replacement");
        } finally { await session.ShutdownAsync(); }
    }

    public static async Task HeldPeerRefreshKeepsTheGateFreeAndRetainsTransportUntilShutdownRetry() {
        var transport = new RecordingRetailTransport { LocalSlot = 0, FailLocalRead = false,
            OnLocalRead = null, RejectLocalFlagRestore = false };
        var session = new RetailBackendSession(4242, () => true, () => transport, () => false,
            observationStopTimeout: TimeSpan.FromMilliseconds(50));
        await session.ExecuteAsync(new(MenuActionKind.Connect));
        await session.ExecuteAsync(RetailBackendSessionTests.Select(1));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int held = 0;
        transport.OnFlagRead = () => {
            // Only the first bound read waits; the fixture storage lock remains available
            if (Interlocked.Increment(ref held) != 1) return;
            entered.TrySetResult();
            release.Task.GetAwaiter().GetResult();
        };
        Task refresh = session.RefreshLocalObservationAsync();
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            var frame = await Task.Run(session.ReadMenuState).WaitAsync(TimeSpan.FromSeconds(2));
            Check(frame.TeammateGodmodeValue == "UNKNOWN" && !frame.CanTeammateGodmode,
                "A held worker did not withdraw the copied peer capability");
            var stopped = await session.ShutdownAsync().WaitAsync(TimeSpan.FromSeconds(3));
            Check(!stopped.Stopped && !transport.Disposed && transport.DisposeCalls == 0,
                "Shutdown released a transport still used by the retained peer read");
            await session.ExecuteAsync(new(MenuActionKind.Connect)).WaitAsync(TimeSpan.FromSeconds(2));
            Check(transport.OpenCalls == 1, "Blocked cleanup opened a replacement connection");

            release.TrySetResult();
            await FinishRefreshAsync(refresh);
            // A completed failed stop is retried through the existing explicit admission reset
            session.AllowShutdownRetry();
            var retry = await session.ShutdownAsync().WaitAsync(TimeSpan.FromSeconds(3));
            Check(retry.Stopped && transport.Disposed && transport.DisposeCalls == 1
                && transport.FlagWrites == 0,
                "Cleanup retry did not release the original transport exactly once without writes");
        } finally {
            transport.OnFlagRead = null;
            release.TrySetResult();
            await FinishRefreshAsync(refresh);
            session.AllowShutdownRetry();
            await session.ShutdownAsync();
        }
    }

    private static async Task FinishRefreshAsync(Task refresh) {
        // Shutdown cancellation may stop the retained worker after its held read returns
        try { await refresh.WaitAsync(TimeSpan.FromSeconds(3)); }
        catch (OperationCanceledException) { }
    }

    private static void Check(bool condition, string message) => RetailBackendSessionTests.Check(condition, message);
}
