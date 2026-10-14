using Apparition.Backend.Controller;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class MenuDesignFailureTests
{
    public static void ClockAndCounterAdmissionFaultsLeaveActualDesignFlagsAndTimerUnchanged()
    {
        var faultClock = new ThrowingClock(); var fault = new MenuDesignTests.Rig(clock: faultClock); fault.Open(); fault.Stage("Classic"); var before = fault.Current(); faultClock.Fail = true;
        TestAssert.Throws<InvalidOperationException>(() => fault.Send(OverlayInput.Select)); fault.Controller.RequestShutdown();
        TestAssert.Equal(before.Appearance, fault.Controller.ClosedState().Appearance);
        var session = new SessionBoundary(); var navigation = new OverlayNavigation(0, ulong.MaxValue - 4);
        navigation.Open(); navigation.OpenPage(MenuPage.Preferences);
        navigation.SetRows(OverlayMenuBuilder.Build(MenuPage.Preferences, session.State, choiceControlsAvailable: true).Rows); navigation.InvalidatePresentation();
        var controller = new BackendController(42, session, navigation, false, choiceControlsAvailable: true); var initial = controller.Snapshot();
        controller.Consume(new(1, 1, 42, OverlayInput.NextChoice, true), () => true);
        TestAssert.Throws<OverflowException>(() => controller.Consume(new(2, 1, 42, OverlayInput.Select, true), () => true));
        controller.RequestShutdown(); var closed = controller.Snapshot(); TestAssert.Equal(initial.Appearance, closed.Appearance);
        TestAssert.True(closed.MenuDesignTransition is null, "A rejected future redraw cannot retain a hidden timer");
    }

    public static void SignedElapsedOverflowRecoversBeforeAndAfterRebuildWithoutRepeatingInvalidation()
    {
        foreach (bool applied in new[] { false, true })
        {
            var rig = new MenuDesignTests.Rig(); rig.Clock.RollBackTimestamp(TimeSpan.MaxValue); rig.Clock.RollBackTimestamp(TimeSpan.FromTicks(1));
            rig.Open(); rig.Stage("Classic"); var initial = rig.Current(); rig.Send(OverlayInput.Select);
            if (applied) { rig.Clock.Advance(TimeSpan.FromMilliseconds(50)); rig.Current(); }
            rig.Clock.Advance(TimeSpan.FromTicks(long.MaxValue - (applied ? TimeSpan.FromMilliseconds(50).Ticks : 0))); rig.Clock.Advance(TimeSpan.FromTicks(1)); var recovered = rig.Current();
            TestAssert.Equal(applied ? "cooldown" : "rebuild", recovered.MenuDesignTransition?.Phase);
            TestAssert.Equal(applied ? "Classic" : "Apparition", recovered.Appearance!.Design);
            rig.Clock.Advance(TimeSpan.FromMilliseconds(applied ? 200 : 250));
            TestAssert.True(rig.Current().MenuDesignTransition is null, "Recovered stages keep a finite remaining lifetime");
            TestAssert.Equal(initial.MenuRevision + 1, rig.Current().MenuRevision); rig.NoGameWork();
        }
    }

    public static void LateObservationRefreshesExactlyOnceAndSameDesignRollbackRestartsItsWholeCooldown()
    {
        var late = new MenuDesignTests.Rig(); late.Open(); late.Stage("Basic"); var initial = late.Current(); late.Send(OverlayInput.Select);
        // A quiet owner may first observe the changed design after both waits have elapsed
        late.Clock.Advance(TimeSpan.FromMilliseconds(1000)); late.Controller.ReadMenuPublicationPhase(); TestAssert.True(late.Current().MenuDesignTransition is null, "A late completed Design visit retires its input timer");
        TestAssert.Equal("Basic", late.Current().Appearance!.Design); TestAssert.Equal(initial.MenuRevision + 1, late.Current().MenuRevision);
        late.Controller.ReadMenuPublicationPhase(); TestAssert.Equal(initial.MenuRevision + 1, late.Current().MenuRevision); late.NoGameWork();
        var same = new MenuDesignTests.Rig(); same.Open(); var before = same.Current(); same.Send(OverlayInput.Select);
        same.Clock.Advance(TimeSpan.FromMilliseconds(200)); same.Current(); same.Clock.RollBackTimestamp(TimeSpan.FromMilliseconds(50));
        TestAssert.Equal("cooldown", same.Current().MenuDesignTransition?.Phase);
        same.Clock.Advance(TimeSpan.FromMilliseconds(249)); TestAssert.Equal("cooldown", same.Current().MenuDesignTransition?.Phase);
        same.Clock.Advance(TimeSpan.FromMilliseconds(1)); TestAssert.True(same.Current().MenuDesignTransition is null, "Same-design recovery retains the full250ms monitor cooldown");
        TestAssert.Equal(before.Appearance, same.Current().Appearance); TestAssert.Equal(before.MenuRevision, same.Current().MenuRevision); same.NoGameWork();
    }

    public static void RetainedDesignAndDraftSurviveOtherLocalModalVisitsWithoutSessionWork()
    {
        var rig = new MenuDesignTests.Rig(); rig.Open(); rig.Stage("Native"); rig.Send(OverlayInput.Select); rig.Clock.Advance(TimeSpan.FromMilliseconds(250)); rig.Current();
        rig.Send(OverlayInput.Toggle); MenuOpenTestHarness.Settle(rig.Controller, rig.Clock); rig.Send(OverlayInput.Toggle); MenuOpenTestHarness.Settle(rig.Controller, rig.Clock); TestAssert.Equal("Native", rig.Current().Appearance!.Design);
        rig.Send(OverlayInput.Back); rig.Select("Menu Credits"); TestAssert.Equal("Native", rig.Current().Appearance!.Design);
        rig.Send(OverlayInput.Toggle); MenuOpenTestHarness.Settle(rig.Controller, rig.Clock); rig.Send(OverlayInput.Toggle); MenuOpenTestHarness.Settle(rig.Controller, rig.Clock); rig.Select("Menu Preferences");
        TestAssert.Equal("Native", rig.Current().Rows[0].Value); TestAssert.Equal("Native", rig.Current().Appearance!.Design); rig.NoGameWork();
    }

    private sealed class ThrowingClock : TimeProvider, IManualClockFixture
    {
        public ManualTimeProvider Time { get; } = new();
        internal bool Fail { get; set; }
        public override long TimestampFrequency => Time.TimestampFrequency;
        public override long GetTimestamp() => Fail ? throw new InvalidOperationException("Owned clock unavailable") : Time.GetTimestamp();
    }
}
