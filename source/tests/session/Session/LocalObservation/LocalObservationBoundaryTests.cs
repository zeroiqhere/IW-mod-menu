using Apparition.Backend.Session;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Session.Tests;

// Factories, queued requests and cancellation are covered without a game process
internal static class LocalObservationBoundaryTests {
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(3);
    private static readonly MenuAction Connect = new(MenuActionKind.Connect);
    private static void Check(bool value, string message) => RetailBackendSessionTests.Check(value, message);

    public static async Task ThrowingOrNullProviderFactoryStaysUnknownAndCandidateCleanupKeepsItsOriginalOwner() {
        foreach (bool missing in new[] { false, true }) {
            var transport = new RecordingRetailTransport();
            var session = new RetailBackendSession(4242, () => true, () => transport, () => false,
                _ => missing ? null : throw new IOException("Owned factory failed"));
            await session.ExecuteAsync(Connect);
            await session.RefreshLocalObservationAsync();
            Check(session.ReadLocalObservation().Life == LocalPlayerLife.Unknown && session.ReadMenuState().Connected,
                "An unavailable read-only factory invented facts or closed unrelated feature ownership");
            transport.DisposeThrows = true;
            var failed = await session.ShutdownAsync();
            Check(!failed.Stopped && !transport.Disposed && !session.ReadMenuState().CanConnect,
                "Candidate cleanup failure released its original owner");
            transport.DisposeThrows = false;
            session.AllowShutdownRetry();
            Check((await session.ShutdownAsync()).Stopped && transport.Disposed, "Original candidate cleanup could not retry");
        }
    }

    public static async Task BlockingFactoryCannotHoldTheLifecycleGateOrLoseItsLiveSource() {
        var entered = RecordingObservationProvider.Completion();
        var release = RecordingObservationProvider.Completion();
        var provider = new RecordingObservationProvider();
        var transport = new RecordingRetailTransport();
        var session = new RetailBackendSession(4242, () => true, () => transport, () => false, _ => {
            entered.TrySetResult();
            release.Task.GetAwaiter().GetResult();
            return provider;
        }, TimeSpan.FromMilliseconds(30));
        await session.ExecuteAsync(Connect);
        var read = session.RefreshLocalObservationAsync();
        try {
            await entered.Task.WaitAsync(Deadline);
            var failed = await session.ShutdownAsync().WaitAsync(Deadline);
            Check(!failed.Stopped && transport.DisposeCalls == 0, "A blocked factory held the gate or lost its source");
            release.TrySetResult();
            await read.WaitAsync(Deadline);
            Check(provider.Reads == 0, "A cancelled factory completion started a new source read");
            session.AllowShutdownRetry();
            Check((await session.ShutdownAsync()).Stopped, "Factory cleanup did not retry");
        } finally { release.TrySetResult(); session.AllowShutdownRetry(); await session.ShutdownAsync(); }
    }

    public static async Task ObservationQueuedDuringOpenCannotStartAfterShutdownAndReconnect() {
        var entered = RecordingObservationProvider.Completion();
        var release = RecordingObservationProvider.Completion();
        var oldTransport = new RecordingRetailTransport { OnOpen = () => { entered.TrySetResult(); release.Task.GetAwaiter().GetResult(); } };
        var nextTransport = new RecordingRetailTransport();
        var oldProvider = new RecordingObservationProvider();
        var nextProvider = new RecordingObservationProvider();
        int creations = 0;
        var session = new RetailBackendSession(4242, () => true,
            () => Interlocked.Increment(ref creations) == 1 ? oldTransport : nextTransport, () => false,
            transport => ReferenceEquals(transport, oldTransport) ? oldProvider : nextProvider);
        var opening = session.ExecuteAsync(Connect);
        try {
            await entered.Task.WaitAsync(Deadline);
            var queued = session.RefreshLocalObservationAsync();
            var shutdown = session.ShutdownAsync();
            release.TrySetResult();
            await Task.WhenAll(opening, queued, shutdown).WaitAsync(Deadline);
            await session.ExecuteAsync(Connect);
            Check(oldProvider.Reads == 0 && nextProvider.Reads == 0,
                "A queued observation migrated into a replacement connection");
            Check(session.ReadLocalObservation().Life == LocalPlayerLife.Unknown, "A new owner inherited stale facts");
            await session.RefreshLocalObservationAsync();
            Check(nextProvider.Reads == 1 && session.ReadLocalObservation().Life == LocalPlayerLife.Alive,
                "A fresh explicit observation could not use the new owner");
        } finally { release.TrySetResult(); await session.ShutdownAsync(); }
    }

    public static async Task CompletedOldReadCannotResurrectAfterShutdownAndReconnect() {
        var oldTransport = new RecordingRetailTransport();
        var nextTransport = new RecordingRetailTransport();
        var oldProvider = new RecordingObservationProvider();
        var nextProvider = new RecordingObservationProvider { Read = _ => Task.FromResult<LocalPlayerObservation?>(
            RecordingObservationProvider.Known(life: LocalPlayerLife.Dead)) };
        int creations = 0;
        var session = new RetailBackendSession(4242, () => true,
            () => Interlocked.Increment(ref creations) == 1 ? oldTransport : nextTransport, () => false,
            transport => ReferenceEquals(transport, oldTransport) ? oldProvider : nextProvider);
        await session.ExecuteAsync(Connect);
        await session.RefreshLocalObservationAsync();
        Check(session.ReadLocalObservation().Life == LocalPlayerLife.Alive, "Old fixture did not publish its own result");
        await session.ShutdownAsync();
        await session.ExecuteAsync(Connect);
        Check(session.ReadLocalObservation().Life == LocalPlayerLife.Unknown, "Reconnect retained an old local binding");
        await session.RefreshLocalObservationAsync();
        Check(session.ReadLocalObservation().Life == LocalPlayerLife.Dead && oldTransport.DisposeCalls == 1,
            "Replacement facts were confused with the retired owner");
        await session.ShutdownAsync();
    }

    public static Task ObservationShapeRejectsUnboundPositiveFactsAndPreservesBoundUnknowns() {
        foreach (Func<LocalPlayerObservation> create in new Func<LocalPlayerObservation>[] {
            () => new(4242, null, LocalPlayerLife.Alive, null, null, "Unbound"),
            () => new(4242, null, LocalPlayerLife.Unknown, true, null, "Unbound permission"),
            () => new(4242, null, LocalPlayerLife.Unknown, null, false, "Unbound controls"),
            () => new(4242, RecordingObservationProvider.Known().Identity! with { ServerCount = 1 },
                LocalPlayerLife.Alive, true, false, "Slot outside server records"),
            () => new(4242, RecordingObservationProvider.Known().Identity, (LocalPlayerLife)99, null, null, "Invalid life"),
            () => new(4242, RecordingObservationProvider.Known().Identity, LocalPlayerLife.Unknown, null, null, new string('x', 501))
        }) {
            try { _ = create(); throw new Exception("Malformed observation was accepted"); }
            catch (ArgumentException) { }
        }
        var knownIdentity = RecordingObservationProvider.Known().Identity;
        var partial = new LocalPlayerObservation(4242, knownIdentity, LocalPlayerLife.Unknown, null, true, "Unknown life");
        Check(partial.Identity == knownIdentity && partial.Life == LocalPlayerLife.Unknown && partial.HasMenu is null,
            "Partial unknown facts were replaced or inferred");
        return Task.CompletedTask;
    }
}
