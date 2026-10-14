using Apparition.Backend.Controller;
using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class ThemeSelectionTests {
    public static void EachReferenceColorUpdatesAccentAndExactlyOneIndicatorWithoutGameActions() {
        var (controller, session, sequence) = OpenColors(preview: false);
        TestAssert.Equal("#3998FE", controller.Snapshot().Accent);
        TestAssert.Equal("Dodger Blue", controller.Snapshot().Rows.Single(row => row.Value == "ON").Label);
        for (int index = 0; index < 36; index++) {
            Send(controller, sequence++, OverlayInput.Select);
            var snapshot = controller.Snapshot();
            TestAssert.Equal(MenuThemes.All[index].Hex, snapshot.Accent);
            TestAssert.Equal(1, snapshot.Rows.Count(row => row.Value == "ON"));
            TestAssert.Equal("ON", snapshot.Rows[index].Value);
            TestAssert.Equal(index, snapshot.SelectedIndex);
            Send(controller, sequence++, OverlayInput.Down);
        }
        controller.StartQueuedAction();
        TestAssert.Equal(0, session.Executed.Count);
        Send(controller, sequence++, OverlayInput.Back);
        TestAssert.Equal("Menu Customization", controller.Snapshot().Title);
        TestAssert.Equal("Main Design Color", controller.Snapshot().Rows[controller.Snapshot().SelectedIndex].Label);
    }

    public static void PreviewAppearanceWorksWhileGameFeaturesRemainDisabledAndUnknown() {
        var (controller, session, sequence) = OpenColors(preview: true);
        Send(controller, sequence++, OverlayInput.Select);
        TestAssert.Equal("#FF0000", controller.Snapshot().Accent);
        TestAssert.Equal("PREVIEW", controller.Snapshot().Context);
        Send(controller, sequence++, OverlayInput.Back);
        Send(controller, sequence++, OverlayInput.Back);
        Send(controller, sequence++, OverlayInput.Up);
        Send(controller, sequence++, OverlayInput.Select);
        var personal = controller.Snapshot();
        TestAssert.Equal("Basic Scripts", personal.Title);
        TestAssert.True(personal.Rows.All(row => !row.Enabled), "Theme selection cannot enable a preview game feature");
        TestAssert.Equal("UNKNOWN", personal.Rows[0].Value);
        Send(controller, sequence, OverlayInput.Select);
        controller.StartQueuedAction();
        TestAssert.Equal(0, session.Executed.Count);
    }

    public static void StaleForeignUnknownAndFocusRejectedCommandsCannotChangeAccent() {
        var (controller, session, sequence) = OpenColors(preview: false);
        Send(controller, sequence++, OverlayInput.Select);
        ulong stale = sequence - 1;
        Send(controller, sequence++, OverlayInput.Down);
        controller.Consume(new(stale, 1, 42, OverlayInput.Select, true), () => true);
        controller.Consume(new(sequence++, 1, 99, OverlayInput.Select, true), () => true);
        controller.Consume(new(sequence++, 1, 42, (OverlayInput)999, false), () => true);
        controller.Consume(new(sequence++, 1, 42, OverlayInput.Select, true), () => false);
        TestAssert.Equal("#FF0000", controller.Snapshot().Accent);
        TestAssert.Equal("ON", controller.Snapshot().Rows[0].Value);
        TestAssert.Equal("OFF", controller.Snapshot().Rows[1].Value);
        Send(controller, sequence, OverlayInput.Select);
        TestAssert.Equal("#00FF00", controller.Snapshot().Accent);
        TestAssert.Equal(0, session.Executed.Count);
    }

    private static (BackendController Controller, SessionBoundary Session, ulong Sequence) OpenColors(bool preview) {
        var session = new SessionBoundary();
        session.State = session.State with { Preview = preview };
        var clock = new ManualTimeProvider();
        var controller = new BackendController(42, session, clock: clock);
        ulong sequence = 1;
        Send(controller, sequence++, OverlayInput.Toggle); MenuOpenTestHarness.Settle(controller, clock);
        for (int index = 0; index < 2; index++) Send(controller, sequence++, OverlayInput.Down);
        Send(controller, sequence++, OverlayInput.Select);
        var customization = controller.Snapshot();
        TestAssert.True(customization.Rows.Any(row => row.Label == "Open Controls" && !row.Enabled
            && row.Value == "UNAVAILABLE"), "The controls explanation remains an informational row");
        // Find the child by its semantic label so its position remains a layout choice
        for (int index = 0; index < customization.Rows.Count; index++) {
            if (controller.Snapshot().Rows[controller.Snapshot().SelectedIndex].Label == "Main Design Color") break;
            Send(controller, sequence++, OverlayInput.Down);
        }
        TestAssert.Equal("Main Design Color", controller.Snapshot().Rows[controller.Snapshot().SelectedIndex].Label);
        Send(controller, sequence++, OverlayInput.Select);
        TestAssert.Equal("Main Design Color", controller.Snapshot().Title);
        TestAssert.Equal(37, controller.Snapshot().Rows.Count);
        return (controller, session, sequence);
    }

    private static void Send(BackendController controller, ulong sequence, OverlayInput input) =>
        controller.Consume(new(sequence, 1, 42, input, true), () => true);
}
