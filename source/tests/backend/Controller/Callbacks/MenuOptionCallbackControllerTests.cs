using Apparition.Backend.Ipc;
using Apparition.Backend.Runtime;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Backend.Tests;

// The real queue and navigation run against a controlled in-memory session
internal static class MenuOptionCallbackControllerTests {
    internal static ScopedOpeningFixture ReadyQuick(bool held = false) {
        var rig = new ScopedOpeningFixture();
        if (held) rig.Session.PendingAction = new(TaskCreationOptions.RunContinuationsAsynchronously);
        rig.Open(OverlayInput.OpenQuick);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(550));
        rig.Controller.Snapshot();
        return rig;
    }

    internal static ScopedOpeningFixture StartBoolean(bool held = false) {
        var rig = ReadyQuick(held);
        rig.Send(OverlayInput.Select);
        rig.Controller.StartQueuedAction();
        return rig;
    }

    public static void CallbackClockRecoveryCannotResurrectTheCompletedQuickOpeningWait() {
        foreach (bool afterRedraw in new[] { false, true }) {
            var rig = StartBoolean(); rig.Controller.Snapshot();
            if (afterRedraw) { rig.Clock.Advance(TimeSpan.FromMilliseconds(180)); rig.Controller.Snapshot(); }
            rig.Clock.RollBackTimestamp(TimeSpan.FromMilliseconds(1));
            var recovered = rig.Controller.Snapshot();
            TestAssert.True(recovered.MenuOpenTransition is null && recovered.MenuOpening is null,
                "Clock recovery cannot resurrect a superseded Quick opening or its row fade");
            TestAssert.Equal(afterRedraw ? "cooldown" : "pending", recovered.MenuOptionCallback!.Phase);
            TestAssert.True(recovered.Serialize().Length > 0, "Recovered callback state must pass the actual wire validator");
            int remaining = afterRedraw ? 250 : 430;
            rig.Clock.Advance(TimeSpan.FromMilliseconds(remaining - 1)); rig.Send(OverlayInput.Down);
            TestAssert.Equal(0, rig.Controller.Snapshot().SelectedIndex);
            rig.Clock.Advance(TimeSpan.FromMilliseconds(1)); var ready = rig.Controller.Snapshot();
            TestAssert.True(ready.MenuOptionCallback is null && ready.MenuOpenTransition is null
                && ready.LocalMenuEligibility!.ControlsDisabled == false,
                "Only the new finite callback stage may own the remaining monitor wait");
            rig.Send(OverlayInput.Down); TestAssert.Equal(1, rig.Controller.Snapshot().SelectedIndex);
        }
    }

    public static void BooleanWaitStartsAtDispatchAndOutlastsAnImmediateTask() {
        var rig = ReadyQuick(); rig.Send(OverlayInput.Select);
        TestAssert.True(rig.Controller.Snapshot().MenuOptionCallback is null,
            "Queued intent must not start the source callback clock");
        rig.Clock.Advance(TimeSpan.FromMilliseconds(70));
        rig.Controller.StartQueuedAction();
        var frame = rig.Controller.Snapshot();
        TestAssert.Equal("pending", frame.MenuOptionCallback!.Phase);
        TestAssert.True(!rig.Controller.Busy && frame.LocalMenuEligibility!.ControlsDisabled == true,
            "Completed backend work must retain the independent monitor wait");
        rig.Clock.Advance(TimeSpan.FromMilliseconds(179));
        TestAssert.Equal("pending", rig.Controller.Snapshot().MenuOptionCallback!.Phase);
        rig.Send(OverlayInput.Down); rig.Send(OverlayInput.Back); rig.Send(OverlayInput.Select);
        TestAssert.Equal(0, rig.Controller.Snapshot().SelectedIndex);
        TestAssert.Equal(1, rig.Session.Executed.Count);
    }

    public static void BooleanRedrawOccursOnceAt180AndInputResumesAt430() {
        var rig = StartBoolean(); var before = rig.Controller.Snapshot();
        ulong revision = before.MenuRevision!.Value;
        rig.Clock.Advance(TimeSpan.FromMilliseconds(180));
        var redraw = rig.Controller.Snapshot();
        TestAssert.Equal("cooldown", redraw.MenuOptionCallback!.Phase);
        TestAssert.Equal(revision + 1, redraw.MenuRevision!.Value);
        TestAssert.True(redraw.MenuOpening is null, "The bool refresh must retire only its active row fade");
        TestAssert.Equal(redraw.MenuRevision!.Value, rig.Controller.Snapshot().MenuRevision!.Value);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(249)); rig.Send(OverlayInput.Down);
        TestAssert.Equal(0, rig.Controller.Snapshot().SelectedIndex);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(1));
        var ready = rig.Controller.Snapshot();
        TestAssert.True(ready.MenuOptionCallback is null && ready.LocalMenuEligibility!.ControlsDisabled == false,
            "The source monitor must resume at430ms");
        rig.Send(OverlayInput.Down); TestAssert.Equal(1, rig.Controller.Snapshot().SelectedIndex);
    }

    public static void LongBackendWorkDoesNotMoveTheBoolRedrawOrRepeatItOnCompletion() {
        var rig = StartBoolean(held: true); var before = rig.Controller.Snapshot();
        rig.Clock.Advance(TimeSpan.FromMilliseconds(180)); var redraw = rig.Controller.Snapshot();
        TestAssert.Equal(before.MenuRevision!.Value + 1, redraw.MenuRevision!.Value);
        TestAssert.Equal("UNKNOWN", redraw.Rows[0].Value);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(250)); var waiting = rig.Controller.Snapshot();
        TestAssert.True(waiting.MenuOptionCallback is null && rig.Controller.Busy,
            "Long work remains independently busy after the source monitor wait");
        TestAssert.True(waiting.LocalMenuEligibility!.ControlsDisabled == false && waiting.LocalMenuEligibility.Blocked,
            "Work admission must not fabricate a source control lock");
        rig.Session.PendingAction!.SetResult(true); var complete = rig.Controller.Snapshot();
        TestAssert.Equal(redraw.MenuRevision!.Value, complete.MenuRevision!.Value);
        TestAssert.True(!rig.Controller.Busy && complete.MenuOptionCallback is null,
            "Worker completion must not restart or redraw the source callback");
    }

    public static void FaultedBackendWorkRetainsTheFiniteWaitWithoutClaimingSuccess() {
        var rig = StartBoolean(held: true);
        rig.Session.PendingAction!.SetException(new InvalidOperationException("Controlled failure"));
        var frame = rig.Controller.Snapshot();
        TestAssert.True(frame.Notice.Contains("Controlled failure", StringComparison.Ordinal), "Actual failure must remain visible");
        TestAssert.Equal("pending", frame.MenuOptionCallback!.Phase);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(430));
        TestAssert.True(rig.Controller.Snapshot().MenuOptionCallback is null, "A failed task cannot strand source controls");
    }

    public static void ReplacementOwnerNavigationAndShutdownCannotRedrawAStaleCallback() {
        foreach (string change in new[] { "owner", "navigation", "shutdown" }) {
            var rig = StartBoolean(); rig.Controller.Snapshot();
            if (change == "owner") rig.Session.Change(LocalPlayerLife.Alive, replaceOwner: true);
            if (change == "navigation") rig.Navigation.OpenPage(MenuPage.History);
            if (change == "shutdown") rig.Controller.RequestShutdown();
            ulong revision = rig.Navigation.MenuRevision;
            rig.Clock.Advance(TimeSpan.FromMilliseconds(180));
            rig.Controller.ReadMenuPublicationPhase();
            TestAssert.Equal(revision, rig.Navigation.MenuRevision);
            TestAssert.True(rig.Controller.Snapshot().MenuOptionCallback is null,
                "A stale callback cannot attach to a replacement or administrative frame");
        }
    }

    public static void PlainLobbyAdapterWaits250WithoutBorrowingABooleanRedraw() {
        var rig = new ScopedOpeningFixture(); rig.Open(OverlayInput.OpenMain);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(50)); rig.Controller.Snapshot();
        rig.Clock.Advance(TimeSpan.FromMilliseconds(550)); rig.Controller.Snapshot();
        SelectLabel(rig, "All Players Menu");
        SelectLabel(rig, "Infinite Ammo (IW lobby)"); rig.Controller.StartQueuedAction();
        var frame = rig.Controller.Snapshot();
        TestAssert.Equal(MenuActionKind.ToggleLobbyAmmo, rig.Session.Executed.Single().Kind);
        TestAssert.Equal("plain", frame.MenuOptionCallback!.Kind);
        TestAssert.Equal("cooldown", frame.MenuOptionCallback.Phase);
        ulong revision = frame.MenuRevision!.Value;
        rig.Clock.Advance(TimeSpan.FromMilliseconds(249)); rig.Send(OverlayInput.Back);
        TestAssert.Equal(MenuPage.AllPlayers, rig.Navigation.Page);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(1)); var ready = rig.Controller.Snapshot();
        TestAssert.True(ready.MenuOptionCallback is null, "The IW binary ammo adapter uses the ordinary source caller wait");
        TestAssert.Equal(revision, ready.MenuRevision!.Value);
        rig.Send(OverlayInput.Back); TestAssert.Equal(MenuPage.Main, rig.Navigation.Page);
    }

    public static void BooleanRedrawCounterAdmissionHappensBeforeGameplayDispatch() {
        var rig = new ScopedOpeningFixture(navigation: new OverlayNavigation(0, ulong.MaxValue - 3));
        rig.Open(OverlayInput.OpenQuick); rig.Clock.Advance(TimeSpan.FromMilliseconds(550));
        TestAssert.Equal(ulong.MaxValue, rig.Controller.Snapshot().MenuRevision!.Value);
        rig.Send(OverlayInput.Select); rig.Controller.StartQueuedAction();
        TestAssert.Equal(0, rig.Session.Executed.Count);
        TestAssert.True(!rig.Controller.Busy && rig.Controller.Snapshot().MenuOptionCallback is null,
            "An unavailable redraw epoch must reject before starting any backend task");
        rig.Controller.StartQueuedAction(); TestAssert.Equal(0, rig.Session.Executed.Count);
    }

    public static void FailedClockAdmissionDoesNotDispatchOrReplayAnAcceptedOption() {
        var clock = new SwitchableClock(); var rig = new ScopedOpeningFixture(clock: clock);
        rig.Open(OverlayInput.OpenQuick); clock.Advance(550); rig.Controller.Snapshot();
        rig.Send(OverlayInput.Select); clock.Fail = true;
        rig.Controller.StartQueuedAction(); clock.Fail = false;
        TestAssert.Equal(0, rig.Session.Executed.Count);
        var frame = rig.Controller.Snapshot();
        TestAssert.True(!rig.Controller.Busy && frame.MenuOptionCallback is null
            && frame.Notice.Contains("Controlled clock failure", StringComparison.Ordinal),
            "Failed admission must leave a visible error and no committed callback owner");
        rig.Controller.StartQueuedAction(); TestAssert.Equal(0, rig.Session.Executed.Count);
    }

    private sealed class SwitchableClock : TimeProvider {
        private long timestamp;
        public bool Fail { get; set; }
        public override long TimestampFrequency => 1000;
        public void Advance(long milliseconds) => timestamp += milliseconds;
        public override long GetTimestamp() => Fail ? throw new IOException("Controlled clock failure") : timestamp;
    }

    public static void VerifiedPlayerSelectionCarriesOnlyItsOwnRemainingPlainWaitToTheChild() {
        var rig = new ScopedOpeningFixture(); rig.Open(OverlayInput.OpenMain);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(50)); rig.Controller.Snapshot();
        rig.Clock.Advance(TimeSpan.FromMilliseconds(550)); rig.Controller.Snapshot();
        SelectLabel(rig, "Players Menu");
        SelectLabel(rig, "Friend");
        rig.Controller.StartQueuedAction();
        var frame = rig.Controller.Snapshot();
        TestAssert.Equal(MenuPage.SelectedPlayer, rig.Navigation.Page);
        TestAssert.Equal("plain", frame.MenuOptionCallback!.Kind);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(249)); rig.Send(OverlayInput.Select);
        TestAssert.Equal(1, rig.Session.Executed.Count);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(1));
        TestAssert.True(rig.Controller.Snapshot().MenuOptionCallback is null,
            "Verified child navigation cannot restart its source caller wait");
        SelectLabel(rig, "Basic Scripts");
        rig.Send(OverlayInput.Select); rig.Controller.StartQueuedAction();
        TestAssert.Equal(MenuActionKind.ToggleTeammateGodmode, rig.Session.Executed.Last().Kind);
        TestAssert.Equal("boolean", rig.Controller.Snapshot().MenuOptionCallback!.Kind);
    }

    public static void MainDeathWaitsForItsCallbackButRevokedPermissionClosesImmediately() {
        foreach (bool revoke in new[] { false, true }) {
            var rig = new ScopedOpeningFixture(); rig.Open(OverlayInput.OpenMain);
            rig.Clock.Advance(TimeSpan.FromMilliseconds(50)); rig.Controller.Snapshot();
            rig.Clock.Advance(TimeSpan.FromMilliseconds(550)); rig.Controller.Snapshot();
            SelectLabel(rig, "Basic Scripts"); SelectLabel(rig, "God Mode");
            rig.Controller.StartQueuedAction(); rig.Controller.Snapshot();
            rig.Session.Change(LocalPlayerLife.Dead, hasMenu: !revoke);
            rig.Clock.Advance(TimeSpan.FromMilliseconds(429));
            TestAssert.Equal(!revoke, rig.Controller.Snapshot().IsOpen);
            rig.Clock.Advance(TimeSpan.FromMilliseconds(1));
            TestAssert.True(!rig.Controller.Snapshot().IsOpen,
                "Main must close when its dead actor's ordinary monitor resumes");
        }
    }

    private static void SelectLabel(ScopedOpeningFixture rig, string label) {
        var frame = rig.Controller.Snapshot();
        int index = frame.Rows.ToList().FindIndex(row => row.Label == label);
        TestAssert.True(index >= 0, "The actual menu must contain the expected selectable row: " + label);
        for (int moved = 0; moved < frame.Rows.Count && rig.Navigation.SelectedIndex != index; moved++) rig.Send(OverlayInput.Down);
        TestAssert.Equal(index, rig.Navigation.SelectedIndex);
        rig.Send(OverlayInput.Select);
    }

    public static void CallbackPublicationEdgesRemainDueAcrossFocusHiddenFrames() {
        var rig = StartBoolean();
        var publisher = new MenuPhasePublication(state => state.Serialize(), rig.Clock);
        publisher.Publish(rig.Controller.Snapshot(), false);
        TestAssert.True(!publisher.HasChanged(rig.Controller.ReadMenuPublicationPhase()), "A committed pending owner phase must remain stable");
        rig.Clock.Advance(TimeSpan.FromMilliseconds(180));
        TestAssert.True(publisher.HasChanged(rig.Controller.ReadMenuPublicationPhase()), "Bool redraw must publish a distinct phase");
        publisher.Publish(rig.Controller.Snapshot(), false);
        TestAssert.True(!publisher.HasChanged(rig.Controller.ReadMenuPublicationPhase()), "Focus-hidden publication retains the actual owner phase");
        rig.Clock.Advance(TimeSpan.FromMilliseconds(250));
        TestAssert.True(publisher.HasChanged(rig.Controller.ReadMenuPublicationPhase()), "Wait retirement must publish even without focus");
    }
}
