using Apparition.Backend.Controller;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class ConnectionFeedbackTests {
    public static void ReturnedConnectWithoutVerifiedConnectionReportsAnActionableFailure() {
        var session = new SessionBoundary();
        var clock = new ManualTimeProvider();
        var controller = new BackendController(42, session, clock: clock);
        Send(controller, 1, OverlayInput.Toggle); MenuOpenTestHarness.Settle(controller, clock);
        Send(controller, 2, OverlayInput.Select);
        controller.StartQueuedAction();
        var state = controller.Snapshot();
        TestAssert.Equal("DISCONNECTED", state.Context);
        TestAssert.True(state.Notice.Contains("Connection did not complete", StringComparison.Ordinal),
            "A silent backend return cannot look like a successful connection");
        TestAssert.True(state.Notice.Contains("Connect after spawning", StringComparison.Ordinal),
            "Connection failure must describe the safe manual retry");
        Send(controller, 2, OverlayInput.Select);
        controller.StartQueuedAction();
        TestAssert.Equal(1, session.Executed.Count);
    }

    public static void VerifiedConnectionClearsGuidanceAndUnlocksConfirmedCapabilities() {
        var session = new SessionBoundary { Pending = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var clock = new ManualTimeProvider();
        var controller = new BackendController(42, session, clock: clock);
        Send(controller, 1, OverlayInput.Toggle); MenuOpenTestHarness.Settle(controller, clock);
        Send(controller, 2, OverlayInput.Select);
        controller.StartQueuedAction();
        session.State = session.State with { Connected = true, CanConnect = false,
            CanPersonalGodmode = true, PersonalGodmodeValue = "OFF", CanLobbyAmmo = true, LobbyAmmoValue = "OFF" };
        session.Pending.SetResult();
        controller.ObserveCompletion();
        var connected = controller.Snapshot();
        TestAssert.Equal("CONNECTED", connected.Context);
        TestAssert.True(!connected.Notice.Contains("Spawn", StringComparison.OrdinalIgnoreCase),
            "Verified connection cannot keep a stale spawn instruction");
        Send(controller, 3, OverlayInput.Down);
        Send(controller, 4, OverlayInput.Select);
        var personal = controller.Snapshot();
        TestAssert.True(personal.Rows[0].Enabled, "Only verified backend capabilities enable a feature");
        TestAssert.Equal("OFF", personal.Rows[0].Value);
    }

    public static void FocusRejectedSelectReportsRetryAndNeverReplaysTheAcknowledgedAction() {
        var session = new SessionBoundary();
        var clock = new ManualTimeProvider();
        var controller = new BackendController(42, session, clock: clock);
        Send(controller, 1, OverlayInput.Toggle); MenuOpenTestHarness.Settle(controller, clock);
        int reads = 0;
        controller.Consume(new(2, 1, 42, OverlayInput.Select, true), () => ++reads == 1);
        controller.StartQueuedAction();
        TestAssert.Equal(0, session.Executed.Count);
        TestAssert.Equal(2UL, controller.LastSequence);
        TestAssert.True(controller.Snapshot().Notice.Contains("focus", StringComparison.OrdinalIgnoreCase),
            "A focus rejection must explain why selection did not execute");
        Send(controller, 2, OverlayInput.Select);
        controller.StartQueuedAction();
        TestAssert.Equal(0, session.Executed.Count);
    }

    public static void CompletedPlayerSelectionCannotNavigateAfterTheMenuWasClosed() {
        var session = new SessionBoundary { Pending = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        session.State = session.State with { Connected = true, CanConnect = false,
            CanSelectPlayers = true, CanRefreshPlayers = true, Players = new[] { new OverlayPlayer(1, "Friend") } };
        var clock = new ManualTimeProvider();
        var controller = new BackendController(42, session, clock: clock);
        Send(controller, 1, OverlayInput.Toggle); MenuOpenTestHarness.Settle(controller, clock);
        Send(controller, 2, OverlayInput.Down);
        Send(controller, 3, OverlayInput.Down);
        Send(controller, 4, OverlayInput.Down);
        Send(controller, 5, OverlayInput.Select);
        Send(controller, 6, OverlayInput.Down);
        Send(controller, 7, OverlayInput.Select);
        controller.StartQueuedAction();
        Send(controller, 8, OverlayInput.Back);
        Send(controller, 9, OverlayInput.Back);
        session.State = session.State with { SelectedSlot = 1, SelectedName = "Friend", CanTeammateGodmode = true };
        session.Pending.SetResult();
        var closed = controller.Snapshot();
        TestAssert.True(!closed.IsOpen, "A completed selection cannot reopen the menu");
        TestAssert.Equal("Apparition", closed.Title);
        Send(controller, 10, OverlayInput.Toggle); MenuOpenTestHarness.Settle(controller, clock);
        TestAssert.Equal("Apparition", controller.Snapshot().Title);
    }

    public static void CompletedPlayerSelectionCannotReplaceASeparatePageOpenedWhilePending() {
        var session = new SessionBoundary { Pending = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        session.State = session.State with { Connected = true, CanConnect = false,
            CanSelectPlayers = true, CanRefreshPlayers = true, Players = new[] { new OverlayPlayer(1, "Friend") } };
        var clock = new ManualTimeProvider();
        var controller = new BackendController(42, session, clock: clock);
        Send(controller, 1, OverlayInput.Toggle); MenuOpenTestHarness.Settle(controller, clock);
        Send(controller, 2, OverlayInput.Down);
        Send(controller, 3, OverlayInput.Down);
        Send(controller, 4, OverlayInput.Down);
        Send(controller, 5, OverlayInput.Select);
        Send(controller, 6, OverlayInput.Down);
        Send(controller, 7, OverlayInput.Select);
        controller.StartQueuedAction();
        Send(controller, 8, OverlayInput.Back);
        Send(controller, 9, OverlayInput.Up);
        Send(controller, 10, OverlayInput.Select);
        TestAssert.Equal("Menu Customization", controller.Snapshot().Title);
        session.State = session.State with { SelectedSlot = 1, SelectedName = "Friend", CanTeammateGodmode = true };
        session.Pending.SetResult();
        TestAssert.Equal("Menu Customization", controller.Snapshot().Title);
    }

    public static void BackAndReturnToTheSamePlayersPageStillCancelsDelayedSelectionNavigation() {
        var (controller, session, clock) = PendingFriendSelection();
        Send(controller, 8, OverlayInput.Back);
        Send(controller, 9, OverlayInput.Select);
        TestAssert.Equal("Players Menu", controller.Snapshot().Title);
        CompleteFriendSelection(session);
        TestAssert.Equal("Players Menu", controller.Snapshot().Title);
        TestAssert.Equal(1, controller.Snapshot().SelectedIndex);
    }

    public static void ClosingAndReopeningPlayersStillCancelsDelayedSelectionNavigation() {
        var (controller, session, clock) = PendingFriendSelection();
        Send(controller, 8, OverlayInput.Toggle); MenuOpenTestHarness.Settle(controller, clock);
        Send(controller, 9, OverlayInput.Toggle); MenuOpenTestHarness.Settle(controller, clock);
        TestAssert.Equal("Players Menu", controller.Snapshot().Title);
        CompleteFriendSelection(session);
        TestAssert.Equal("Players Menu", controller.Snapshot().Title);
    }

    public static void VerifiedSelectionWithUnchangedNavigationOpensItsPageAndRestoresTheParentCursor() {
        var (controller, session, clock) = PendingFriendSelection();
        // Repeated heartbeat snapshots must not count as a user navigation change
        controller.Snapshot();
        controller.Snapshot();
        CompleteFriendSelection(session);
        var selected = controller.Snapshot();
        TestAssert.Equal("Friend", selected.Title);
        TestAssert.True(selected.Rows[1].Enabled, "Only the verified selected identity permits its Basic Scripts link");
        Send(controller, 8, OverlayInput.Back);
        TestAssert.Equal("Players Menu", controller.Snapshot().Title);
        TestAssert.Equal(1, controller.Snapshot().SelectedIndex);
        TestAssert.Equal(1, session.Executed.Count);
    }

    public static void MovingAwayAndBackToTheSamePlayerRowCancelsDelayedSelectionNavigation() {
        var (controller, session, clock) = PendingFriendSelection();
        Send(controller, 8, OverlayInput.Up);
        Send(controller, 9, OverlayInput.Down);
        TestAssert.Equal(1, controller.Snapshot().SelectedIndex);
        CompleteFriendSelection(session);
        TestAssert.Equal("Players Menu", controller.Snapshot().Title);
    }

    public static void NavigationBeforeQueuedExecutionStillCancelsDelayedSelectionNavigation() {
        var (controller, session, clock) = PendingFriendSelection(startQueuedAction: false);
        // Navigation can occur after acknowledgment but before the owner starts the task
        Send(controller, 8, OverlayInput.Back);
        Send(controller, 9, OverlayInput.Select);
        controller.StartQueuedAction();
        CompleteFriendSelection(session);
        TestAssert.Equal("Players Menu", controller.Snapshot().Title);
        TestAssert.Equal(1, session.Executed.Count);
    }

    private static (BackendController Controller, SessionBoundary Session, ManualTimeProvider Clock) PendingFriendSelection(bool startQueuedAction = true) {
        var session = new SessionBoundary { Pending = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        session.State = session.State with { Connected = true, CanConnect = false,
            CanSelectPlayers = true, CanRefreshPlayers = true, Players = new[] { new OverlayPlayer(1, "Friend") } };
        var clock = new ManualTimeProvider();
        var controller = new BackendController(42, session, clock: clock);
        Send(controller, 1, OverlayInput.Toggle); MenuOpenTestHarness.Settle(controller, clock);
        Send(controller, 2, OverlayInput.Down);
        Send(controller, 3, OverlayInput.Down);
        Send(controller, 4, OverlayInput.Down);
        Send(controller, 5, OverlayInput.Select);
        Send(controller, 6, OverlayInput.Down);
        Send(controller, 7, OverlayInput.Select);
        if (startQueuedAction) controller.StartQueuedAction();
        return (controller, session, clock);
    }

    private static void CompleteFriendSelection(SessionBoundary session) {
        session.State = session.State with { SelectedSlot = 1, SelectedName = "Friend", CanTeammateGodmode = true };
        session.Pending!.SetResult();
    }

    private static void Send(BackendController controller, ulong sequence, OverlayInput input) =>
        controller.Consume(new(sequence, 1, 42, input, true), () => true);
}
