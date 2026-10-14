using Apparition.Backend.Controller;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class MenuInitialOpenFailureTests
{
    public static void InvalidForeignStaleAndEitherFocusRejectionCannotAdmitOrReplayAnOpening()
    {
        var clock = new ManualTimeProvider();
        var controller = new BackendController(42, new SessionBoundary(), clock: clock);
        ulong sequence = 1;
        controller.Consume(new(sequence++, 1, 99, OverlayInput.Toggle, true), () => true);
        controller.Consume(new(sequence++, 1, 42, OverlayInput.Toggle, false), () => true);
        controller.Consume(new(sequence++, 1, 42, OverlayInput.Toggle, true), () => false);
        int focusReads = 0;
        controller.Consume(new(sequence++, 1, 42, OverlayInput.Toggle, true), () => ++focusReads == 1);
        var rejected = controller.Snapshot();
        TestAssert.True(!rejected.IsOpen && rejected.MenuOpenTransition is null && rejected.MenuOpening is null, "Both focus checks protect admission");
        controller.Consume(new(sequence - 1, 1, 42, OverlayInput.Toggle, true), () => true);
        TestAssert.Equal(rejected.NavigationRevision, controller.Snapshot().NavigationRevision);
        controller.Consume(new(sequence++, 1, 42, OverlayInput.Toggle, true), () => true);
        clock.Advance(TimeSpan.FromMilliseconds(50));
        TestAssert.Equal(1UL, controller.Snapshot().MenuOpening!.Revision);
    }

    public static void ClockNavigationAndEventExhaustionRejectBeforeLogicalAdmissionIncludingEmptyPages()
    {
        foreach (int mode in new[] { 0, 1, 2 })
        {
            var clock = new FaultClock();
            var session = new SessionBoundary();
            var nav = mode == 1 ? new OverlayNavigation(ulong.MaxValue, 0) : new OverlayNavigation();
            if (mode == 2)
            {
                nav.Open();
                nav.OpenPage(MenuPage.History);
                nav.Close();
            }
            var controller = new BackendController(42, session, nav, false, clock: clock, openingRevision: mode == 2 ? ulong.MaxValue : 0);
            var initial = controller.Snapshot();
            clock.Fail = mode == 0;
            if (mode == 0)
                TestAssert.Throws<InvalidOperationException>(() => controller.Consume(new(1, 1, 42, OverlayInput.Toggle, true), () => true));
            else
                TestAssert.Throws<OverflowException>(() => controller.Consume(new(1, 1, 42, OverlayInput.Toggle, true), () => true));
            clock.Fail = false;
            var rejected = controller.Snapshot();
            TestAssert.True(!rejected.IsOpen && rejected.MenuOpenTransition is null && rejected.MenuOpening is null, "A rejected admission owns no partial timeline");
            TestAssert.Equal(initial.NavigationRevision, rejected.NavigationRevision);
            TestAssert.Equal(initial.MenuRevision, rejected.MenuRevision);
            TestAssert.Equal(initial.Title, rejected.Title);
            TestAssert.Equal(1UL, rejected.LastSequence);
            TestAssert.Equal(0, session.Executed.Count);
        }
    }

    public static void RollbackBeforeRedrawRestartsRebuildAndAfterRedrawKeepsTheHudVisibleWithoutReplayingFade()
    {
        foreach (bool drawn in new[] { false, true })
        {
            var clock = new ManualTimeProvider();
            var controller = new BackendController(42, new SessionBoundary(), clock: clock);
            controller.Consume(new(1, 1, 42, OverlayInput.Toggle, true), () => true);
            clock.Advance(TimeSpan.FromMilliseconds(drawn ? 50 : 25));
            var before = controller.Snapshot();
            clock.RollBackTimestamp(TimeSpan.FromMilliseconds(10));
            var recovered = controller.Snapshot();
            TestAssert.Equal(drawn ? "cooldown" : "rebuild", MenuInitialOpenTests.Phase(recovered));
            TestAssert.True(recovered.MenuOpening is null, "Recovery never replays drawn opacity");
            TestAssert.Equal(before.MenuRevision, recovered.MenuRevision);
            TestAssert.Equal(before.NavigationRevision, recovered.NavigationRevision);
            clock.Advance(TimeSpan.FromMilliseconds(drawn ? 549 : 49));
            TestAssert.Equal(drawn ? "cooldown" : "rebuild", MenuInitialOpenTests.Phase(controller.Snapshot()));
            clock.Advance(TimeSpan.FromMilliseconds(1));
            var end = controller.Snapshot();
            if (drawn)
                TestAssert.True(end.MenuOpenTransition is null && end.MenuOpening is null, "Post-draw recovery retains only a finite 550 ms wait");
            else
                TestAssert.True(end.MenuOpenTransition?.Phase == "cooldown" && end.MenuOpening?.ElapsedMilliseconds == 0, "Pre-draw recovery waits a fresh 50 ms before first redraw");
        }
    }

    public static void ShutdownCancelsEveryOpeningStageAndAdministrativeRecoveryRemainsImmediatelyUsable()
    {
        foreach (bool drawn in new[] { false, true })
        {
            var clock = new ManualTimeProvider();
            var controller = new BackendController(42, new SessionBoundary(), clock: clock);
            controller.Consume(new(1, 1, 42, OverlayInput.Toggle, true), () => true);
            if (drawn)
            {
                clock.Advance(TimeSpan.FromMilliseconds(50));
                controller.Snapshot();
            }
            controller.RequestShutdown();
            var closed = controller.ClosedState();
            TestAssert.True(!closed.IsOpen && closed.MenuOpenTransition is null && closed.MenuOpening is null, "Cleanup cancels local presentation ownership");
            clock.Advance(TimeSpan.FromSeconds(5));
            TestAssert.True(controller.ReadMenuPublicationPhase() is null, "Clock polling cannot resurrect a retired visit");
        }
        var retryClock = new ManualTimeProvider();
        var retry = new BackendController(42, new SessionBoundary(), clock: retryClock);
        retry.RequestShutdown();
        retry.RetryAfterShutdownFailure("Controlled cleanup retry");
        var immediate = retry.Snapshot();
        TestAssert.True(immediate.IsOpen && immediate.MenuOpenTransition is null && immediate.MenuOpening is null, "Administrative recovery never delays the Exit retry");
        retry.Consume(new(1, 1, 42, OverlayInput.Up, true), () => true);
        retry.Consume(new(2, 1, 42, OverlayInput.Select, true), () => true);
        TestAssert.True(retry.ExitRequested, "The fresh retry Exit remains actionable");
    }

    public static void FailedFreshRowCounterCommitCannotConsumeRedrawOrAdvanceItsPreparedClock()
    {
        var session = new SessionBoundary();
        var nav = new OverlayNavigation(0, ulong.MaxValue - 5);
        nav.Open();
        nav.OpenPage(MenuPage.Players);
        nav.SetRows(OverlayMenuBuilder.Build(MenuPage.Players, session.State).Rows);
        nav.Close();
        var clock = new ScriptedClock();
        var controller = new BackendController(42, session, nav, false, clock: clock);
        controller.Consume(new(1, 1, 42, OverlayInput.Toggle, true), () => true);
        var admitted = controller.Snapshot();
        session.State = session.State with
        {
            Players = new[] { new OverlayPlayer(1, "Friend") }
        };
        clock.Samples.Enqueue(50);
        clock.Samples.Enqueue(75);
        TestAssert.Throws<OverflowException>(() => controller.Snapshot());
        clock.Samples.Enqueue(60);
        TestAssert.Equal("open:ready", controller.ReadMenuPublicationPhase());
        session.State = session.State with
        {
            Players = Array.Empty<OverlayPlayer>()
        };
        var drawn = controller.Snapshot();

        TestAssert.Equal("cooldown", drawn.MenuOpenTransition?.Phase);
        TestAssert.Equal(admitted.MenuRevision, drawn.MenuRevision);
        TestAssert.True(drawn.MenuOpening is null, "Restored one-row data commits without another event or counter");
    }

    private sealed class ScriptedClock : TimeProvider
    {
        internal Queue<long> Samples { get; } = new(); private long timestamp;
        public override long TimestampFrequency => 1000;
        public override long GetTimestamp()
        {
            if (Samples.TryDequeue(out long next))
                timestamp = next;
            return timestamp;
        }
    }

    internal sealed class FaultClock : TimeProvider
    {
        internal ManualTimeProvider Time { get; } = new(); internal bool Fail
        {
            get; set;
        }
        public override long TimestampFrequency => Time.TimestampFrequency;
        public override long GetTimestamp() => Fail ? throw new InvalidOperationException("Owned clock unavailable") : Time.GetTimestamp();
    }
}
