using Apparition.Backend.Controller;
using System.Text.Json;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class PreferenceSelectionTests {
    public static void ControllerAlwaysSerializesExactCamelCaseAppearanceFields() {
        var controller = new BackendController(42, new SessionBoundary());
        var document = controller.Snapshot();
        TestAssert.Equal(MenuAppearance.Default, document.Appearance);
        using var json = JsonDocument.Parse(document.Serialize());
        var appearance = json.RootElement.GetProperty("appearance");
        TestAssert.Equal(17, appearance.EnumerateObject().Count());
        TestAssert.Equal("Apparition", appearance.GetProperty("design").GetString());
        TestAssert.Equal(JsonValueKind.Null, appearance.GetProperty("menuPosition").ValueKind);
        TestAssert.Equal(120, appearance.GetProperty("scrollAnimationMilliseconds").GetInt32());
        TestAssert.Equal(260, appearance.GetProperty("menuWidth").GetInt32());
        TestAssert.Equal("Boxes", appearance.GetProperty("booleanDisplay").GetString());
        TestAssert.Equal("Right", appearance.GetProperty("boolBoxLocation").GetString());
        TestAssert.True(appearance.GetProperty("instructionsVisible").GetBoolean(), "Instructions start visible");
        TestAssert.True(!appearance.GetProperty("optionCounter").GetBoolean(), "Counter starts disabled");
        TestAssert.True(appearance.GetProperty("coloredCursor").GetBoolean(), "Colored cursor starts enabled");
        TestAssert.True(appearance.GetProperty("spotlightCursor").GetBoolean(), "Spotlight starts enabled");
        TestAssert.True(!appearance.GetProperty("largeCursor").GetBoolean(), "Large cursor starts disabled");
        TestAssert.True(!appearance.GetProperty("quickExit").GetBoolean(), "Quick Exit starts disabled");
    }

    public static void ExistingReversibleChoicesAndTogglesReturnWithoutAnyGameDispatch() {
        var (controller, session, sequence, clock) = OpenPreferences();
        foreach (int index in new[] { 1, 2, 3, 4, 6, 7, 8, 9 }) {
            for (int moved = 0; moved < 12 && controller.Snapshot().SelectedIndex != index; moved++) Send(controller, sequence++, OverlayInput.Down);
            string original = controller.Snapshot().Rows[index].Value;
            int cycle = index == 1 ? 3 : index == 3 ? 16 : 2;
            for (int step = 1; step <= cycle; step++) {
                if (index <= 3) Send(controller, sequence++, OverlayInput.NextChoice);
                Send(controller, sequence++, OverlayInput.Select);
                if (index is 4 or 6 or 7 or 8) clock.Advance(TimeSpan.FromMilliseconds(430));
                if (index == 9) clock.Advance(TimeSpan.FromMilliseconds(250));
                if (step < cycle) TestAssert.True(controller.Snapshot().Rows[index].Value != original,
                    "A completed local choice must change its displayed state");
            }
            TestAssert.Equal(original, controller.Snapshot().Rows[index].Value);
        }
        TestAssert.Equal(MenuAppearance.Default, controller.Snapshot().Appearance);
        controller.StartQueuedAction();
        TestAssert.Equal(0, session.Executed.Count);
    }

    public static void StaleForeignUnknownAndImmediateFocusLossCannotMutatePreferences() {
        var (controller, session, sequence, clock) = OpenPreferences();
        controller.Consume(new(sequence++, 1, 42, OverlayInput.Select, true), () => false);
        ulong stale = sequence - 1;
        controller.Consume(new(stale, 1, 42, OverlayInput.Select, true), () => true);
        controller.Consume(new(sequence++, 1, 99, OverlayInput.Select, true), () => true);
        controller.Consume(new(sequence++, 1, 42, null, false), () => true);
        int focusReads = 0;
        Send(controller, sequence++, OverlayInput.NextChoice);
        controller.Consume(new(sequence++, 1, 42, OverlayInput.Select, true), () => ++focusReads == 1);
        TestAssert.Equal(MenuAppearance.Default, controller.Snapshot().Appearance);
        Send(controller, sequence++, OverlayInput.Select);
        TestAssert.Equal("Text", controller.Snapshot().Appearance?.BooleanDisplay);
        TestAssert.Equal(0, session.Executed.Count);
    }

    public static void PreviewPreferencesAndThemeRemainLocalAndPreserveDisabledGameFeatures() {
        var (controller, session, sequence, clock) = OpenPreferences();
        Send(controller, sequence++, OverlayInput.Down);
        Send(controller, sequence++, OverlayInput.Down);
        Send(controller, sequence++, OverlayInput.Down);
        Send(controller, sequence++, OverlayInput.Select);
        clock.Advance(TimeSpan.FromMilliseconds(430));
        TestAssert.True(controller.Snapshot().Appearance?.QuickExit == true, "Quick Exit toggle must update its appearance field");
        TestAssert.Equal("PREVIEW", controller.Snapshot().Context);
        TestAssert.Equal("#3998FE", controller.Snapshot().Accent);
        Send(controller, sequence++, OverlayInput.Back);
        Send(controller, sequence++, OverlayInput.Up);
        Send(controller, sequence++, OverlayInput.Select);
        Send(controller, sequence++, OverlayInput.Select);
        TestAssert.Equal("#FF0000", controller.Snapshot().Accent);
        TestAssert.True(controller.Snapshot().Appearance?.QuickExit == true, "Theme changes cannot reset preferences");
        Send(controller, sequence++, OverlayInput.Back);
        Send(controller, sequence++, OverlayInput.Back);
        Send(controller, sequence++, OverlayInput.Up);
        Send(controller, sequence++, OverlayInput.Select);
        TestAssert.True(controller.Snapshot().Rows.All(row => !row.Enabled), "Preferences cannot enable preview features");
        TestAssert.Equal("UNKNOWN", controller.Snapshot().Rows[0].Value);
        Send(controller, sequence++, OverlayInput.Select);
        controller.StartQueuedAction();
        TestAssert.Equal(0, session.Executed.Count);
    }

    private static (BackendController Controller, SessionBoundary Session, ulong Sequence, ManualTimeProvider Clock) OpenPreferences() {
        var session = new SessionBoundary();
        session.State = session.State with { Preview = true };
        var clock = new ManualTimeProvider();
        var controller = new BackendController(42, session, choiceControlsAvailable: true, clock: clock);
        ulong sequence = 1;
        Send(controller, sequence++, OverlayInput.Toggle); MenuOpenTestHarness.Settle(controller, clock);
        for (int index = 0; index < 2; index++) Send(controller, sequence++, OverlayInput.Down);
        Send(controller, sequence++, OverlayInput.Select);
        var rows = controller.Snapshot().Rows;
        int preferences = rows.ToList().FindIndex(row => row.Label == "Menu Preferences");
        TestAssert.True(preferences >= 0, "Customization must link to implemented preferences");
        for (int index = 0; index < preferences; index++) Send(controller, sequence++, OverlayInput.Down);
        Send(controller, sequence++, OverlayInput.Select);
        TestAssert.Equal("Menu Preferences", controller.Snapshot().Title);
        Send(controller, sequence++, OverlayInput.Down);
        return (controller, session, sequence, clock);
    }

    private static void Send(BackendController controller, ulong sequence, OverlayInput input) =>
        controller.Consume(new(sequence, 1, 42, input, true), () => true);
}
