using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Backend.Tests;

// The real controller receives inert actor proofs and a controlled source clock
internal static class MenuInstructionCallbackControllerTests {
    private static ScopedOpeningFixture Ready() => LocalOptionCallbackControllerTests.ReadyPage(MenuPage.Instructions);
    private static void Select(ScopedOpeningFixture rig, int index) => LocalOptionCallbackControllerTests.SelectIndex(rig, index);
    private static string Paint(StateDocument frame, int index) {
        TestAssert.True(frame.MenuBooleanPaint is { Rows.Count: 4 }, "The four source instruction BOOL rows require an owned display sample");
        return frame.MenuBooleanPaint!.Rows.Single(row => row.Index == index).Value;
    }

    public static void FourInstructionBooleansKeepActualFlagsAndSourceRedrawClocksSeparate() {
        for (int index = 0; index < 4; index++) {
            var rig = Ready(); Select(rig, index); var before = rig.Controller.Snapshot();
            TestAssert.Equal("OFF", Paint(before, index)); rig.Send(OverlayInput.Select);
            var pending = rig.Controller.Snapshot();
            TestAssert.Equal("ON", pending.Rows[index].Value);
            TestAssert.Equal("boolean", pending.MenuOptionCallback!.Kind);
            TestAssert.Equal("pending", pending.MenuOptionCallback.Phase);
            // Disable refreshes immediately; the other three retain the previous drawn sample
            string copied = index == 0 ? "ON" : "OFF";
            TestAssert.Equal(copied, Paint(pending, index));
            TestAssert.Equal(before.MenuRevision!.Value + (index == 0 ? 1UL : 0UL), pending.MenuRevision!.Value);
            rig.Clock.Advance(TimeSpan.FromMilliseconds(179));
            TestAssert.Equal(copied, Paint(rig.Controller.Snapshot(), index));
            rig.Clock.Advance(TimeSpan.FromMilliseconds(1)); var refreshed = rig.Controller.Snapshot();
            TestAssert.Equal("ON", Paint(refreshed, index));
            TestAssert.Equal(pending.MenuRevision!.Value + 1, refreshed.MenuRevision!.Value);
            TestAssert.Equal("cooldown", refreshed.MenuOptionCallback!.Phase);
            rig.Clock.Advance(TimeSpan.FromMilliseconds(249)); rig.Send(OverlayInput.Back);
            TestAssert.Equal(MenuPage.Instructions, rig.Navigation.Page);
            rig.Clock.Advance(TimeSpan.FromMilliseconds(1)); rig.Send(OverlayInput.Back);
            TestAssert.Equal(MenuPage.Main, rig.Navigation.Page); TestAssert.Equal(0, rig.Session.Executed.Count);
        }
    }

    public static void AlternateClearsAdaptiveImmediatelyButRetainsBothOldIndicatorsUntil180Milliseconds() {
        var rig = Ready(); Select(rig, 3); rig.Send(OverlayInput.Select);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(430)); var adaptive = rig.Controller.Snapshot();
        TestAssert.Equal("ON", Paint(adaptive, 3)); Select(rig, 1); rig.Send(OverlayInput.Select);
        var pending = rig.Controller.Snapshot();
        TestAssert.True(pending.Appearance is { AlternateInstructions: true, AdaptiveInstructions: false, InstructionPosition: null },
            "Alternate clears the actual adaptive setting and saved anchor before its caller redraw");
        TestAssert.Equal("ON", pending.Rows[1].Value); TestAssert.Equal("OFF", pending.Rows[3].Value);
        TestAssert.Equal("OFF", Paint(pending, 1)); TestAssert.Equal("ON", Paint(pending, 3));
        rig.Clock.Advance(TimeSpan.FromMilliseconds(180)); var refreshed = rig.Controller.Snapshot();
        TestAssert.Equal("ON", Paint(refreshed, 1)); TestAssert.Equal("OFF", Paint(refreshed, 3));
    }

    public static void RejectedAdaptiveSelectionStaysSelectableReportsItsErrorAndWaits430Milliseconds() {
        var rig = Ready(); Select(rig, 1); rig.Send(OverlayInput.Select);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(430)); rig.Controller.Snapshot(); Select(rig, 3);
        var before = rig.Controller.Snapshot(); TestAssert.True(before.Rows[3].Enabled,
            "The source always declares Adaptive Position as a selectable BOOL");
        rig.Send(OverlayInput.Select); var rejected = rig.Controller.Snapshot();
        TestAssert.Equal(before.Appearance, rejected.Appearance);
        TestAssert.Equal("OFF", rejected.Rows[3].Value); TestAssert.Equal("OFF", Paint(rejected, 3));
        TestAssert.Equal("pending", rejected.MenuOptionCallback!.Phase);
        TestAssert.True(rejected.Notice.Contains("Adaptive Position", StringComparison.Ordinal)
            && rejected.Notice.Contains("Alternate", StringComparison.Ordinal), "The actual rejected callback must explain the source dependency");
        rig.Clock.Advance(TimeSpan.FromMilliseconds(180)); var refreshed = rig.Controller.Snapshot();
        TestAssert.Equal("OFF", Paint(refreshed, 3));
        TestAssert.Equal(before.MenuRevision!.Value + 1, refreshed.MenuRevision!.Value);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(250)); TestAssert.True(rig.Controller.Snapshot().MenuOptionCallback is null,
            "An error return does not remove or lengthen the caller's ordinary wait");
        TestAssert.Equal(0, rig.Session.Executed.Count);
    }

    public static void EachInstructionBooleanRestoresItsDefaultAfterTwoFreshOwnedSelections() {
        for (int index = 0; index < 4; index++) {
            var rig = Ready(); Select(rig, index); var before = rig.Controller.Snapshot();
            for (int turn = 0; turn < 2; turn++) {
                rig.Send(OverlayInput.Select); rig.Clock.Advance(TimeSpan.FromMilliseconds(430)); rig.Controller.Snapshot();
            }
            var after = rig.Controller.Snapshot(); TestAssert.Equal(before.Appearance, after.Appearance);
            TestAssert.Equal("OFF", Paint(after, index)); TestAssert.Equal(0, rig.Session.Executed.Count);
        }
    }

    public static void HiddenFocusAndReplacementActorCannotBorrowInstructionPaintOrCallback() {
        var rig = Ready(); Select(rig, 2); rig.Send(OverlayInput.Select);
        var hidden = StateFocusProjection.Apply(rig.Controller.Snapshot(), false);
        TestAssert.True(hidden.MenuBooleanPaint is null && hidden.MenuOptionCallback is null,
            "Hidden public state carries neither copied instruction paint nor its timer");
        rig.Clock.Advance(TimeSpan.FromMilliseconds(180)); TestAssert.Equal("ON", Paint(rig.Controller.Snapshot(), 2));
        rig.Session.Change(LocalPlayerLife.Alive, replaceOwner: true); var retired = rig.Controller.Snapshot();
        TestAssert.True(!retired.IsOpen && retired.MenuBooleanPaint is null && retired.MenuOptionCallback is null,
            "Replacement actor proofs cannot retain the old instruction display owner");
    }

    public static void ClockFailureRejectsInstructionChangesBeforeFlagsPaintOrWaitCommit() {
        var clock = new LocalOptionCallbackControllerTests.SwitchableClock();
        var rig = LocalOptionCallbackControllerTests.ReadyPage(MenuPage.Instructions, clock: clock);
        var before = rig.Controller.Snapshot(); clock.Fail = true;
        TestAssert.Throws<IOException>(() => rig.Send(OverlayInput.Select)); clock.Fail = false;
        var after = rig.Controller.Snapshot(); TestAssert.Equal(before.Appearance, after.Appearance);
        TestAssert.Equal(before.MenuBooleanPaint!.Revision, after.MenuBooleanPaint!.Revision);
        TestAssert.True(after.MenuOptionCallback is null, "Rejected clock preparation creates no callback");
    }

    public static void VisibilityDoubleRedrawReservesBothPaintEpochsBeforeChangingFlags() {
        var rig = LocalOptionCallbackControllerTests.ReadyPage(MenuPage.Instructions, paintRevision: ulong.MaxValue - 2);
        var before = rig.Controller.Snapshot();
        TestAssert.Throws<OverflowException>(() => rig.Send(OverlayInput.Select));
        var after = rig.Controller.Snapshot(); TestAssert.Equal(before.Appearance, after.Appearance);
        TestAssert.Equal(before.MenuRevision, after.MenuRevision);
        TestAssert.Equal(before.MenuBooleanPaint!.Revision, after.MenuBooleanPaint!.Revision);
        TestAssert.True(after.MenuOptionCallback is null, "A future second paint epoch is reserved before disabling instructions");
    }

    public static void VisibilityDoubleRedrawReservesBothMenuEpochsBeforeChangingFlags() {
        var rig = new ScopedOpeningFixture(navigation: new OverlayNavigation(0, ulong.MaxValue - 6));
        rig.Open(OverlayInput.OpenMain); rig.Clock.Advance(TimeSpan.FromMilliseconds(50)); rig.Controller.Snapshot();
        rig.Clock.Advance(TimeSpan.FromMilliseconds(550)); rig.Controller.Snapshot();
        rig.Navigation.OpenPage(MenuPage.Instructions); var before = rig.Controller.Snapshot();
        TestAssert.Equal(ulong.MaxValue - 2, before.MenuRevision!.Value);
        TestAssert.Throws<OverflowException>(() => rig.Send(OverlayInput.Select));
        TestAssert.Equal(before.Appearance, rig.Controller.Snapshot().Appearance);
        TestAssert.True(rig.Controller.Snapshot().MenuOptionCallback is null,
            "Immediate and scheduled redraws leave a further structural epoch before mutation");
    }
}
