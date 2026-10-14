using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Backend.Tests;

internal static class MenuBooleanPaintControllerTests {
    private static MenuBooleanPaint Paint(StateDocument frame) {
        TestAssert.True(frame.MenuBooleanPaint is not null, "The actual owned gameplay menu must publish copied boolean paint");
        return frame.MenuBooleanPaint!;
    }
    private static string Value(StateDocument frame) => Paint(frame).Rows.Single(row => row.Index == 0).Value;

    public static void PreviewAndDisconnectedSessionsCannotPublishGameplayPaintMetadata() {
        foreach (bool preview in new[] { false, true }) {
            var rig = new ScopedOpeningFixture();
            rig.Session.State = rig.Session.State with { Preview = preview, Connected = preview };
            rig.Open(OverlayInput.OpenQuick);
            var frame = rig.Controller.Snapshot();
            TestAssert.True(frame.MenuBooleanPaint is null, "A preview or disconnected view cannot own gameplay paint metadata");
            TestAssert.Equal("UNKNOWN", frame.Rows[0].Value);
            TestAssert.Equal(0, rig.Session.Executed.Count);
        }
        var live = MenuOptionCallbackControllerTests.ReadyQuick(); Paint(live.Controller.Snapshot());
        live.Session.State = live.Session.State with { Preview = true };
        TestAssert.True(live.Controller.Snapshot().MenuBooleanPaint is null,
            "Entering an inert preview must retire its prior gameplay display cache");
    }

    public static void QuickDrawCapturesPaintAndCursorOrHeartbeatKeepsTheOriginalSample() {
        var rig = new ScopedOpeningFixture(); rig.Open(OverlayInput.OpenQuick);
        var opening = rig.Controller.Snapshot();
        TestAssert.Equal("OFF", Value(opening));
        TestAssert.Equal("cooldown", opening.MenuOpenTransition!.Phase);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(550)); rig.Controller.Snapshot();
        rig.Session.State = rig.Session.State with { PersonalGodmodeValue = "ON" };
        var current = rig.Controller.Snapshot();
        TestAssert.Equal("ON", current.Rows[0].Value);
        TestAssert.Equal("OFF", Value(current));
        TestAssert.Equal(Paint(opening).Revision, Paint(current).Revision);
        TestAssert.True(current.Notice.Contains("Current God Mode: ON", StringComparison.Ordinal),
            "A retained display must disclose a different known current state");
        rig.Send(OverlayInput.Down); rig.Send(OverlayInput.Up);
        TestAssert.Equal(Paint(opening).Revision, Paint(rig.Controller.Snapshot()).Revision);
    }

    public static void BooleanActionSamplesChangedFactsOnlyAtTheActual180MillisecondRedraw() {
        var rig = MenuOptionCallbackControllerTests.StartBoolean(); var initial = rig.Controller.Snapshot();
        rig.Session.State = rig.Session.State with { PersonalGodmodeValue = "ON" };
        rig.Clock.Advance(TimeSpan.FromMilliseconds(179)); var pending = rig.Controller.Snapshot();
        TestAssert.Equal("ON", pending.Rows[0].Value); TestAssert.Equal("OFF", Value(pending));
        rig.Clock.Advance(TimeSpan.FromMilliseconds(1)); var redraw = rig.Controller.Snapshot();
        TestAssert.Equal("ON", Value(redraw));
        TestAssert.Equal(Paint(initial).Revision + 1, Paint(redraw).Revision);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(250)); var retired = rig.Controller.Snapshot();
        TestAssert.Equal(Paint(redraw).Revision, Paint(retired).Revision);
        TestAssert.True(retired.MenuOptionCallback is null, "Callback retirement does not repaint a bool row");
    }

    public static void LateWorkKeepsUnknownPaintUntilAnActualRedrawWithoutOverwritingConfirmedFacts() {
        var rig = MenuOptionCallbackControllerTests.StartBoolean(held: true);
        var pending = rig.Controller.Snapshot();
        TestAssert.Equal("UNKNOWN", pending.Rows[0].Value); TestAssert.Equal("OFF", Value(pending));
        rig.Clock.Advance(TimeSpan.FromMilliseconds(180)); var redraw = rig.Controller.Snapshot();
        TestAssert.Equal("UNKNOWN", Value(redraw));
        rig.Clock.Advance(TimeSpan.FromMilliseconds(250)); rig.Controller.Snapshot();
        rig.Session.State = rig.Session.State with { PersonalGodmodeValue = "ON" };
        rig.Session.PendingAction!.SetResult(true); var complete = rig.Controller.Snapshot();
        TestAssert.Equal("ON", complete.Rows[0].Value); TestAssert.Equal("UNKNOWN", Value(complete));
        TestAssert.Equal(Paint(redraw).Revision, Paint(complete).Revision);
        TestAssert.True(complete.Notice.Contains("Current God Mode: ON", StringComparison.Ordinal),
            "The retained UNKNOWN display must not hide the known current state");
        rig.Send(OverlayInput.Down); rig.Send(OverlayInput.Up); rig.Send(OverlayInput.Select);
        rig.Controller.StartQueuedAction();
        TestAssert.Equal(2, rig.Session.Executed.Count);
        TestAssert.Equal(MenuActionKind.TogglePersonalGodmode, rig.Session.Executed.Last().Kind);
    }

    public static void ReopeningAndTopologyChangesCaptureFreshValuesWhileHiddenFocusPreservesOwnerPaint() {
        var rig = MenuOptionCallbackControllerTests.ReadyQuick(); var initial = rig.Controller.Snapshot();
        rig.Session.State = rig.Session.State with { PersonalGodmodeValue = "ON" };
        var owner = rig.Controller.Snapshot();
        TestAssert.True(StateFocusProjection.Apply(owner, false).MenuBooleanPaint is null, "Hidden projections omit paint");
        TestAssert.Equal(Paint(initial).Revision, Paint(rig.Controller.Snapshot()).Revision);
        rig.Send(OverlayInput.Back); TestAssert.True(rig.Controller.Snapshot().MenuBooleanPaint is null, "Closed menus omit paint");
        rig.Clock.Advance(TimeSpan.FromMilliseconds(250));
        rig.Open(OverlayInput.OpenQuick); var reopened = rig.Controller.Snapshot();
        TestAssert.Equal("ON", Value(reopened));
        TestAssert.True(Paint(reopened).Revision > Paint(initial).Revision, "A new menu visit owns fresh paint");
        rig.Clock.Advance(TimeSpan.FromMilliseconds(550)); rig.Controller.Snapshot();
        rig.Session.State = rig.Session.State with { PersonalGodmodeIdentity = null, PersonalGodmodeRequiresVerifiedTarget = true };
        TestAssert.True(rig.Controller.Snapshot().MenuBooleanPaint is null,
            "A changed unavailable action cannot retain the prior player's boolean paint");
    }

    public static void RetainedMainPageCapturesAtTheFirstVisibleDrawBeforeTheNextHeartbeat() {
        var rig = new ScopedOpeningFixture(); rig.Open(OverlayInput.OpenMain);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(50)); rig.Controller.Snapshot();
        rig.Clock.Advance(TimeSpan.FromMilliseconds(550)); rig.Controller.Snapshot();
        rig.Navigation.OpenPage(MenuPage.Personal); Paint(rig.Controller.Snapshot());
        rig.Send(OverlayInput.Toggle); TestAssert.True(!rig.Controller.Snapshot().IsOpen, "The retained Main page must close");
        rig.Open(OverlayInput.OpenMain);
        TestAssert.True(rig.Controller.Snapshot().MenuBooleanPaint is null, "The hidden rebuild cannot show boolean paint");
        rig.Clock.Advance(TimeSpan.FromMilliseconds(50)); var drawn = rig.Controller.Snapshot();
        TestAssert.Equal("OFF", Value(drawn));
        TestAssert.Equal("cooldown", drawn.MenuOpenTransition!.Phase);
        rig.Session.State = rig.Session.State with { PersonalGodmodeValue = "ON" };
        var heartbeat = rig.Controller.Snapshot();
        TestAssert.Equal("ON", heartbeat.Rows[0].Value); TestAssert.Equal("OFF", Value(heartbeat));
        TestAssert.Equal(Paint(drawn).Revision, Paint(heartbeat).Revision);
    }

    public static void LifeChangesRemovingAllBooleanRowsStillReserveThePendingRedrawContext() {
        var rig = new ScopedOpeningFixture(navigation: new OverlayNavigation(0, ulong.MaxValue - 5));
        rig.Open(OverlayInput.OpenQuick); rig.Clock.Advance(TimeSpan.FromMilliseconds(550)); rig.Controller.Snapshot();
        rig.Send(OverlayInput.Select); rig.Controller.StartQueuedAction(); rig.Controller.Snapshot();
        ulong before = rig.Navigation.MenuRevision;
        rig.Session.Change(LocalPlayerLife.Dead);
        TestAssert.Throws<OverflowException>(() => rig.Controller.Snapshot());
        TestAssert.Equal(before, rig.Navigation.MenuRevision);
        rig.Session.Change(LocalPlayerLife.Unknown);
        TestAssert.Throws<OverflowException>(() => rig.Controller.Snapshot());
        TestAssert.Equal(before, rig.Navigation.MenuRevision);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(180)); var redraw = rig.Controller.Snapshot();
        TestAssert.Equal(ulong.MaxValue, redraw.MenuRevision!.Value);
        TestAssert.True(redraw.MenuBooleanPaint is null && redraw.MenuOptionCallback is { Phase: "cooldown" },
            "The actual life-change redraw must finish without inventing a boolean for an unknown actor");
        TestAssert.Equal(1, rig.Session.Executed.Count);
    }

    public static void NavigationPreflightReservesBothScheduledAndChangedTopologyRedrawEpochs() {
        var insufficient = new ScopedOpeningFixture(navigation: new OverlayNavigation(0, ulong.MaxValue - 4));
        insufficient.Open(OverlayInput.OpenQuick); insufficient.Clock.Advance(TimeSpan.FromMilliseconds(550)); insufficient.Controller.Snapshot();
        insufficient.Send(OverlayInput.Select); insufficient.Controller.StartQueuedAction();
        TestAssert.Equal(0, insufficient.Session.Executed.Count);
        var rig = new ScopedOpeningFixture(navigation: new OverlayNavigation(0, ulong.MaxValue - 5));
        rig.Open(OverlayInput.OpenQuick); rig.Clock.Advance(TimeSpan.FromMilliseconds(550)); rig.Controller.Snapshot();
        rig.Send(OverlayInput.Select); rig.Controller.StartQueuedAction(); rig.Controller.Snapshot();
        TestAssert.Equal(1, rig.Session.Executed.Count);
        ulong before = rig.Navigation.MenuRevision;
        rig.Session.State = rig.Session.State with { CanPersonalGodmode = false, CanRestorePersonalGodmode = true };
        TestAssert.Throws<OverflowException>(() => rig.Controller.Snapshot());
        TestAssert.Equal(before, rig.Navigation.MenuRevision);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(180)); var redraw = rig.Controller.Snapshot();
        TestAssert.Equal(ulong.MaxValue, redraw.MenuRevision!.Value);
        TestAssert.Equal("cooldown", redraw.MenuOptionCallback!.Phase);
        TestAssert.Equal(MenuActionKind.RestorePersonalGodmode, rig.Navigation.Rows[0].Action.Kind);
        TestAssert.Equal(ulong.MaxValue, rig.Controller.Snapshot().MenuRevision!.Value);
    }

    public static void RecoveryTopologyCannotSpendTheEpochReservedForItsPendingBooleanRedraw() {
        var rig = new ScopedOpeningFixture(booleanPaintRevision: ulong.MaxValue - 2);
        rig.Open(OverlayInput.OpenQuick); rig.Clock.Advance(TimeSpan.FromMilliseconds(550)); rig.Controller.Snapshot();
        rig.Send(OverlayInput.Select); rig.Controller.StartQueuedAction(); rig.Controller.Snapshot();
        TestAssert.Equal(1, rig.Session.Executed.Count);
        ulong before = rig.Navigation.MenuRevision;
        rig.Session.State = rig.Session.State with { CanPersonalGodmode = false, CanRestorePersonalGodmode = true };
        TestAssert.Throws<OverflowException>(() => rig.Controller.Snapshot());
        TestAssert.Equal(before, rig.Navigation.MenuRevision);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(180)); var redraw = rig.Controller.Snapshot();
        TestAssert.Equal(ulong.MaxValue, Paint(redraw).Revision);
        TestAssert.Equal("cooldown", redraw.MenuOptionCallback!.Phase);
        TestAssert.Equal(MenuActionKind.RestorePersonalGodmode, rig.Navigation.Rows[0].Action.Kind);
        TestAssert.Equal(Paint(redraw).Revision, Paint(rig.Controller.Snapshot()).Revision);
    }

    public static void ExhaustedPaintEpochRejectsBooleanDispatchWithoutReplayingQueuedWork() {
        var rig = new ScopedOpeningFixture(booleanPaintRevision: ulong.MaxValue - 1);
        rig.Open(OverlayInput.OpenQuick); rig.Clock.Advance(TimeSpan.FromMilliseconds(550));
        var frame = rig.Controller.Snapshot(); TestAssert.Equal(ulong.MaxValue, Paint(frame).Revision);
        rig.Send(OverlayInput.Select); rig.Controller.StartQueuedAction();
        TestAssert.Equal(0, rig.Session.Executed.Count);
        TestAssert.True(!rig.Controller.Busy && rig.Controller.Snapshot().MenuOptionCallback is null,
            "Paint counter admission must precede gameplay dispatch and retain no callback");
        rig.Controller.StartQueuedAction(); TestAssert.Equal(0, rig.Session.Executed.Count);
        TestAssert.Equal(ulong.MaxValue, Paint(rig.Controller.Snapshot()).Revision);
    }

    public static void RevokedOrReplacedActorCannotBorrowPaintAndPlainLobbyAdapterHasNoBooleanCache() {
        foreach (bool replacement in new[] { false, true }) {
            var rig = MenuOptionCallbackControllerTests.ReadyQuick(); Paint(rig.Controller.Snapshot());
            rig.Session.Change(LocalPlayerLife.Alive, hasMenu: replacement, replaceOwner: replacement);
            var stale = rig.Controller.Snapshot();
            TestAssert.True(!stale.IsOpen && stale.MenuBooleanPaint is null, "A stale actor cannot publish an old indicator");
        }
        var lobby = new ScopedOpeningFixture(); lobby.Open(OverlayInput.OpenMain);
        lobby.Clock.Advance(TimeSpan.FromMilliseconds(50)); lobby.Controller.Snapshot();
        lobby.Clock.Advance(TimeSpan.FromMilliseconds(550)); lobby.Controller.Snapshot();
        lobby.Navigation.OpenPage(MenuPage.AllPlayers);
        TestAssert.True(lobby.Controller.Snapshot().MenuBooleanPaint is null, "The plain IW lobby ammo adapter is not a source BOOL row");
    }
}
