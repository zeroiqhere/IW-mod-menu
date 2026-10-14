using Apparition.Backend.Controller;
using Apparition.Backend.Ipc;
using Apparition.Backend.OpenControls;
using Apparition.Backend.Preferences;
using Backend.Tests;
using syndishanx_iw_tool.Overlay;
using static Session.Tests.RetailBackendSessionTests;

namespace Session.Tests;

// The real controller and session share only recorded bytes, never a game process
internal static class RetailSessionRecoveryControllerTests {
    public static async Task IncompleteAmmoCleanupOpensActorlessRecoveryAndExitRetriesTheOriginalOwner() {
        await using var rig = new RecoveryRig();
        await rig.OpenAsync();
        rig.Features.RejectAmmoWrites = true;
        rig.Features.DisposeThrows = true;
        await rig.ToggleAmmoAsync();
        var state = rig.Session.ReadMenuState();
        var frame = rig.Controller.Snapshot();
        Check(state.NeedsSessionRecovery && state.Connected && rig.Session.ReadLocalMenuAdmission() is null,
            "The retained cleanup failure did not publish explicit recovery without actor admission");
        Check(frame.IsOpen && frame.IsRootMenu && frame.LocalMenuEligibility is null
            && frame.Notice.Contains(state.LobbyAmmoStatus, StringComparison.Ordinal),
            "Automatic cleanup closed the admitted visit and made its retry guidance unreachable: " + $"Open={frame.IsOpen}, root={frame.IsRootMenu}, notice={frame.Notice}");
        Check(frame.MenuOptionCallback is null && frame.MenuUseContinuation is null && !rig.Controller.Busy,
            "The retired ammo callback retained its input lock or actor-owned continuation");
        int writes = rig.Features.AmmoWrites;
        rig.Send(OverlayInput.Toggle);
        Check(!rig.Controller.Snapshot().IsOpen, "Administrative recovery could not be closed");
        rig.Send(OverlayInput.Toggle);
        Check(rig.Controller.Snapshot().IsOpen && rig.Controller.Snapshot().IsRootMenu,
            "Recovery could not reopen while its retained source still reported Connected");
        rig.SelectLabel("All Players Menu");
        var ammo = rig.Controller.Snapshot().Rows.Single(row => row.Label == "Infinite Ammo (IW lobby)");
        Check(!ammo.Enabled, "Actorless recovery granted another lobby ammo write");
        rig.Send(OverlayInput.Back);
        rig.SelectLabel("Exit");
        Check(rig.Controller.ExitRequested, "The visible Exit failed to reach the runner's cleanup route");
        rig.Controller.Snapshot();
        Check(rig.Controller.ExitRequested, "Recovery metadata reopened the menu during an explicit Exit");
        var firstExit = await rig.Session.ShutdownAsync();
        Check(!firstExit.Stopped && rig.Features.DisposeCalls == 2,
            "The first visible Exit replayed the failed result instead of retrying the retained original source");
        // Follow the existing runner's failed-exit branch before requesting the retry
        rig.Controller.RetryAfterShutdownFailure(firstExit.ErrorMessage);
        rig.Session.AllowShutdownRetry();
        rig.Features.DisposeThrows = false;
        rig.SelectLabel("Exit");
        Check(rig.Controller.ExitRequested && (await rig.Session.ShutdownAsync()).Stopped
            && rig.Features.DisposeCalls == 3 && rig.Features.AmmoWrites == writes,
            "The second visible Exit did not release the original source without another patch write");
        Check(!rig.Controller.ClosedState().IsOpen && !rig.Session.ReadMenuState().NeedsSessionRecovery,
            "Successful cleanup retained an active recovery visit");
    }

    public static async Task UnconfirmedAmmoCleanupShowsRestartWithoutGrantingReconnectOrGameplay() {
        await using var rig = new RecoveryRig();
        await rig.OpenAsync();
        rig.Features.OnAmmoRead = () => {
            if (rig.Features.AmmoReadCalls >= 3) throw new IOException("Original instruction cannot be confirmed");
        };
        await rig.ToggleAmmoAsync();
        var frame = rig.Controller.Snapshot();
        var state = rig.Session.ReadMenuState();
        Check(state.NeedsSessionRecovery && !state.Connected && !state.CanConnect
            && frame.IsOpen && frame.IsRootMenu && frame.LocalMenuEligibility is null
            && frame.Notice.Contains("Fully exit Infinite Warfare", StringComparison.Ordinal),
            "Unconfirmed cleanup hid the required restart or granted a new actorless connection: " + $"Open={frame.IsOpen}, root={frame.IsRootMenu}, notice={frame.Notice}");
        var connect = frame.Rows.Single(row => row.Label == "Connect after spawning");
        Check(!connect.Enabled, "Restart recovery admitted reconnection");
        rig.SelectLabel("Connect after spawning");
        rig.Controller.StartQueuedAction();
        Check(!rig.Controller.Busy && rig.Features.OpenCalls == 1 && rig.Features.AmmoWrites == 1,
            "A disabled recovery row created another connection or patch write");
        rig.SelectLabel("Exit");
        var result = await rig.Session.ShutdownAsync();
        Check(rig.Controller.ExitRequested && result.Stopped && !result.Restored
            && !rig.Controller.ClosedState().IsOpen, "Restart recovery could not exit cleanly");
    }

    public static async Task FailedConnectDisposalPublishesExitRecoveryAndRetainsTheOriginalCandidate() {
        foreach (bool openSucceeded in new[] { false, true }) {
            await using var rig = new RecoveryRig();
            rig.Features.OpenResult = openSucceeded;
            rig.Features.ReportedPid = openSucceeded ? 4243 : 4242;
            rig.Features.DisposeThrows = true;
            rig.Send(OverlayInput.Toggle);
            rig.SettleOpening();
            rig.SelectLabel("Connect after spawning");
            rig.Controller.StartQueuedAction();
            await rig.Controller.DrainOperationAsync().WaitAsync(TimeSpan.FromSeconds(5));
            var state = rig.Session.ReadMenuState();
            var frame = rig.Controller.Snapshot();
            Check(state.NeedsSessionRecovery && !state.CanConnect && frame.IsOpen && frame.IsRootMenu
                && frame.Notice.Contains("Select Exit to retry cleanup", StringComparison.Ordinal)
                && state.RecentMessages.Contains("original retail game could not be opened", StringComparison.Ordinal),
                "Failed candidate disposal hid its original Connect error or the actual Exit recovery route");
            await rig.Session.ExecuteAsync(new(MenuActionKind.Connect));
            Check(rig.Features.OpenCalls == 1 && rig.Features.DisposeCalls == 1,
                "The retained failed candidate permitted another Open or automatic disposal retry");
            rig.Features.DisposeThrows = false;
            rig.SelectLabel("Exit");
            Check(rig.Controller.ExitRequested && (await rig.Session.ShutdownAsync()).Stopped
                && rig.Features.DisposeCalls == 2 && rig.Features.AmmoWrites == 0,
                "Exit did not release the failed original candidate without a game patch write");
        }
    }

    public static async Task LostActorAdmissionWithoutCleanupCannotGrantAnAdministrativeVisit() {
        await using var rig = new RecoveryRig();
        await rig.OpenAsync();
        rig.Fixture.Provider.BeforeRead = (_, _) => throw new IOException("Owned actor receipt unavailable");
        await rig.Session.RefreshLocalObservationAsync();
        var state = rig.Session.ReadMenuState();
        Check(state.Connected && !state.NeedsSessionRecovery && rig.Session.ReadLocalMenuAdmission() is null,
            "An actor read failure incorrectly became session cleanup authority");
        Check(!rig.Controller.Snapshot().IsOpen, "A lost actor retained its admitted menu");
        rig.Send(OverlayInput.Toggle);
        Check(!rig.Controller.Snapshot().IsOpen && rig.Features.AmmoWrites == 0,
            "Unavailable actor admission bypassed the ordinary connected-owner opening guard");
    }

    public static async Task SuccessfulAmmoOnAndOffPreserveActorAdmissionWithoutEnteringRecovery() {
        await using var rig = new RecoveryRig();
        await rig.OpenAsync();
        var original = rig.Session.ReadLocalMenuAdmission();
        await rig.ToggleAmmoAsync();
        Check(rig.Session.ReadMenuState().LobbyAmmoValue == "ON" && rig.Features.AmmoWrites == 1,
            "The real menu route did not apply the shared ammo change once");
        rig.Clock.Advance(TimeSpan.FromSeconds(1));
        rig.Controller.Snapshot();
        await rig.ToggleAmmoAsync();
        var state = rig.Session.ReadMenuState();
        Check(state.LobbyAmmoValue == "OFF" && !state.NeedsSessionRecovery
            && rig.Features.AmmoWrites == 2 && rig.Features.DisposeCalls == 0
            && rig.Session.ReadLocalMenuAdmission() == original && rig.Controller.Snapshot().IsOpen,
            "Ordinary ammo OFF entered recovery, lost actor admission, or failed to restore its patch");
    }

    // Reuse the owned observation fixture and production navigation rather than duplicate actor facts
    private sealed class RecoveryRig : IAsyncDisposable {
        internal readonly LocalAdmissionFixture Fixture = new();
        internal RecordingRetailTransport Features => Fixture.Transport.Features;
        internal Apparition.Backend.Session.RetailBackendSession Session => Fixture.Session;
        internal readonly ManualTimeProvider Clock = new();
        internal readonly BackendController Controller;
        private ulong sequence;

        internal RecoveryRig() {
            Check(VerifiedOpeningActions.TryCreate([OpeningAction.Aim, OpeningAction.Melee], out var actions, out _),
                "The fixture opening actions were rejected");
            Controller = new(4242, Session, clock: Clock, openingActions: actions,
                preferenceStore: new MemoryCommittedMenuPreferencesStore());
        }

        internal async Task OpenAsync() {
            var admission = await Fixture.OpenAndObserveAsync();
            var frame = Controller.Snapshot();
            Controller.Consume(new(++sequence, 1, 4242, OverlayInput.OpenMain, true,
                frame.NavigationRevision, frame.MainOpeningControls!.Revision, admission.Revision), () => true);
            Controller.StartQueuedAction();
            await Controller.DrainOperationAsync();
            SettleOpening();
        }

        internal void SettleOpening() {
            Controller.Snapshot();
            Clock.Advance(TimeSpan.FromMilliseconds(50));
            Controller.Snapshot();
            Clock.Advance(TimeSpan.FromSeconds(2));
            Check(Controller.Snapshot().IsOpen && !Controller.Busy, "The owned ordinary menu did not open");
        }

        internal void Send(OverlayInput input) => Controller.Consume(new(++sequence, 1, 4242, input, true), () => true);

        internal void SelectLabel(string label) {
            var frame = Controller.Snapshot();
            int target = frame.Rows.Select((row, index) => (row, index)).Single(pair => pair.row.Label == label).index;
            for (int steps = 0; Controller.Snapshot().SelectedIndex != target && steps < frame.Rows.Count; steps++)
                Send(OverlayInput.Down);
            Check(Controller.Snapshot().SelectedIndex == target, "The real row cursor could not reach " + label);
            Send(OverlayInput.Select);
            Controller.Snapshot();
        }

        internal async Task ToggleAmmoAsync() {
            if (Controller.Snapshot().IsRootMenu) SelectLabel("All Players Menu");
            SelectLabel("Infinite Ammo (IW lobby)");
            Check(Controller.Busy, "The visible ammo row did not queue its action");
            Controller.StartQueuedAction();
            await Controller.DrainOperationAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }

        public async ValueTask DisposeAsync() {
            Features.DisposeThrows = false;
            await Fixture.DisposeAsync();
        }
    }
}
