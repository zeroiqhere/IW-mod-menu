using Apparition.Backend.Controller;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class MenuInitialOpenRosterTests
{
    public static void RedrawUsesCurrentPlayerIdentityAndClampsAShrunkRetainedCursorBeforeBindingFade()
    {
        foreach (bool shrink in new[] { false, true })
        {
            var session = new SessionBoundary();
            session.State = session.State with
            {
                Players = new[] { new OverlayPlayer(1, "Friend"), new OverlayPlayer(2, "Other") }
            };
            var nav = new OverlayNavigation();
            nav.Open();
            nav.OpenPage(MenuPage.Players);
            nav.SetRows(OverlayMenuBuilder.Build(MenuPage.Players, session.State).Rows);
            nav.SelectIndex(2);
            nav.Close();
            var clock = new ManualTimeProvider();
            var controller = new BackendController(42, session, nav, false, clock: clock);
            controller.Consume(new(1, 1, 42, OverlayInput.Toggle, true), () => true);
            var admitted = controller.Snapshot();
            clock.Advance(TimeSpan.FromMilliseconds(50));
            session.State = session.State with
            {
                Players = shrink ? Array.Empty<OverlayPlayer>()
                : new[] { new OverlayPlayer(1, "Friend"), new OverlayPlayer(2, "Replacement") }
            };
            var drawn = controller.Snapshot();
            TestAssert.Equal("cooldown", drawn.MenuOpenTransition?.Phase);
            TestAssert.Equal(admitted.MenuRevision + 1, drawn.MenuRevision);
            TestAssert.Equal(shrink ? 0 : 2, drawn.SelectedIndex);
            if (shrink)
            {
                TestAssert.True(drawn.MenuOpening is null, "A shrunk one-row menu has no obsolete duration");
                TestAssert.Equal(admitted.NavigationRevision + 1, drawn.NavigationRevision);
            }
            else
            {
                TestAssert.Equal("Replacement", drawn.Rows[2].Label);
                TestAssert.Equal(0, drawn.MenuOpening!.ElapsedMilliseconds);
                TestAssert.Equal(admitted.NavigationRevision, drawn.NavigationRevision);
                // The copied navigation row carries the fresh selection identity, even while disabled
                TestAssert.Equal("Replacement", nav.Rows[2].Action.ExpectedName);
            }
            TestAssert.Equal(0, session.Executed.Count);
        }
    }

    public static void CapabilityAndBusyUpdatesRetainFadeButActualIdentityChangesCancelWithoutExtendingCooldown()
    {
        var session = new SessionBoundary();
        session.State = session.State with
        {
            Connected = true,
            CanSelectPlayers = true,
            Players = new[] { new OverlayPlayer(1, "Friend"), new OverlayPlayer(2, "Other") }
        };
        var nav = new OverlayNavigation();
        nav.Open();
        nav.OpenPage(MenuPage.Players);
        nav.Close();
        var clock = new ManualTimeProvider();
        var controller = new BackendController(42, session, nav, false, clock: clock);
        controller.Consume(new(1, 1, 42, OverlayInput.Toggle, true), () => true);
        clock.Advance(TimeSpan.FromMilliseconds(50));
        var drawn = controller.Snapshot();
        clock.Advance(TimeSpan.FromMilliseconds(100));
        session.State = session.State with
        {
            Busy = true,
            CanSelectPlayers = false
        };
        var busy = controller.Snapshot();
        TestAssert.Equal(drawn.MenuRevision, busy.MenuRevision);
        TestAssert.Equal(drawn.MenuOpening!.Revision, busy.MenuOpening!.Revision);
        TestAssert.Equal(100, busy.MenuOpening.ElapsedMilliseconds);
        TestAssert.True(busy.Rows.All(row => !row.Enabled), "A retained fade never grants unavailable gameplay capabilities");
        session.State = session.State with
        {
            Players = new[] { new OverlayPlayer(1, "Replacement"), new OverlayPlayer(2, "Other") }
        };
        var changed = controller.Snapshot();
        TestAssert.True(changed.MenuOpening is null, "New row labels and action identities invalidate the old fade");
        TestAssert.Equal("cooldown", changed.MenuOpenTransition?.Phase);
        clock.Advance(TimeSpan.FromMilliseconds(450));
        TestAssert.True(controller.Snapshot().MenuOpenTransition is null, "Structural changes do not start another synchronous wait");
        TestAssert.Equal(0, session.Executed.Count);
    }

    public static void ReopeningWhileARealQueuedSessionTaskFinishesKeepsClockQueriesSeparateFromCompletionObservation() {
        var clock = new ManualTimeProvider();
        var session = new SessionBoundary { Pending = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var controller = new BackendController(42, session, clock: clock);
        ulong sequence = 1;
        void Send(OverlayInput input) => controller.Consume(new(sequence++, 1, 42, input, true), () => true);
        Send(OverlayInput.Toggle);
        MenuOpenTestHarness.Settle(controller, clock);
        Send(OverlayInput.Select);
        controller.StartQueuedAction();
        Send(OverlayInput.Toggle);
        Send(OverlayInput.Toggle);
        int reads = session.MenuStateReads;
        session.Pending.SetResult();
        session.RejectMenuStateReads = true;

        clock.Advance(TimeSpan.FromMilliseconds(50));
        TestAssert.Equal("open:ready", controller.ReadMenuPublicationPhase());
        TestAssert.Equal(reads, session.MenuStateReads);
        TestAssert.True(controller.Busy, "Clock readiness cannot observe an independently completed game task");
        TestAssert.Equal(1, session.Executed.Count);

        session.RejectMenuStateReads = false;
        var drawn = controller.Snapshot();
        TestAssert.Equal("cooldown", drawn.MenuOpenTransition?.Phase);
        TestAssert.Equal(2UL, drawn.MenuOpening!.Revision);
        TestAssert.True(!controller.Busy, "The normal snapshot owns task completion observation");
        TestAssert.Equal(0, session.ShutdownCalls);
    }

    public static void SignedElapsedOverflowRestartsOnlyTheCurrentOpeningStageWithoutAnotherEventOrRedraw()
    {
        foreach (bool drawn in new[] { false, true })
        {
            var clock = new ManualTimeProvider();
            clock.RollBackTimestamp(TimeSpan.MaxValue);
            clock.RollBackTimestamp(TimeSpan.FromTicks(1));
            var controller = new BackendController(42, new SessionBoundary(), clock: clock);
            controller.Consume(new(1, 1, 42, OverlayInput.Toggle, true), () => true);
            if (drawn)
            {
                clock.Advance(TimeSpan.FromMilliseconds(50));
                controller.Snapshot();
            }
            ulong? epoch = controller.Snapshot().MenuRevision;
            clock.Advance(TimeSpan.FromTicks(long.MaxValue - (drawn ? TimeSpan.FromMilliseconds(50).Ticks : 0)));
            clock.Advance(TimeSpan.FromTicks(1));
            if (drawn)
                clock.Advance(TimeSpan.FromTicks(long.MaxValue - TimeSpan.FromSeconds(1).Ticks));
            var recovered = controller.Snapshot();
            TestAssert.Equal(drawn ? "cooldown" : "rebuild", recovered.MenuOpenTransition?.Phase);
            TestAssert.Equal(epoch, recovered.MenuRevision);
            TestAssert.True(recovered.MenuOpening is null, "Clock recovery cannot reapply a completed opacity event");
            clock.Advance(TimeSpan.FromMilliseconds(drawn ? 550 : 50));
            var end = controller.Snapshot();
            if (drawn)
                TestAssert.True(end.MenuOpenTransition is null, "Visible recovery remains bounded by 550 ms");
            else
                TestAssert.Equal(1UL, end.MenuOpening!.Revision);
        }
    }
}
