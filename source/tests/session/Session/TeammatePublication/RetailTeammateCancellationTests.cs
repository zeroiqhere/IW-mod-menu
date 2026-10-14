using System.Reflection;
using Apparition.Backend.Session;
using syndishanx_iw_tool.Overlay;

namespace Session.Tests;

// Owner cancellation revokes a retained observation without faulting the normal polling loop
internal static class RetailTeammateCancellationTests {
    public static async Task ShutdownCancelsAQueuedPeerRefreshWithoutFaultingTheDefaultPollingTask() {
        var transport = new RecordingRetailTransport();
        var session = RetailBackendSessionTests.Create(transport);
        Task? action = null;
        Task? polling = null;
        Task<syndishanx_iw_tool.SessionLifecycle.SessionShutdownResult>? shutdown = null;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Exception? pollingFailure = null;
        try {
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            await session.ExecuteAsync(RetailBackendSessionTests.Select(1));
            Check(session.ReadSelectedPlayerTarget()?.Identity.Slot == 1,
                "The original peer selection must be valid before the held action");
            var owner = ReadConnection(session);
            var peerGate = ReadPeerGate(owner.Teammates);
            transport.OnFlagRead = () => {
                // The action owns its actual service gate before its bound preflight reaches this read
                transport.OnFlagRead = null;
                entered.TrySetResult();
                release.Task.GetAwaiter().GetResult();
            };
            action = session.ExecuteAsync(RetailBackendSessionTests.Toggle(1));
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Check(peerGate.CurrentCount == 0 && !action.IsCompleted,
                "The actual admitted teammate action did not hold its service gate");

            // This is the production retained polling entry, with no caller cancellation token
            polling = session.RefreshLocalObservationAsync();
            await WaitForQueuedRefreshAsync(peerGate);
            Check(!polling.IsCompleted && owner.ObservationRead is { IsCompleted: false },
                "The retained worker must be waiting behind the original action");
            shutdown = session.ShutdownAsync();
            try { await polling.WaitAsync(TimeSpan.FromSeconds(3)); }
            catch (Exception error) { pollingFailure = error; }
            Check(owner.ObservationCancellation.IsCancellationRequested,
                "Only the original owner's shutdown must cancel this observation");
            var closing = session.ReadMenuState();
            Check(closing.TeammateGodmodeValue == "UNKNOWN" && !closing.CanTeammateGodmode
                && closing.SelectedPlayerTarget is null,
                "A closing frame must not retain cached peer authority");
        } finally {
            // Every failed assertion releases and joins the actual action and original cleanup
            transport.OnFlagRead = null;
            release.TrySetResult();
            if (action is not null) await action.WaitAsync(TimeSpan.FromSeconds(3));
            if (polling is not null) {
                try { await polling.WaitAsync(TimeSpan.FromSeconds(3)); }
                catch (OperationCanceledException) { }
            }
            var result = await (shutdown ?? session.ShutdownAsync()).WaitAsync(TimeSpan.FromSeconds(3));
            Check(result.Stopped && result.Restored && transport.DisposeCalls == 1,
                "Joined original work must release its transport exactly once after confirmed cleanup");
        }
        Check(transport.FlagWrites == 0,
            "The held enable must not enter its flag write after shutdown disarms admission");
        Check(pollingFailure is null,
            "Owner shutdown must complete default polling normally, not throw "
                + pollingFailure?.GetType().Name);
    }

    private static RetailConnection ReadConnection(RetailBackendSession session) =>
        typeof(RetailBackendSession).GetField("connection", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(session) as RetailConnection
            ?? throw new InvalidOperationException("The original connection field was not available");

    private static SemaphoreSlim ReadPeerGate(TeammateGodmodeSession peers) =>
        typeof(TeammateGodmodeSession).GetField("gate", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(peers) as SemaphoreSlim
            ?? throw new InvalidOperationException("The original teammate gate was not available");

    private static async Task WaitForQueuedRefreshAsync(SemaphoreSlim gate) {
        // Readonly test inspection proves the real waiter exists; elapsed time never stands in for it
        var head = typeof(SemaphoreSlim).GetField("m_asyncHead", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("The runtime's asynchronous semaphore waiter field changed");
        using var bounded = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (head.GetValue(gate) is null) await Task.Delay(1, bounded.Token);
    }

    private static void Check(bool condition, string message) => RetailBackendSessionTests.Check(condition, message);
}
