using Apparition.Backend.Controller;
using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class ControllerTests {
    public static void StartupIsClosedAndOpeningNeverConnectsAutomatically() {
        var session = new SessionBoundary();
        var clock = new ManualTimeProvider();
        var controller = new BackendController(42, session, clock: clock);
        TestAssert.True(!controller.Snapshot().IsOpen, "Startup must not display or connect automatically");
        Send(controller, 1, OverlayInput.Toggle); MenuOpenTestHarness.Settle(controller, clock);
        TestAssert.True(controller.Snapshot().IsOpen, "Toggle must open the main page");
        TestAssert.Equal(0, session.Executed.Count);
        TestAssert.Equal("Connect after spawning", controller.Snapshot().Rows[0].Label);
    }

    public static void ConnectPublishesBusyAcknowledgmentBeforeExecutionAndNeverReplays() {
        var session = new SessionBoundary { Pending = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var clock = new ManualTimeProvider();
        var controller = new BackendController(42, session, clock: clock);
        Send(controller, 1, OverlayInput.Toggle); MenuOpenTestHarness.Settle(controller, clock);
        Send(controller, 2, OverlayInput.Select);
        var pending = controller.Snapshot();
        TestAssert.Equal(2UL, pending.LastSequence);
        TestAssert.True(controller.Busy && !pending.Rows[0].Enabled, "Queued work must publish a disabled busy frame");
        TestAssert.Equal(0, session.Executed.Count);
        controller.StartQueuedAction();
        TestAssert.Equal(1, session.Executed.Count);
        Send(controller, 2, OverlayInput.Select);
        Send(controller, 3, OverlayInput.Select);
        controller.StartQueuedAction();
        TestAssert.Equal(1, session.Executed.Count);
        session.Pending.SetResult();
        controller.ObserveCompletion();
        TestAssert.True(!controller.Busy, "Completion must release the action gate");
    }

    public static void FocusChangeImmediatelyBeforeDispatchRejectsGameAction() {
        var session = new SessionBoundary();
        var clock = new ManualTimeProvider();
        var controller = new BackendController(42, session, clock: clock);
        Send(controller, 1, OverlayInput.Toggle); MenuOpenTestHarness.Settle(controller, clock);
        int focusReads = 0;
        controller.Consume(new(2, 1, 42, OverlayInput.Select, true), () => ++focusReads == 1);
        controller.StartQueuedAction();
        TestAssert.Equal(2UL, controller.LastSequence);
        TestAssert.Equal(0, session.Executed.Count);
    }

    public static void DisconnectedStaleCapabilitiesCannotEnableFeatures() {
        var session = ConnectedSession();
        session.State = session.State with { Connected = false, CanConnect = false };
        var clock = new ManualTimeProvider();
        var controller = new BackendController(42, session, clock: clock);
        Send(controller, 1, OverlayInput.Toggle); MenuOpenTestHarness.Settle(controller, clock);
        Send(controller, 2, OverlayInput.Down);
        Send(controller, 3, OverlayInput.Select);
        TestAssert.Equal("Basic Scripts", controller.Snapshot().Title);
        TestAssert.True(!controller.Snapshot().Rows[0].Enabled, "Disconnected features must remain disabled");
        Send(controller, 4, OverlayInput.Select);
        controller.StartQueuedAction();
        TestAssert.Equal(0, session.Executed.Count);
    }

    public static void PendingFeatureShowsUnknownAndCannotAcceptAnotherFeatureRequest() {
        var session = ConnectedSession();
        session.Pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var clock = new ManualTimeProvider();
        var controller = new BackendController(42, session, clock: clock);
        Send(controller, 1, OverlayInput.Toggle); MenuOpenTestHarness.Settle(controller, clock);
        Send(controller, 2, OverlayInput.Down);
        Send(controller, 3, OverlayInput.Select);
        Send(controller, 4, OverlayInput.Select);
        var state = controller.Snapshot();
        TestAssert.Equal("UNKNOWN", state.Rows[0].Value);
        TestAssert.True(!state.Rows[0].Enabled, "A pending feature cannot be selected again");
        controller.StartQueuedAction();
        Send(controller, 5, OverlayInput.Select);
        controller.StartQueuedAction();
        TestAssert.Equal(1, session.Executed.Count);
        session.Pending.SetResult();
        controller.ObserveCompletion();
    }

    public static void PendingFriendCleanupBlocksSelectionButAllowsCurrentTargetOff() {
        var session = ConnectedSession();
        session.State = session.State with {
            Players = new[] { new OverlayPlayer(1, "Friend") }, SelectedSlot = 1, SelectedName = "Friend",
            CanSelectPlayers = false, CanRefreshPlayers = false, CanTeammateGodmode = true,
            TeammateGodmodeValue = "UNKNOWN", TeammateGodmodeStatus = "Cleanup unconfirmed"
        };
        var players = OverlayMenuBuilder.Build(MenuPage.Players, session.State);
        TestAssert.True(players.Rows.All(row => !row.Enabled), "Pending cleanup must lock roster changes");
        var identity = new syndishanx_iw_tool.Players.PlayerIdentity(1, 0x10000, 0x20000, 0x30000, 4, "Friend", "1234567890ABCDEF");
        session.State = session.State with { SelectedPlayerTarget = new(identity,
            syndishanx_iw_tool.Overlay.Players.PlayerMenuRoute.Teammate, new object(), 1) };
        var current = OverlayMenuBuilder.Build(MenuPage.SelectedBasicScripts, session.State);
        TestAssert.True(current.Rows[0].Enabled, "The retained target must still allow cleanup Off");
        TestAssert.Equal(1, current.Rows[0].Action.Slot);
        TestAssert.Equal("Friend", current.Rows[0].Action.ExpectedName);
    }

    public static void FailedAsyncActionKeepsVisibleErrorAndReleasesBusyGate() {
        var session = new SessionBoundary { Fail = true };
        var clock = new ManualTimeProvider();
        var controller = new BackendController(42, session, clock: clock);
        Send(controller, 1, OverlayInput.Toggle); MenuOpenTestHarness.Settle(controller, clock);
        Send(controller, 2, OverlayInput.Select);
        controller.StartQueuedAction();
        var state = controller.Snapshot();
        TestAssert.True(!controller.Busy, "Failed work must not keep the action gate locked");
        TestAssert.True(state.Notice.Contains("Controlled failure", StringComparison.Ordinal), "Action failure must be visible");
        TestAssert.Equal(2UL, state.LastSequence);
    }

    public static void AccentSelectionWorksWithoutConnectingAndControlsRowIsUnavailable() {
        var session = new SessionBoundary();
        var clock = new ManualTimeProvider();
        var controller = new BackendController(42, session, clock: clock);
        Send(controller, 1, OverlayInput.Toggle); MenuOpenTestHarness.Settle(controller, clock);
        ulong sequence = 2;
        for (int index = 0; index < 2; index++) Send(controller, sequence++, OverlayInput.Down);
        Send(controller, sequence++, OverlayInput.Select);
        TestAssert.Equal("Menu Customization", controller.Snapshot().Title);
        TestAssert.True(!controller.Snapshot().Rows[1].Enabled, "The controls row is informational");
        int colors = controller.Snapshot().Rows.ToList().FindIndex(row => row.Label == "Main Design Color");
        TestAssert.True(colors >= 0, "The color page must remain selectable");
        for (int index = 0; index < colors; index++) Send(controller, sequence++, OverlayInput.Down);
        Send(controller, sequence++, OverlayInput.Select);
        TestAssert.Equal("Main Design Color", controller.Snapshot().Title);
        int mintIndex = controller.Snapshot().Rows.ToList().FindIndex(row => row.Label == "Mint");
        TestAssert.True(mintIndex >= 0, "The source Mint color must be selectable");
        for (int index = 0; index < mintIndex; index++) {
            Send(controller, sequence++, OverlayInput.Down);
        }
        Send(controller, sequence, OverlayInput.Select);
        TestAssert.Equal("#98FF98", controller.Snapshot().Accent);
        TestAssert.Equal(0, session.Executed.Count);
    }

    public static void ExitAndCleanupRetryRemainAvailableWhileDisconnected() {
        var clock = new ManualTimeProvider(); var controller = new BackendController(42, new SessionBoundary(), clock: clock);
        Send(controller, 1, OverlayInput.Toggle); MenuOpenTestHarness.Settle(controller, clock);
        Send(controller, 2, OverlayInput.Up);
        Send(controller, 3, OverlayInput.Select);
        TestAssert.True(controller.ExitRequested, "Exit must reach cleanup without a connection");
        controller.RetryAfterShutdownFailure("Test cleanup failure");
        TestAssert.True(!controller.ExitRequested, "An unconfirmed stop must allow another cleanup attempt");
        TestAssert.True(controller.Snapshot().Notice.Contains("Test cleanup failure", StringComparison.Ordinal), "Cleanup errors must remain visible");
        TestAssert.True(controller.Snapshot().Rows[^1].Enabled, "Retry Exit must remain enabled");
        TestAssert.True(!controller.ClosedState().IsOpen, "The terminal frame must close the menu");
    }

    private static SessionBoundary ConnectedSession() {
        var session = new SessionBoundary();
        session.State = session.State with {
            Connected = true, CanConnect = false, CanPersonalGodmode = true,
            PersonalGodmodeValue = "OFF", PersonalGodmodeStatus = "Off", CanLobbyAmmo = true,
            LobbyAmmoValue = "OFF", LobbyAmmoStatus = "Off", CanSelectPlayers = true, CanRefreshPlayers = true
        };
        return session;
    }

    private static void Send(BackendController controller, ulong sequence, OverlayInput input) =>
        controller.Consume(new(sequence, 1, 42, input, true), () => true);
}
