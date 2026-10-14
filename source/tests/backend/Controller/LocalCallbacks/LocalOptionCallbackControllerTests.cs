using Apparition.Backend.Ipc;
using Apparition.Backend.Session;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Backend.Tests;

// Real owner-loop actions use a connected inert session and an explicit source clock
internal static class LocalOptionCallbackControllerTests {
    internal static ScopedOpeningFixture ReadyPage(MenuPage page, ulong paintRevision = 0, TimeProvider? clock = null,
        LocalMenuPreferenceScope? preferenceScope = null) {
        var rig = new ScopedOpeningFixture(clock: clock, booleanPaintRevision: paintRevision,
            choiceControlsAvailable: true, preferenceScope: preferenceScope);
        rig.Open(OverlayInput.OpenMain);
        if (clock is SwitchableClock external) {
            external.Advance(50); rig.Controller.Snapshot(); external.Advance(550);
        } else {
            rig.Clock.Advance(TimeSpan.FromMilliseconds(50)); rig.Controller.Snapshot();
            rig.Clock.Advance(TimeSpan.FromMilliseconds(550));
        }
        rig.Controller.Snapshot(); rig.Navigation.OpenPage(page); rig.Controller.Snapshot();
        return rig;
    }
    internal static void SelectIndex(ScopedOpeningFixture rig, int index) {
        // The controller retains real row identities and observes every accepted cursor movement
        for (int moved = 0; moved < 128 && rig.Navigation.SelectedIndex != index; moved++) rig.Send(OverlayInput.Down);
        TestAssert.Equal(index, rig.Navigation.SelectedIndex);
    }
    private static string Paint(StateDocument frame, int index) {
        TestAssert.True(frame.MenuBooleanPaint is not null, "The actual palette must publish its copied indicator values");
        return frame.MenuBooleanPaint!.Rows.Single(row => row.Index == index).Value;
    }

    public static void FixedThemeAppliesImmediatelyButCheckedIndicatorsRefreshOnlyAt180Milliseconds() {
        var rig = ReadyPage(MenuPage.ThemeColors); var before = rig.Controller.Snapshot();
        TestAssert.Equal("OFF", Paint(before, 0)); TestAssert.Equal("ON", Paint(before, 6));
        rig.Send(OverlayInput.Select); var pending = rig.Controller.Snapshot();
        TestAssert.Equal("#FF0000", pending.Accent); TestAssert.Equal("ON", pending.Rows[0].Value);
        TestAssert.Equal("OFF", Paint(pending, 0)); TestAssert.Equal("ON", Paint(pending, 6));
        TestAssert.Equal("boolean", pending.MenuOptionCallback!.Kind);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(179));
        TestAssert.Equal("OFF", Paint(rig.Controller.Snapshot(), 0));
        rig.Clock.Advance(TimeSpan.FromMilliseconds(1)); var refreshed = rig.Controller.Snapshot();
        TestAssert.Equal("ON", Paint(refreshed, 0)); TestAssert.Equal("OFF", Paint(refreshed, 6));
        TestAssert.Equal(before.MenuRevision!.Value + 1, refreshed.MenuRevision!.Value);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(249)); rig.Send(OverlayInput.Down);
        TestAssert.Equal(0, rig.Navigation.SelectedIndex);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(1)); rig.Send(OverlayInput.Down);
        TestAssert.Equal(1, rig.Navigation.SelectedIndex); TestAssert.Equal(0, rig.Session.Executed.Count);
    }

    public static void SameFixedColorAndActiveRainbowReselectionKeepTheFullBooleanCallerWait() {
        foreach (int index in new[] { 6, 36 }) {
            var rig = ReadyPage(MenuPage.ThemeColors); SelectIndex(rig, index);
            rig.Send(OverlayInput.Select); rig.Clock.Advance(TimeSpan.FromMilliseconds(430)); rig.Controller.Snapshot();
            bool rainbow = index == 36;
            var applied = rig.Controller.Snapshot();
            rig.Send(OverlayInput.Select); var repeated = rig.Controller.Snapshot();
            TestAssert.Equal(applied.Accent, repeated.Accent); TestAssert.Equal(rainbow, repeated.SmoothRainbowTheme);
            TestAssert.Equal("pending", repeated.MenuOptionCallback!.Phase);
            rig.Clock.Advance(TimeSpan.FromMilliseconds(429)); rig.Send(OverlayInput.Back);
            TestAssert.Equal(MenuPage.ThemeColors, rig.Navigation.Page);
            rig.Clock.Advance(TimeSpan.FromMilliseconds(1)); TestAssert.True(rig.Controller.Snapshot().MenuOptionCallback is null,
                "A valid no-op source BOOL selection must resume at its ordinary430ms deadline");
        }
    }

    public static void SourceSlidersUse250MillisecondsIncludingSameCandidateWithoutBooleanRedraw() {
        foreach (int index in new[] { 1, 2, 3 }) {
            var rig = ReadyPage(MenuPage.Preferences); SelectIndex(rig, index);
            var before = rig.Controller.Snapshot(); rig.Send(OverlayInput.Select); var waiting = rig.Controller.Snapshot();
            TestAssert.Equal("plain", waiting.MenuOptionCallback!.Kind); TestAssert.Equal("cooldown", waiting.MenuOptionCallback.Phase);
            TestAssert.True(waiting.MenuPreferenceRefresh is null && waiting.MenuDesignTransition is null,
                "Generic source slider waits must not borrow a specialized boolean/design timeline");
            TestAssert.Equal(before.MenuRevision!.Value, waiting.MenuRevision!.Value);
            rig.Clock.Advance(TimeSpan.FromMilliseconds(249)); rig.Send(OverlayInput.Down);
            TestAssert.Equal(index, rig.Navigation.SelectedIndex);
            rig.Clock.Advance(TimeSpan.FromMilliseconds(1)); rig.Send(OverlayInput.Down);
            TestAssert.Equal(index + 1, rig.Navigation.SelectedIndex); TestAssert.Equal(0, rig.Session.Executed.Count);
        }
    }

    public static void ChangedDisplayCandidateCommitsBeforeItsIndependentPlainWait() {
        var rig = ReadyPage(MenuPage.Preferences); SelectIndex(rig, 1);
        rig.StageChoice(OverlayInput.NextChoice); rig.Send(OverlayInput.Select); var frame = rig.Controller.Snapshot();
        TestAssert.Equal("Text", frame.Appearance!.BooleanDisplay);
        TestAssert.Equal("plain", frame.MenuOptionCallback!.Kind);
        TestAssert.True(frame.Rows[1].Value.Contains("Text", StringComparison.Ordinal), "Applied source slider state must remain visible");
        rig.Clock.Advance(TimeSpan.FromMilliseconds(250)); TestAssert.True(rig.Controller.Snapshot().MenuOptionCallback is null,
            "Immediate slider commit cannot lengthen its caller wait");
    }

    public static void RejectedBoxesCandidateKeepsTextAndStillOwnsThePlainCallerWait() {
        var rig = ReadyPage(MenuPage.Preferences); SelectIndex(rig, 10);
        rig.Send(OverlayInput.Select); rig.Clock.Advance(TimeSpan.FromMilliseconds(430)); rig.Controller.Snapshot();
        TestAssert.True(rig.Controller.Snapshot().Appearance!.StealthUI, "The controlled source visit must enable Stealth first");
        SelectIndex(rig, 1); rig.Send(OverlayInput.Select); var rejected = rig.Controller.Snapshot();
        TestAssert.Equal("Text", rejected.Appearance!.BooleanDisplay);
        TestAssert.Equal("plain", rejected.MenuOptionCallback!.Kind);
        TestAssert.True(rejected.Notice.Contains("Boxes cannot be applied", StringComparison.Ordinal),
            "Actual rejected source choice feedback remains visible while ordinary controls are paused");
        rig.Clock.Advance(TimeSpan.FromMilliseconds(249)); rig.Send(OverlayInput.Down);
        TestAssert.Equal(1, rig.Navigation.SelectedIndex);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(1)); rig.Send(OverlayInput.Down);
        TestAssert.Equal(2, rig.Navigation.SelectedIndex);
    }

    public static void ResetPositionOwnsThePlainCallerWaitEvenWhenAlreadyAtItsDefault() {
        var rig = ReadyPage(MenuPage.Instructions); SelectIndex(rig, 5);
        var before = rig.Controller.Snapshot(); rig.Send(OverlayInput.Select); var frame = rig.Controller.Snapshot();
        TestAssert.True(frame.Appearance!.InstructionPosition is null, "Reset keeps the source default anchor");
        TestAssert.Equal("plain", frame.MenuOptionCallback!.Kind);
        TestAssert.Equal(before.MenuRevision!.Value, frame.MenuRevision!.Value);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(249)); rig.Send(OverlayInput.Back);
        TestAssert.Equal(MenuPage.Instructions, rig.Navigation.Page);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(1)); rig.Send(OverlayInput.Back);
        TestAssert.Equal(MenuPage.Main, rig.Navigation.Page);
    }

    public static void HiddenFocusAndReplacementActorCannotReplayOrBorrowLocalPaletteCallbacks() {
        var rig = ReadyPage(MenuPage.ThemeColors); rig.Send(OverlayInput.Select); var owner = rig.Controller.Snapshot();
        var hidden = StateFocusProjection.Apply(owner, false);
        TestAssert.True(hidden.MenuOptionCallback is null && hidden.MenuBooleanPaint is null && hidden.Serialize().Length > 0,
            "Focus projection hides both local callback and copied palette metadata");
        rig.Clock.Advance(TimeSpan.FromMilliseconds(180));
        TestAssert.Equal("cooldown", rig.Controller.Snapshot().MenuOptionCallback!.Phase);
        rig.Session.Change(LocalPlayerLife.Alive, replaceOwner: true); var retired = rig.Controller.Snapshot();
        TestAssert.True(!retired.IsOpen && retired.MenuOptionCallback is null && retired.MenuBooleanPaint is null,
            "A replacement actor cannot own the old local color display or wait");
        TestAssert.Equal(0, rig.Session.Executed.Count);
    }

    public static void FailedLocalCallbackPreparationLeavesTheColorAndIndicatorsUnchanged() {
        var clock = new SwitchableClock(); var rig = ReadyPage(MenuPage.ThemeColors, clock: clock);
        var before = rig.Controller.Snapshot(); clock.Fail = true;
        TestAssert.Throws<IOException>(() => rig.Send(OverlayInput.Select)); clock.Fail = false;
        var after = rig.Controller.Snapshot();
        TestAssert.Equal(before.Accent, after.Accent); TestAssert.True(after.MenuOptionCallback is null, "Failed clock admission commits no local wait");
        TestAssert.Equal(before.MenuBooleanPaint!.Revision, after.MenuBooleanPaint!.Revision);
        var exhausted = ReadyPage(MenuPage.ThemeColors, paintRevision: ulong.MaxValue - 1);
        var unchanged = exhausted.Controller.Snapshot();
        TestAssert.Throws<OverflowException>(() => exhausted.Send(OverlayInput.Select));
        TestAssert.Equal(unchanged.Accent, exhausted.Controller.Snapshot().Accent);
        TestAssert.True(exhausted.Controller.Snapshot().MenuOptionCallback is null, "Counter admission fails before a local theme mutation");
    }

    internal sealed class SwitchableClock : TimeProvider {
        private long timestamp;
        public bool Fail { get; set; }
        public override long TimestampFrequency => 1000;
        public void Advance(long milliseconds) => timestamp += milliseconds;
        public override long GetTimestamp() => Fail ? throw new IOException("Controlled local callback clock failure") : timestamp;
    }
}
