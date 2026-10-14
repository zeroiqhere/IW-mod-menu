using Apparition.Backend.Session;
using syndishanx_iw_tool.Overlay;

namespace Session.Tests;

// Exercise the real session and its feature owners without opening a process
internal static class RetailBackendSessionTests {
    internal static RetailBackendSession Create(RecordingRetailTransport transport, Func<bool>? foreground = null,
        Func<bool>? otherTool = null) => new(4242, foreground ?? (() => true), () => transport, otherTool ?? (() => false));
    internal static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static readonly MenuAction Connect = new(MenuActionKind.Connect);
    internal static MenuAction Select(int slot) => new(MenuActionKind.SelectPlayer, Slot: slot, ExpectedName: "Friend" + slot);
    internal static MenuAction Toggle(int slot) => new(MenuActionKind.ToggleTeammateGodmode, Slot: slot, ExpectedName: "Friend" + slot);

    public static async Task ConnectPublishesOneCompleteOwnerAndCleanupDisposesIt() {
        var transport = new RecordingRetailTransport(); var session = Create(transport);
        Check(session.ReadMenuState().CanConnect, "Disconnected session cannot connect");
        await session.ExecuteAsync(Connect);
        Check(session.ReadMenuState().Connected && transport.OpenCalls == 1, "A valid owner was not published");
        await session.ExecuteAsync(Connect);
        Check(transport.OpenCalls == 1 && transport.AmmoWrites == 0 && transport.HealthWrites == 0, "Connect wrote features or reopened");
        var result = await session.ShutdownAsync();
        Check(result.Stopped && result.Restored && transport.DisposeCalls == 1 && !session.ReadMenuState().Connected, "Cleanup lost its transport");
    }
    public static async Task ForegroundAndOtherToolGuardsRejectBeforeOpening() {
        var transport = new RecordingRetailTransport(); var session = Create(transport, () => false);
        await session.ExecuteAsync(Connect); Check(transport.OpenCalls == 0, "Background connect opened a transport");
        session = Create(transport, otherTool: () => true);
        try { await session.ExecuteAsync(Connect); throw new Exception("Conflict accepted"); } catch (InvalidOperationException) { }
        Check(transport.OpenCalls == 0, "Conflicting tool opened a transport");
        await session.ShutdownAsync();
    }
    public static async Task FailedOpenAndWrongPidDisposeBeforeAnotherConnect() {
        foreach (bool failOpen in new[] { true, false }) {
            var transport = new RecordingRetailTransport { OpenResult = !failOpen, ReportedPid = failOpen ? 4242 : 99 };
            var session = Create(transport);
            try { await session.ExecuteAsync(Connect); throw new Exception("Invalid process accepted"); } catch (InvalidOperationException) { }
            Check(transport.DisposeCalls == 1 && session.ReadMenuState().CanConnect, "Rejected candidate retained a clean handle");
            await session.ShutdownAsync();
        }
    }
    public static async Task FailedCandidateDisposalRetainsOwnershipUntilCleanupRetry() {
        var transport = new RecordingRetailTransport { OpenResult = false, DisposeThrows = true }; var session = Create(transport);
        try { await session.ExecuteAsync(Connect); throw new Exception("Invalid process accepted"); } catch (InvalidOperationException) { }
        Check(!session.ReadMenuState().CanConnect, "Unreleased candidate allowed another connection");
        await session.ExecuteAsync(Connect); Check(transport.OpenCalls == 1, "Blocked candidate reopened");
        var failed = await session.ShutdownAsync(); Check(!failed.Stopped, "Disposal failure was treated as stopped");
        transport.DisposeThrows = false; session.AllowShutdownRetry();
        Check((await session.ShutdownAsync()).Stopped && transport.Disposed, "Retry did not release retained candidate");
    }
    public static async Task ConnectedReadCanOverlapActualShutdownWithoutDereferencingClearedFields() {
        var transport = new RecordingRetailTransport();
        var session = Create(transport);
        await session.ExecuteAsync(Connect);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        transport.OnConnected = () => {
            transport.OnConnected = null;
            entered.TrySetResult();
            release.Task.GetAwaiter().GetResult();
        };
        Task read = session.RefreshLocalObservationAsync();
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            // Shutdown is requested outside the retained worker so it never joins itself
            var closing = session.ShutdownAsync();
            var frame = session.ReadMenuState();
            Check(!frame.Connected && !frame.CanConnect && !transport.Disposed,
                "Closing published a connected frame or released a still-running source");
            release.TrySetResult();
            await read.WaitAsync(TimeSpan.FromSeconds(3));
            var stopped = await closing.WaitAsync(TimeSpan.FromSeconds(3));
            Check(stopped.Stopped && transport.Disposed && !session.ReadMenuState().Connected,
                "The retired worker survived cleanup or published stale connected state");
        } finally {
            transport.OnConnected = null;
            release.TrySetResult();
            await read.WaitAsync(TimeSpan.FromSeconds(3));
            session.AllowShutdownRetry();
            await session.ShutdownAsync();
        }
    }
    public static async Task SelectedPlayersKeepIndependentRealFlagOwners() {
        var transport = new RecordingRetailTransport(); var session = Create(transport); await session.ExecuteAsync(Connect);
        await session.ExecuteAsync(Select(1)); Check(transport.FlagWrites == 0, "Read-only selection wrote a flag");
        await session.ExecuteAsync(Toggle(1)); await session.ExecuteAsync(Select(2)); await session.ExecuteAsync(Toggle(2));
        Check(transport.Flags(1) == 0x201 && transport.Flags(2) == 0x201, "Switching borrowed or cleared a player owner");
        await session.ExecuteAsync(Select(1)); await session.ExecuteAsync(Toggle(1));
        Check(transport.Flags(1) == 0x200 && transport.Flags(2) == 0x201 && transport.HealthWrites == 0, "Individual OFF changed another target");
        Check((await session.ShutdownAsync()).Restored && transport.Flags(2) == 0x200, "Cleanup did not restore every retained player");
    }
    public static async Task FailedPlayerRestoreKeepsTransportAndRejectsConnectionUntilRetry() {
        var transport = new RecordingRetailTransport(); var session = Create(transport); await session.ExecuteAsync(Connect);
        await session.ExecuteAsync(Select(1)); await session.ExecuteAsync(Toggle(1)); transport.RejectFlagRestore = true;
        var result = await session.ShutdownAsync();
        Check(!result.Stopped && !result.Restored && transport.DisposeCalls == 0 && !session.ReadMenuState().CanConnect, "Unrestored flags lost ownership");
        await session.ExecuteAsync(Connect); Check(transport.OpenCalls == 1, "Cleanup failure allowed reconnect");
        transport.RejectFlagRestore = false; session.AllowShutdownRetry();
        Check((await session.ShutdownAsync()).Restored && transport.Flags(1) == 0x200 && transport.DisposeCalls == 1, "Retry did not restore the original player");
    }
    public static async Task AClosingSessionRejectsConnectWhileAnOpenCandidateIsStillRunning() {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var transport = new RecordingRetailTransport { OnOpen = () => { entered.SetResult(); release.Task.GetAwaiter().GetResult(); } };
        var session = Create(transport); var opening = session.ExecuteAsync(Connect); await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var shutdown = session.ShutdownAsync(); var second = session.ExecuteAsync(Connect); release.SetResult();
        await Task.WhenAll(opening, second, shutdown).WaitAsync(TimeSpan.FromSeconds(5));
        Check(transport.OpenCalls == 1 && transport.DisposeCalls == 1 && !session.ReadMenuState().Connected, "Closing reopened or published a candidate");
    }
    public static async Task AConnectQueuedBeforeShutdownCannotOpenAfterTheFinalClose() {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var transport = new RecordingRetailTransport { OnOpen = () => { entered.TrySetResult(); release.Task.GetAwaiter().GetResult(); } };
        var session = Create(transport);
        var opening = session.ExecuteAsync(Connect); await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var queued = session.ExecuteAsync(Connect);
        var shutdown = session.ShutdownAsync(); release.SetResult();
        await Task.WhenAll(opening, queued, shutdown).WaitAsync(TimeSpan.FromSeconds(5));
        Check(transport.OpenCalls == 1 && transport.DisposeCalls == 1 && !session.ReadMenuState().Connected,
            "A retired Connect recreated resources after shutdown");
    }
    public static async Task AClosedProcessIsRejectedBeforeAnyFeatureWrite() {
        var transport = new RecordingRetailTransport();
        transport.OnOpen = () => transport.Disposed = true;
        var session = Create(transport);
        try { await session.ExecuteAsync(Connect); throw new Exception("Closed process accepted"); } catch (InvalidOperationException) { }
        Check(!session.ReadMenuState().Connected && transport.DisposeCalls == 1
            && transport.HealthWrites == 0 && transport.FlagWrites == 0 && transport.AmmoWrites == 0,
            "A closed candidate published resources or changed feature state");
        await session.ShutdownAsync();
    }
    public static async Task ConnectedOwnerDisposalFailureBlocksReconnectAndAllowsOriginalCleanupRetry() {
        var transport = new RecordingRetailTransport(); var session = Create(transport);
        await session.ExecuteAsync(Connect); transport.DisposeThrows = true;
        var failed = await session.ShutdownAsync();
        Check(!failed.Stopped && failed.Restored && !transport.Disposed && !session.ReadMenuState().CanConnect,
            "Unreleased connected transport was treated as a clean disconnect");
        await session.ExecuteAsync(Connect); Check(transport.OpenCalls == 1, "Unreleased owner was replaced");
        transport.DisposeThrows = false; session.AllowShutdownRetry();
        Check((await session.ShutdownAsync()).Stopped && transport.DisposeCalls == 2,
            "Retry did not release the original connected transport");
    }
    public static async Task FreshConnectionClearsFailureMetadataFromAnEarlierOwner() {
        var old = new RecordingRetailTransport { Health = 1, Scan = _ => Task.FromResult<IReadOnlyList<long>>([]) };
        var fresh = new RecordingRetailTransport(); var candidates = new Queue<RecordingRetailTransport>([old, fresh]);
        var session = new RetailBackendSession(4242, () => true, () => candidates.Dequeue(), () => false);
        await session.ExecuteAsync(Connect); await session.ExecuteAsync(new(MenuActionKind.TogglePersonalGodmode));
        await session.ExecuteAsync(new(MenuActionKind.ToggleLobbyAmmo));
        Check(session.ReadMenuState().PersonalGodmodeValue == "UNKNOWN", "Failed personal operation was hidden");
        await session.ShutdownAsync(); await session.ExecuteAsync(Connect);
        var frame = session.ReadMenuState();
        Check(frame.PersonalGodmodeValue == "OFF" && frame.LobbyAmmoValue == "OFF", "Old failure poisoned a fresh owner");
        await session.ShutdownAsync();
    }
}
