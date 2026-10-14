using Apparition.Backend.Controller;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class BackendBusyProjectionTests {
    public static void SessionBusyHidesConfirmedValuesAndRejectsActionsUntilTheSessionIsReady() {
        var session = new SessionBoundary();
        session.State = session.State with { Connected = true, CanConnect = false, Busy = true,
            CanPersonalGodmode = true, PersonalGodmodeValue = "ON" };
        var clock = new ManualTimeProvider();
        var controller = new BackendController(42, session, clock: clock);
        Send(controller, 1, OverlayInput.Toggle); MenuOpenTestHarness.Settle(controller, clock);
        Send(controller, 2, OverlayInput.Down);
        Send(controller, 3, OverlayInput.Select);
        var pending = controller.Snapshot();
        // Session work can exist without a command owned by this controller
        TestAssert.True(!controller.Busy, "The fixture must isolate backend-owned work");
        TestAssert.Equal("UNKNOWN", pending.Rows[0].Value);
        TestAssert.True(!pending.Rows[0].Enabled, "Backend-owned work must disable feature selection");
        TestAssert.True(pending.Context.Contains("WORKING", StringComparison.Ordinal),
            "The context must preserve the session's busy state");
        TestAssert.True(pending.Notice.Contains("operation is in progress", StringComparison.Ordinal),
            "The menu must explain disabled controls");
        Send(controller, 4, OverlayInput.Select);
        controller.StartQueuedAction();
        TestAssert.Equal(0, session.Executed.Count);
        // A fresh ready frame restores confirmed values without replaying rejected input
        session.State = session.State with { Busy = false };
        var ready = controller.Snapshot();
        TestAssert.Equal("ON", ready.Rows[0].Value);
        TestAssert.True(ready.Rows[0].Enabled, "Readiness must restore the advertised capability");
        controller.StartQueuedAction();
        TestAssert.Equal(0, session.Executed.Count);
    }

    public static void SessionBusyStillAllowsLocalPreferencesWithoutDispatchingGameActions() {
        var session = new SessionBoundary();
        session.State = session.State with { Busy = true };
        var clock = new ManualTimeProvider();
        var controller = new BackendController(42, session, clock: clock);
        ulong sequence = 1;
        Send(controller, sequence++, OverlayInput.Toggle); MenuOpenTestHarness.Settle(controller, clock);
        for (int index = 0; index < 2; index++) Send(controller, sequence++, OverlayInput.Down);
        Send(controller, sequence++, OverlayInput.Select);
        // Local appearance changes do not require an available game connection
        int rowCount = controller.Snapshot().Rows.Count;
        for (int index = 0; index < rowCount; index++) {
            if (controller.Snapshot().Rows[controller.Snapshot().SelectedIndex].Label == "Main Design Color") break;
            Send(controller, sequence++, OverlayInput.Down);
        }
        TestAssert.Equal("Main Design Color", controller.Snapshot().Rows[controller.Snapshot().SelectedIndex].Label);
        Send(controller, sequence++, OverlayInput.Select);
        Send(controller, sequence++, OverlayInput.Select);
        TestAssert.Equal("#FF0000", controller.Snapshot().Accent);
        TestAssert.True(controller.Snapshot().Context.Contains("WORKING", StringComparison.Ordinal),
            "Local edits cannot erase the backend status");
        controller.StartQueuedAction();
        TestAssert.Equal(0, session.Executed.Count);
    }

    private static void Send(BackendController controller, ulong sequence, OverlayInput input) =>
        controller.Consume(new(sequence, 1, 42, input, true), () => true);
}
