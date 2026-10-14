using Apparition.Backend.Controller;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class MenuOpeningFailureTests {
    public static void OpeningRevisionExhaustionRejectsChangedDesignButSameDesignNeedsNoNewEvent() {
        var session = new SessionBoundary(); var navigation = new OverlayNavigation(); navigation.Open(); navigation.OpenPage(MenuPage.Preferences);
        navigation.SetRows(OverlayMenuBuilder.Build(MenuPage.Preferences, session.State, choiceControlsAvailable: true).Rows);
        var clock = new ManualTimeProvider(); var controller = new BackendController(42, session, navigation, false, choiceControlsAvailable: true, clock: clock, openingRevision: ulong.MaxValue);
        var initial = controller.Snapshot(); controller.Consume(new(1, 1, 42, OverlayInput.NextChoice, true), () => true);
        TestAssert.Throws<OverflowException>(() => controller.Consume(new(2, 1, 42, OverlayInput.Select, true), () => true));
        TestAssert.Equal(initial.Appearance, controller.Snapshot().Appearance); TestAssert.Equal(initial.MenuRevision, controller.Snapshot().MenuRevision);
        TestAssert.True(controller.Snapshot().MenuOpening is null && controller.Snapshot().MenuDesignTransition is null, "Failed admission cannot retain either timeline");
        controller.Consume(new(2, 1, 42, OverlayInput.Select, true), () => true); TestAssert.Equal(2UL, controller.LastSequence);
        controller.Consume(new(3, 1, 42, OverlayInput.PreviousChoice, true), () => true); controller.Consume(new(4, 1, 42, OverlayInput.Select, true), () => true);
        TestAssert.Equal("cooldown", controller.Snapshot().MenuDesignTransition?.Phase); TestAssert.True(controller.Snapshot().MenuOpening is null, "An unchanged design consumes no opening revision");
    }

    public static void SharedClockSampleArmsTransactionallyAndRecoveryCannotRevertOrResurrectDrawnRows() {
        var clock = new CountingClock(); var rig = new MenuDesignTests.Rig(clock: clock); rig.Open(); rig.Stage("Classic"); var before = rig.Current();
        clock.Fail = true; TestAssert.Throws<InvalidOperationException>(() => rig.Send(OverlayInput.Select)); clock.Fail = false;
        TestAssert.Equal(before.Appearance, rig.Current().Appearance); TestAssert.True(rig.Current().MenuOpening is null, "Clock admission fails before actual flags or either timeline");
        int reads = clock.Reads; rig.Send(OverlayInput.Select); TestAssert.Equal(reads + 1, clock.Reads);
        clock.Time.Advance(TimeSpan.FromMilliseconds(25)); rig.Controller.ReadMenuPublicationPhase(); clock.Time.RollBackTimestamp(TimeSpan.FromMilliseconds(10));
        rig.Controller.ReadMenuPublicationPhase(); clock.Time.Advance(TimeSpan.FromMilliseconds(49)); TestAssert.True(rig.Current().MenuOpening is null, "Clock recovery before redraw restarts both armed states coherently");
        clock.Time.Advance(TimeSpan.FromMilliseconds(1)); reads = clock.Reads; rig.Controller.ReadMenuPublicationPhase(); TestAssert.Equal(reads + 1, clock.Reads);
        var drawn = rig.Current(); TestAssert.Equal(0, drawn.MenuOpening!.ElapsedMilliseconds); TestAssert.Equal(before.MenuRevision + 1, drawn.MenuRevision);
        clock.Time.Advance(TimeSpan.FromMilliseconds(25)); rig.Current(); clock.Time.RollBackTimestamp(TimeSpan.FromMilliseconds(10)); var recovered = rig.Current();
        TestAssert.True(recovered.MenuOpening is null, "Post-redraw clock failure safely completes opacity rather than returning invisible rows"); TestAssert.Equal("Classic", recovered.Appearance!.Design); TestAssert.Equal(drawn.MenuRevision, recovered.MenuRevision);
        clock.Time.Advance(TimeSpan.FromMilliseconds(200)); TestAssert.True(rig.Current().MenuOpening is null && rig.Current().MenuDesignTransition is null, "Recovery retains only the finite Design cooldown"); rig.NoGameWork();
    }

    private sealed class CountingClock : TimeProvider, IManualClockFixture {
        public ManualTimeProvider Time { get; } = new(); internal int Reads { get; private set; } internal bool Fail { get; set; }
        public override long TimestampFrequency => Time.TimestampFrequency;
        public override long GetTimestamp() { Reads++; if (Fail) throw new InvalidOperationException("Owned clock unavailable"); return Time.GetTimestamp(); }
    }
}
