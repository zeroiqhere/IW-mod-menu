using Apparition.Backend.Session;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Session.Tests;

// Exercise observation admission and lifetime through the actual retail session owner
internal static class LocalObservationTests {
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(3);
    private static readonly MenuAction Connect = new(MenuActionKind.Connect);
    private static void Check(bool value, string message) => RetailBackendSessionTests.Check(value, message);

    private static RetailBackendSession Create(RecordingRetailTransport transport,
        Func<syndishanx_iw_tool.GameSession.IRetailSessionTransport, ILocalPlayerObservationProvider?>? factory = null,
        TimeSpan? timeout = null) => new(4242, () => true, () => transport, () => false, factory, timeout);

    public static async Task UnconfiguredSessionNeverInfersFactsFromConnectedHealthOrRoster() {
        var transport = new RecordingRetailTransport();
        IBackendSession session = Create(transport);
        await session.ExecuteAsync(Connect);
        int actorReads = transport.LocalReadCalls;
        int actorBindings = transport.LocalBindCalls;
        int processQueries = 0;
        transport.OnConnected = () => Interlocked.Increment(ref processQueries);
        transport.OnHealthRead = () => throw new Exception("Health cannot authorize local observation");
        await session.RefreshLocalObservationAsync();
        Check(processQueries > 0, "Default polling skipped original-process liveness without a local provider");
        Check(transport.LocalReadCalls == actorReads && transport.LocalBindCalls == actorBindings,
            "Unconfigured polling queried or rebound the personal actor source");
        transport.OnConnected = null;
        var actual = session.ReadLocalObservation();
        Check(actual.Life == LocalPlayerLife.Unknown && actual.Identity is null && actual.HasMenu is null,
            "The unconfigured production binding invented player facts");
        Check(session.ReadMenuState().Connected, "Unknown observation must not disable existing menu state");
        Check((await session.ShutdownAsync()).Stopped && transport.DisposeCalls == 1, "Default observation retained a resource");
    }

    public static async Task WorkerReadPublishesOwnedFactsAndPreservesPartialUnknown() {
        var transport = new RecordingRetailTransport();
        var provider = new RecordingObservationProvider();
        var session = Create(transport, _ => provider);
        await session.ExecuteAsync(Connect);
        await session.RefreshLocalObservationAsync();
        Check(provider.Reads == 1 && session.ReadLocalObservation() == RecordingObservationProvider.Known(),
            "The actual session did not publish the supplied owned observation");
        var partial = RecordingObservationProvider.Known() with { Life = LocalPlayerLife.Unknown, HasMenu = null };
        provider.Read = _ => Task.FromResult<LocalPlayerObservation?>(partial);
        await session.RefreshLocalObservationAsync();
        Check(session.ReadLocalObservation() == partial, "A valid partial unknown observation was coerced into invented facts");
        Check(transport.HealthWrites == 0 && transport.FlagWrites == 0 && transport.AmmoWrites == 0, "Observation wrote a feature");
        await session.ShutdownAsync();
    }

    public static async Task NullMalformedWrongProcessAndProviderFailureRevokePriorFacts() {
        var transport = new RecordingRetailTransport();
        var provider = new RecordingObservationProvider();
        var session = Create(transport, _ => provider);
        await session.ExecuteAsync(Connect);
        foreach (int failure in Enumerable.Range(0, 5)) {
            provider.Read = _ => Task.FromResult<LocalPlayerObservation?>(RecordingObservationProvider.Known());
            await session.RefreshLocalObservationAsync();
            Check(session.ReadLocalObservation().Life == LocalPlayerLife.Alive, "The fixture must first publish a known observation");
            provider.Read = failure switch {
                0 => _ => Task.FromResult<LocalPlayerObservation?>(null),
                1 => _ => Task.FromResult<LocalPlayerObservation?>(RecordingObservationProvider.Known(99)),
                2 => _ => Task.FromResult<LocalPlayerObservation?>(RecordingObservationProvider.Known() with { Life = (LocalPlayerLife)99 }),
                3 => _ => Task.FromResult<LocalPlayerObservation?>(RecordingObservationProvider.Known() with {
                    Identity = RecordingObservationProvider.Known().Identity! with { ServerCount = 1 }
                }),
                _ => _ => throw new IOException("Owned read failed")
            };
            await session.RefreshLocalObservationAsync();
            Check(session.ReadLocalObservation().Life == LocalPlayerLife.Unknown && session.ReadLocalObservation().Identity is null,
                "A failed read retained earlier positive facts");
        }
        await session.ShutdownAsync();
    }

    public static async Task ConcurrentRefreshCoalescesOneReadWhileCachedViewsRemainNonblocking() {
        var entered = RecordingObservationProvider.Completion();
        var release = RecordingObservationProvider.Completion<LocalPlayerObservation?>();
        var provider = new RecordingObservationProvider { Read = _ => { entered.TrySetResult(); return release.Task; } };
        var transport = new RecordingRetailTransport();
        var session = Create(transport, _ => provider);
        await session.ExecuteAsync(Connect);
        var first = session.RefreshLocalObservationAsync();
        try {
            await entered.Task.WaitAsync(Deadline);
            var second = session.RefreshLocalObservationAsync();
            var views = Task.Run(() => { _ = session.ReadMenuState(); _ = session.ReadLocalObservation(); });
            await views.WaitAsync(Deadline);
            Check(provider.Reads == 1 && transport.DisposeCalls == 0, "A shared read was duplicated or released early");
            release.TrySetResult(RecordingObservationProvider.Known());
            await Task.WhenAll(first, second).WaitAsync(Deadline);
            Check(session.ReadLocalObservation().Life == LocalPlayerLife.Alive, "Coalesced completion lost its owner");
        } finally { release.TrySetResult(null); await session.ShutdownAsync(); }
    }

    public static async Task ClosingRevokesFactsAndJoinsCooperativeReadBeforeDisposal() {
        var transport = new RecordingRetailTransport();
        var provider = new RecordingObservationProvider();
        var session = Create(transport, _ => provider);
        await session.ExecuteAsync(Connect);
        await session.RefreshLocalObservationAsync();
        var entered = RecordingObservationProvider.Completion();
        provider.Read = async token => { entered.TrySetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, token); return null; };
        var read = session.RefreshLocalObservationAsync();
        await entered.Task.WaitAsync(Deadline);
        var closing = session.ShutdownAsync();
        Check(session.ReadLocalObservation().Life == LocalPlayerLife.Unknown, "Closing retained known player facts");
        var stopped = await closing.WaitAsync(Deadline);
        await read.WaitAsync(Deadline);
        Check(stopped.Stopped && transport.DisposeCalls == 1, "The cooperative read did not end before release");
    }

    public static async Task UncooperativeReadTimesOutWithoutDisposalAndOriginalCleanupCanRetry() {
        var entered = RecordingObservationProvider.Completion();
        var release = RecordingObservationProvider.Completion<LocalPlayerObservation?>();
        var provider = new RecordingObservationProvider { Read = _ => { entered.TrySetResult(); return release.Task; } };
        var transport = new RecordingRetailTransport();
        var session = Create(transport, _ => provider, TimeSpan.FromMilliseconds(30));
        await session.ExecuteAsync(Connect);
        var read = session.RefreshLocalObservationAsync();
        try {
            await entered.Task.WaitAsync(Deadline);
            var failed = await session.ShutdownAsync().WaitAsync(Deadline);
            Check(!failed.Stopped && transport.DisposeCalls == 0, "A live source was disposed after the bounded stop expired");
            Check(!session.ReadMenuState().CanConnect && session.ReadLocalObservation().Life == LocalPlayerLife.Unknown,
                "A timed-out owner remained eligible or allowed a replacement");
            await session.ExecuteAsync(Connect);
            Check(transport.OpenCalls == 1, "A live retained source allowed reconnect");
            release.TrySetResult(RecordingObservationProvider.Known());
            await read.WaitAsync(Deadline);
            Check(session.ReadLocalObservation().Life == LocalPlayerLife.Unknown, "A late completion resurrected eligibility");
            session.AllowShutdownRetry();
            Check((await session.ShutdownAsync()).Stopped && transport.DisposeCalls == 1, "Original cleanup could not retry after the read ended");
        } finally { release.TrySetResult(null); session.AllowShutdownRetry(); await session.ShutdownAsync(); }
    }

    public static async Task CancellingOneCallerCannotCancelAnotherCallerOrTheOwnedRead() {
        var entered = RecordingObservationProvider.Completion();
        var release = RecordingObservationProvider.Completion<LocalPlayerObservation?>();
        CancellationToken sourceToken = default;
        var provider = new RecordingObservationProvider { Read = token => { sourceToken = token; entered.TrySetResult(); return release.Task; } };
        var transport = new RecordingRetailTransport();
        var session = Create(transport, _ => provider);
        await session.ExecuteAsync(Connect);
        using var caller = new CancellationTokenSource();
        var first = session.RefreshLocalObservationAsync(caller.Token);
        try {
            await entered.Task.WaitAsync(Deadline);
            var second = session.RefreshLocalObservationAsync();
            caller.Cancel();
            try { await first; throw new Exception("The cancelled caller completed normally"); }
            catch (OperationCanceledException) { }
            Check(!sourceToken.IsCancellationRequested && provider.Reads == 1, "A caller cancelled shared source ownership");
            release.TrySetResult(RecordingObservationProvider.Known());
            await second.WaitAsync(Deadline);
            Check(session.ReadLocalObservation().Life == LocalPlayerLife.Alive, "The surviving caller lost its read");
        } finally { release.TrySetResult(null); await session.ShutdownAsync(); }
    }

    public static async Task ShutdownRetainsSourceUntilCancellationCallbacksHaveAlsoEnded() {
        var entered = RecordingObservationProvider.Completion();
        var callbackEntered = RecordingObservationProvider.Completion();
        var releaseCallback = RecordingObservationProvider.Completion();
        var provider = new RecordingObservationProvider { Read = async token => {
            _ = token.Register(() => {
                callbackEntered.TrySetResult();
                releaseCallback.Task.GetAwaiter().GetResult();
            });
            entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return null;
        } };
        var transport = new RecordingRetailTransport();
        var session = Create(transport, _ => provider, TimeSpan.FromMilliseconds(30));
        await session.ExecuteAsync(Connect);
        var read = session.RefreshLocalObservationAsync();
        try {
            await entered.Task.WaitAsync(Deadline);
            var shutdown = session.ShutdownAsync();
            await callbackEntered.Task.WaitAsync(Deadline);
            var failed = await shutdown.WaitAsync(Deadline);
            Check(!failed.Stopped && transport.DisposeCalls == 0, "A live cancellation callback lost its source");
            releaseCallback.TrySetResult();
            await read.WaitAsync(Deadline);
            session.AllowShutdownRetry();
            Check((await session.ShutdownAsync()).Stopped && transport.DisposeCalls == 1,
                "Callback completion did not permit original cleanup retry");
        } finally { releaseCallback.TrySetResult(); session.AllowShutdownRetry(); await session.ShutdownAsync(); }
    }
}
