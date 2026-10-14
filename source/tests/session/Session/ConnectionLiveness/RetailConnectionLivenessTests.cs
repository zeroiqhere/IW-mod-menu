using Apparition.Backend.Session;
using syndishanx_iw_tool.Overlay;

namespace Session.Tests;

// Cached frame getters must not wait for the transport's native access lock
internal static class RetailConnectionLivenessTests {
    public static async Task CachedFrameReadsNeverQueryNativeConnectionLiveness() {
        var transport = new LockedRetailTransport();
        var session = Create(transport, withAdmission: true);
        await session.ExecuteAsync(new(MenuActionKind.Connect));
        await session.RefreshLocalObservationAsync();
        await session.ExecuteAsync(RetailBackendSessionTests.Select(1));
        try {
            int connectedReads = transport.ConnectedReads;
            int pidReads = transport.ProcessIdReads;
            for (int frame = 0; frame < 40; frame++) ReadCachedGetters(session);
            Check(transport.ConnectedReads == connectedReads && transport.ProcessIdReads == pidReads,
                "Cached menu or target getters queried native connection liveness");

            // A fresh wrong-process observation retires this owner even if later scalar values recover
            transport.Storage.ReportedPid = 4343;
            await session.RefreshLocalObservationAsync();
            Check(!session.ReadMenuState().Connected && session.ReadSelectedPlayerTarget() is null,
                "The retained polling worker failed to retire a wrong-process owner");
            transport.Storage.ReportedPid = 4242;
            await session.RefreshLocalObservationAsync();
            Check(!session.ReadMenuState().Connected && !session.ReadMenuState().CanTeammateGodmode,
                "Recovered process scalars revived a terminal original-owner failure");
        } finally { await session.ShutdownAsync(); }
    }

    public static async Task HeldNativeActorQueryDoesNotBlockCachedSessionGetters() {
        var transport = new LockedRetailTransport();
        var session = Create(transport, withAdmission: true);
        await session.ExecuteAsync(new(MenuActionKind.Connect));
        await session.RefreshLocalObservationAsync();
        await session.ExecuteAsync(RetailBackendSessionTests.Select(1));
        transport.HoldNextActorQuery();
        Task query = Task.Run(() => transport.ReadTeammateState(1));
        Task? frames = null;
        try {
            await transport.Entered.WaitAsync(TimeSpan.FromSeconds(3));
            // Actor storage and liveness are blocked by the exact same emulated native lock
            frames = Task.Run(() => ReadCachedGetters(session));
            await frames.WaitAsync(TimeSpan.FromSeconds(2));
            Check(!query.IsCompleted, "The native actor query was not held across the cached frame proof");
        } finally {
            transport.ReleaseQuery();
            await query.WaitAsync(TimeSpan.FromSeconds(3));
            if (frames is not null) await frames.WaitAsync(TimeSpan.FromSeconds(3));
            await session.ShutdownAsync();
        }
    }

    public static async Task DefaultRetainedPollingJoinsHeldLivenessBeforeTransportRelease() {
        var transport = new LockedRetailTransport();
        var session = Create(transport);
        await session.ExecuteAsync(new(MenuActionKind.Connect));
        // Neither a provider nor a selected peer is needed for the original owner's polling task
        transport.HoldNextLivenessQuery();
        Task poll = session.RefreshLocalObservationAsync();
        Task? frames = null;
        try {
            await transport.Entered.WaitAsync(TimeSpan.FromSeconds(3));
            frames = Task.Run(() => ReadCachedGetters(session, selected: false, admitted: false));
            await frames.WaitAsync(TimeSpan.FromSeconds(2));
            var stopped = await session.ShutdownAsync().WaitAsync(TimeSpan.FromSeconds(3));
            Check(!stopped.Stopped && !transport.Storage.Disposed && transport.Storage.DisposeCalls == 0,
                "Bounded shutdown released the original transport during its held liveness query");

            transport.ReleaseQuery();
            await FinishPollAsync(poll);
            session.AllowShutdownRetry();
            var retry = await session.ShutdownAsync().WaitAsync(TimeSpan.FromSeconds(3));
            Check(retry.Stopped && transport.Storage.DisposeCalls == 1 && transport.Storage.FlagWrites == 0,
                "Retry did not join and release the original polling owner without flag writes");
        } finally {
            transport.ReleaseQuery();
            await FinishPollAsync(poll);
            if (frames is not null) await frames.WaitAsync(TimeSpan.FromSeconds(3));
            session.AllowShutdownRetry();
            await session.ShutdownAsync();
        }
    }

    private static RetailBackendSession Create(LockedRetailTransport transport, bool withAdmission = false) =>
        new(4242, () => true, () => transport, () => false,
            observationProviderFactory: withAdmission ? _ => new LockedLocalObservationProvider(transport) : null,
            observationStopTimeout: TimeSpan.FromMilliseconds(50));

    private static void ReadCachedGetters(RetailBackendSession session, bool selected = true, bool admitted = true) {
        Check(session.ReadMenuState().Connected, "A current cached owner did not publish connected state");
        Check((session.ReadSelectedPlayerTarget() is not null) == selected,
            "The cached selected target changed while its native query was held");
        _ = session.ReadPlayerSelectionScope();
        var local = session.ReadLocalObservation();
        var admission = session.ReadLocalMenuAdmission();
        Check((admission is not null) == admitted && (!admitted || local.Identity is not null),
            "The cached observation/admission proof did not exercise its owned receipt path");
    }

    private static async Task FinishPollAsync(Task poll) {
        // Source cancellation remains retained until the native query has returned
        try { await poll.WaitAsync(TimeSpan.FromSeconds(3)); }
        catch (OperationCanceledException) { }
    }

    private static void Check(bool condition, string message) => RetailBackendSessionTests.Check(condition, message);
}
