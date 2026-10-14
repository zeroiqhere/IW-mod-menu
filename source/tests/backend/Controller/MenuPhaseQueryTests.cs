using Apparition.Backend.Controller;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class MenuPhaseQueryTests {
    public static void PhaseQueriesReadOnlyClockStateAndNeverObserveCompletedSessionWork() {
        var clock = new ManualTimeProvider();
        var session = new SessionBoundary { Pending = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var controller = new BackendController(42, session, true, true, clock);
        ulong sequence = 1;
        WidthEditorTests.Send(controller, sequence++, OverlayInput.Toggle); MenuOpenTestHarness.Settle(controller, clock);
        WidthEditorTests.Send(controller, sequence++, OverlayInput.Select);
        controller.StartQueuedAction();
        for (int index = 0; index < 2; index++) WidthEditorTests.Send(controller, sequence++, OverlayInput.Down);
        WidthEditorTests.Send(controller, sequence++, OverlayInput.Select);
        WidthEditorTests.HighlightWidth(controller, ref sequence);
        WidthEditorTests.Send(controller, sequence++, OverlayInput.Select);
        WidthEditorTests.Send(controller, sequence++, OverlayInput.FinishWidthEditor);
        int reads = session.MenuStateReads;
        session.RejectMenuStateReads = true;
        session.Pending.SetResult();
        TestAssert.Equal("rebuild", controller.ReadMenuUnlockPhase());
        clock.Advance(TimeSpan.FromMilliseconds(50));
        TestAssert.Equal("pause", controller.ReadMenuUnlockPhase());
        clock.Advance(TimeSpan.FromMilliseconds(100));
        TestAssert.True(controller.ReadMenuUnlockPhase() is null, "The monotonic phase query must end normally without a game read");
        TestAssert.True(controller.Busy, "A clock-only query cannot observe or complete a pending session task");
        TestAssert.Equal(reads, session.MenuStateReads);
        session.RejectMenuStateReads = false;
        controller.ObserveCompletion();
        TestAssert.True(!controller.Busy, "Normal completion observation remains independent");
    }

}
