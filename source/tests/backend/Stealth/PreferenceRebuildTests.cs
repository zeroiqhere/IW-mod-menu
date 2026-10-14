using Apparition.Backend.Controller;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class PreferenceRebuildTests {
    public static void CounterOnAndOffRetainPageCursorAndCaptureTheActualOpeningCapacity() {
        var rig = new MenuDesignTests.Rig();
        rig.Open();
        foreach (bool enabled in new[] { true, false }) {
            rig.Highlight("Option Counter");
            var before = rig.Current();
            rig.Send(OverlayInput.Select);
            TestAssert.Equal(before.Appearance, rig.Current().Appearance);
            rig.Clock.Advance(TimeSpan.FromMilliseconds(50));
            var drawn = rig.Current();
            TestAssert.Equal(enabled, drawn.Appearance!.OptionCounter);
            TestAssert.Equal("Menu Preferences", drawn.Title);
            TestAssert.Equal(before.SelectedIndex, drawn.SelectedIndex);
            TestAssert.Equal(before.NavigationRevision, drawn.NavigationRevision);
            TestAssert.Equal(before.MenuRevision + 1, drawn.MenuRevision);
            TestAssert.Equal(0, drawn.MenuOpening!.ElapsedMilliseconds);
            rig.Clock.Advance(TimeSpan.FromMilliseconds(200));
            TestAssert.True(rig.Current().MenuPreferenceRebuild is null, "Input cooldown ends independently of opacity");
            int duration = enabled ? 700 : 900;
            rig.Clock.Advance(TimeSpan.FromMilliseconds(duration - 201));
            TestAssert.Equal(duration - 1, rig.Current().MenuOpening!.ElapsedMilliseconds);
            rig.Clock.Advance(TimeSpan.FromMilliseconds(1));
            TestAssert.True(rig.Current().MenuOpening is null, "The last visible ordinal completes once");
        }
        rig.NoGameWork();
    }

    public static void LockedInputsAreAcknowledgedWithoutReplayAndShutdownCancelsOnlyPresentation() {
        foreach (int elapsed in new[] { 0, 50, 249 }) {
            var rig = new MenuDesignTests.Rig();
            rig.Open();
            rig.Select("Option Counter");
            rig.Clock.Advance(TimeSpan.FromMilliseconds(elapsed));
            var before = rig.Current();
            foreach (var input in Enum.GetValues<OverlayInput>()) rig.Send(input);
            TestAssert.Equal(before.NavigationRevision, rig.Current().NavigationRevision);
            TestAssert.Equal(before.Appearance, rig.Current().Appearance);
            rig.Controller.RequestShutdown();
            var closed = rig.Controller.ClosedState();
            TestAssert.True(!closed.IsOpen && closed.MenuPreferenceRebuild is null && closed.MenuOpening is null, "Cleanup cancels every local timeline");
            TestAssert.True(closed.Appearance!.OptionCounter, "The committed Counter value survives cleanup");
            rig.Clock.Advance(TimeSpan.FromSeconds(5));
            TestAssert.Equal(closed.MenuRevision, rig.Current().MenuRevision);
            rig.NoGameWork();
        }
    }

    public static void LateRedrawStartsAtZeroAndClockRollbackRestartsOnlyTheRemainingStage() {
        var late = new MenuDesignTests.Rig();
        late.Open();
        late.Select("Option Counter");
        late.Clock.Advance(TimeSpan.FromMilliseconds(1000));
        var drawn = late.Current();
        TestAssert.True(drawn.MenuPreferenceRebuild is null && drawn.MenuOpening!.ElapsedMilliseconds == 0, "A late observation cannot age an unseen draw");
        late.NoGameWork();
        foreach (bool applied in new[] { false, true }) {
            var rig = new MenuDesignTests.Rig();
            rig.Open();
            rig.Select("Option Counter");
            if (applied) rig.Clock.Advance(TimeSpan.FromMilliseconds(50));
            var before = rig.Current();
            rig.Clock.RollBackTimestamp(TimeSpan.FromMilliseconds(1));
            var recovered = rig.Current();
            TestAssert.Equal(applied ? "cooldown" : "rebuild", recovered.MenuPreferenceRebuild!.Phase);
            TestAssert.Equal(before.Appearance, recovered.Appearance);
            TestAssert.Equal(before.MenuRevision, recovered.MenuRevision);
            if (applied) TestAssert.True(recovered.MenuOpening is null, "Clock recovery cannot restart already-drawn opacity");
            rig.Clock.Advance(TimeSpan.FromMilliseconds(applied ? 200 : 250));
            TestAssert.True(rig.Current().MenuPreferenceRebuild is null, "Recovery keeps a bounded remaining wait");
            TestAssert.Equal(before.MenuRevision + (applied ? 0UL : 1UL), rig.Current().MenuRevision);
            rig.NoGameWork();
        }
    }

    public static void SignedElapsedOverflowAndUnknownCauseCannotRevertOrPartiallyStartARebuild() {
        var clock = new ManualTimeProvider();
        var state = LocalMenuPreferenceRebuildState.Start(MenuAppearance.Default, MenuPreferenceRebuildCause.OptionCounter, clock);
        TestAssert.Equal(MenuPreferenceRebuildCause.OptionCounter, state.Cause);
        var stealth = LocalMenuPreferenceRebuildState.Start(MenuAppearance.Default, MenuPreferenceRebuildCause.StealthUI, clock);
        TestAssert.Equal(MenuPreferenceRebuildCause.StealthUI, stealth.Cause);
        TestAssert.Throws<ArgumentOutOfRangeException>(() => LocalMenuPreferenceRebuildState.Start(MenuAppearance.Default, (MenuPreferenceRebuildCause)99, clock));
        foreach (bool applied in new[] { false, true }) {
            var rig = new MenuDesignTests.Rig();
            rig.Clock.RollBackTimestamp(TimeSpan.MaxValue);
            rig.Clock.RollBackTimestamp(TimeSpan.FromTicks(1));
            rig.Open();
            rig.Select("Option Counter");
            if (applied) rig.Clock.Advance(TimeSpan.FromMilliseconds(50));
            var before = rig.Current();
            rig.Clock.Advance(TimeSpan.FromTicks(long.MaxValue - (applied ? TimeSpan.FromMilliseconds(50).Ticks : 0)));
            rig.Clock.Advance(TimeSpan.FromTicks(1));
            var recovered = rig.Current();
            TestAssert.Equal(applied ? "cooldown" : "rebuild", recovered.MenuPreferenceRebuild!.Phase);
            TestAssert.Equal(before.Appearance, recovered.Appearance);
            TestAssert.Equal(before.MenuRevision, recovered.MenuRevision);
            rig.NoGameWork();
        }
    }
}
