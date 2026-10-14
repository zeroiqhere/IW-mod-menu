using Apparition.Backend.Session;
using syndishanx_iw_tool.Overlay;
using static Session.Tests.RetailBackendSessionTests;

namespace Session.Tests;

// Real actions finish while their retail continuation is held across a new connection
internal static class RetailCompletionTests {
    public static async Task SuccessfulOldAmmoCompletionCannotOverwriteNewOwnerStatus() {
        await HeldAmmoCompletionPreservesFreshOwner(false);
    }
    public static async Task FailedOldAmmoCompletionCannotMarkTheNewOwnerUnknown() {
        await HeldAmmoCompletionPreservesFreshOwner(true);
    }
    private static async Task HeldAmmoCompletionPreservesFreshOwner(bool fail) {
        var scan = new TaskCompletionSource<IReadOnlyList<long>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var old = new RecordingRetailTransport { Scan = _ => scan.Task };
        var fresh = new RecordingRetailTransport(); var queue = new Queue<RecordingRetailTransport>([old, fresh]);
        var session = new RetailBackendSession(4242, () => true, () => queue.Dequeue(), () => false);
        await session.ExecuteAsync(new(MenuActionKind.Connect));
        var context = new HeldContinuationContext();
        var oldAction = context.Start(() => session.ExecuteOnWorkerAsync(new(MenuActionKind.ToggleLobbyAmmo)));
        scan.SetResult(fail ? [] : [0x140001000]);
        await context.WaitForPostAsync();
        Check(!oldAction.IsCompleted && old.AmmoWrites == (fail ? 0 : 1), "The real ammo action did not finish before its retail continuation");
        Check((await session.ShutdownAsync()).Restored && old.Disposed, "Original ammo owner was not cleaned up");
        await session.ExecuteAsync(new(MenuActionKind.Connect));
        await session.ExecuteAsync(new(MenuActionKind.ToggleLobbyAmmo));
        await context.DrainAsync(oldAction);
        var frame = session.ReadMenuState();
        Check(frame.LobbyAmmoValue == "ON" && frame.LobbyAmmoStatus == "On", "An old ammo completion changed the new owner");
        await session.ShutdownAsync();
    }
    public static async Task SuccessfulOldSelectionCannotReappearAfterShutdownAndReconnect() {
        var old = new RecordingRetailTransport(); var fresh = new RecordingRetailTransport();
        var queue = new Queue<RecordingRetailTransport>([old, fresh]);
        var session = new RetailBackendSession(4242, () => true, () => queue.Dequeue(), () => false);
        await session.ExecuteAsync(new(MenuActionKind.Connect)); await session.ExecuteAsync(Select(1)); await session.ExecuteAsync(Toggle(1));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var offEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int first = 0;
        old.OnFlagRead = () => {
            if (Interlocked.Exchange(ref first, 1) != 0) { offEntered.TrySetResult(); return; }
            entered.SetResult(); release.Task.GetAwaiter().GetResult();
        };
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var off = session.ExecuteAsync(Toggle(1));
        await offEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        // A harmless queued action confirms OFF has released the admission gate
        await session.ExecuteAsync(new(MenuActionKind.None)).WaitAsync(TimeSpan.FromSeconds(5));
        // Selection waits on the real teammate gate while OFF joins the blocked monitor
        var context = new HeldContinuationContext();
        var selection = context.Start(() => session.ExecuteOnWorkerAsync(Select(2)));
        old.OnFlagRead = null; release.SetResult();
        await context.WaitForPostAsync(); await off;
        Check(!selection.IsCompleted && old.Flags(1) == 0x200, "Selection was not held after the real monitor joined");
        await session.ShutdownAsync(); await session.ExecuteAsync(new(MenuActionKind.Connect));
        await context.DrainAsync(selection);
        Check(session.ReadMenuState().SelectedSlot == -1, "Old selection reappeared in a fresh connection");
        await session.ShutdownAsync();
    }
    public static async Task OldPersonalOffCompletionCannotOverwriteNewOwnerOnStatus() {
        var old = new RecordingRetailTransport(); var fresh = new RecordingRetailTransport();
        var queue = new Queue<RecordingRetailTransport>([old, fresh]);
        var session = new RetailBackendSession(4242, () => true, () => queue.Dequeue(), () => false);
        await session.ExecuteAsync(new(MenuActionKind.Connect));
        await session.ExecuteAsync(new(MenuActionKind.TogglePersonalGodmode));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int first = 0;
        old.OnLocalFlagRead = () => {
            if (Interlocked.Exchange(ref first, 1) != 0) return;
            entered.SetResult(); release.Task.GetAwaiter().GetResult();
        };
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var context = new HeldContinuationContext();
        var oldAction = context.Start(() => session.ExecuteOnWorkerAsync(new(MenuActionKind.TogglePersonalGodmode)));
        old.OnLocalFlagRead = null; release.SetResult();
        await context.WaitForPostAsync();
        Check(!oldAction.IsCompleted, "Personal OFF did not reach its held retail continuation");
        await session.ShutdownAsync(); await session.ExecuteAsync(new(MenuActionKind.Connect));
        await session.ExecuteAsync(new(MenuActionKind.TogglePersonalGodmode));
        await context.DrainAsync(oldAction);
        var frame = session.ReadMenuState();
        Check(frame.PersonalGodmodeValue == "ON" && frame.PersonalGodmodeStatus == "On",
            "Old personal completion changed the new owner status");
        await session.ShutdownAsync();
    }
    public static async Task APersonalFlagMonitorMustJoinBeforeItsTransportCanBeDisposed() {
        var transport = new RecordingRetailTransport(); var session = Create(transport);
        await session.ExecuteAsync(new(MenuActionKind.Connect)); await session.ExecuteAsync(new(MenuActionKind.TogglePersonalGodmode));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int first = 0;
        transport.OnLocalFlagRead = () => {
            if (Interlocked.Exchange(ref first, 1) != 0) return;
            entered.SetResult(); release.Task.GetAwaiter().GetResult();
        };
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var shutdown = session.ShutdownAsync();
        for (int index = 0; index < 1000; index++) {
            var frame = session.ReadMenuState();
            Check(frame.Busy && !frame.CanConnect && !frame.CanPersonalGodmode && !frame.CanLobbyAmmo, "Cleanup admitted a feature action");
        }
        Check(!shutdown.IsCompleted && transport.DisposeCalls == 0, "Transport was disposed while its worker was live");
        transport.OnLocalFlagRead = null; release.SetResult();
        Check((await shutdown.WaitAsync(TimeSpan.FromSeconds(5))).Stopped && transport.DisposeCalls == 1, "Joined worker retained its clean transport");
    }
    public static async Task AFailedAmmoWriteRequestsCleanupWithoutHoldingTheLifecycleGate() {
        var transport = new RecordingRetailTransport { RejectAmmoWrites = true }; var session = Create(transport);
        await session.ExecuteAsync(new(MenuActionKind.Connect));
        await session.ExecuteAsync(new(MenuActionKind.ToggleLobbyAmmo)).WaitAsync(TimeSpan.FromSeconds(5));
        Check(!session.ReadMenuState().Connected && transport.DisposeCalls == 1 && transport.AmmoWrites == 1,
            "Failed ammo write deadlocked or did not follow the actual cleanup path");
    }
}
