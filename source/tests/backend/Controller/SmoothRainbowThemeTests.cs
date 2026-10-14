using System.Text.Json;
using Apparition.Backend.Controller;
using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class SmoothRainbowThemeTests {
    public static void RainbowSelectionChecksOnlyTheSourceModeAndRepeatedUseDoesNotResetItsState() {
        var rig = new Rig();
        rig.OpenColors();
        var initial = rig.Controller.Snapshot();
        TestAssert.Equal(37, initial.Rows.Count);
        TestAssert.Equal("OFF", initial.Rows[36].Value);
        rig.Select("Smooth Rainbow");
        var enabled = rig.Controller.Snapshot();
        TestAssert.True(Mode(enabled), "Selecting Smooth Rainbow must enable its presentation mode");
        TestAssert.Equal(36, enabled.SelectedIndex);
        TestAssert.True(enabled.Rows.Take(36).All(row => row.Value == "OFF"), "Rainbow cannot borrow a fixed color indicator");
        TestAssert.Equal("ON", enabled.Rows[36].Value);
        rig.Send(OverlayInput.Select);
        var repeated = rig.Controller.Snapshot();
        TestAssert.True(Mode(repeated), "The source handler returns when already enabled");
        TestAssert.Equal(enabled.Accent, repeated.Accent);
        TestAssert.Equal(enabled.NavigationRevision, repeated.NavigationRevision);
        TestAssert.Equal(enabled.Appearance, repeated.Appearance);
        TestAssert.True(repeated.LastSequence > enabled.LastSequence, "A no-op must still acknowledge fresh input");
        rig.AssertNoGameWork();
    }

    public static void EachFixedPaletteSelectionStopsRainbowAndChecksOnlyItsExactSourceColor() {
        var rig = new Rig();
        rig.OpenColors();
        foreach (var theme in MenuThemes.All) {
            rig.Select("Smooth Rainbow");
            rig.Select(theme.Name);
            var state = rig.Controller.Snapshot();
            TestAssert.True(!Mode(state), "Every valid fixed color must stop rainbow mode");
            TestAssert.Equal(theme.Hex, state.Accent);
            TestAssert.Equal(theme.Name, state.Rows.Single(row => row.Value == "ON").Label);
            TestAssert.Equal("OFF", state.Rows[36].Value);
        }
        rig.AssertNoGameWork();
    }

    public static void StaleForeignInvalidAndEitherFocusCheckCannotEnableOrStopRainbowOrReplay() {
        var rig = new Rig();
        rig.OpenColors();
        rig.Highlight("Smooth Rainbow");
        rig.Send(OverlayInput.Select, focused: false);
        RejectSelections(rig);
        TestAssert.True(!Mode(rig.Controller.Snapshot()), "Rejected inputs cannot enable rainbow");
        rig.Send(OverlayInput.Select);
        rig.Highlight("Mint");
        RejectSelections(rig);
        TestAssert.True(Mode(rig.Controller.Snapshot()), "Rejected fixed-color input cannot stop rainbow");
        TestAssert.Equal(MenuThemes.DefaultAccent, rig.Controller.Snapshot().Accent);
        rig.Send(OverlayInput.Select);
        TestAssert.True(!Mode(rig.Controller.Snapshot()), "A new focused Use may commit the fixed color");
        TestAssert.Equal("#98FF98", rig.Controller.Snapshot().Accent);
        rig.AssertNoGameWork();
    }

    public static void RainbowRetainsItsOwnerModeAcrossBackHideFocusAndFinalClosedPresentation() {
        var rig = new Rig();
        rig.OpenColors(); rig.Select("Smooth Rainbow");
        rig.Send(OverlayInput.Back);
        rig.Send(OverlayInput.Toggle); MenuOpenTestHarness.Settle(rig.Controller, rig.Clock);
        var hidden = rig.Controller.Snapshot();
        TestAssert.True(!hidden.IsOpen && Mode(hidden), "Source menu close does not terminate the theme mode");
        rig.Send(OverlayInput.Toggle); MenuOpenTestHarness.Settle(rig.Controller, rig.Clock);
        rig.Select("Main Design Color");
        var visible = rig.Controller.Snapshot();
        TestAssert.True(Mode(visible), "Returning to colors must retain the same mode");
        TestAssert.Equal(36, visible.SelectedIndex);
        var focusHidden = StateFocusProjection.Apply(visible, false);
        TestAssert.True(!focusHidden.IsOpen && Mode(focusHidden), "Focus projection cannot alter owned theme state");
        rig.Controller.RequestShutdown();
        var closed = rig.Controller.ClosedState();
        TestAssert.True(!closed.IsOpen && Mode(closed), "Final closed state may retain a coherent mode but must hide drawing");
        rig.AssertNoGameWork();
        TestAssert.True(!Mode(new BackendController(42, new SessionBoundary()).Snapshot()), "A new owner starts with the fixed default");
    }

    public static void RainbowRemainsLocalInPreviewAndWhileARealControllerOperationIsPending() {
        var rig = new Rig();
        rig.Session.Pending = new TaskCompletionSource();
        rig.Send(OverlayInput.Toggle); MenuOpenTestHarness.Settle(rig.Controller, rig.Clock); rig.Send(OverlayInput.Select); rig.Controller.StartQueuedAction();
        TestAssert.Equal(1, rig.Session.Executed.Count);
        TestAssert.True(rig.Controller.Busy, "The fixture must retain an actual queued backend operation");
        rig.OpenColors(); rig.Select("Smooth Rainbow");
        TestAssert.True(Mode(rig.Controller.Snapshot()) && rig.Controller.Busy, "A local theme cannot complete or replace pending work");
        rig.Controller.StartQueuedAction();
        TestAssert.Equal(1, rig.Session.Executed.Count);
        // Completing the original task must not consume or replace a local theme action
        rig.Session.Pending.SetResult(); rig.Controller.ObserveCompletion();
        TestAssert.True(Mode(rig.Controller.Snapshot()), "Operation completion cannot clear local theme state");
        var preview = new Rig(preview: true);
        preview.OpenColors(); preview.Select("Smooth Rainbow");
        preview.Send(OverlayInput.Back); preview.Send(OverlayInput.Back); preview.Select("Basic Scripts");
        TestAssert.True(Mode(preview.Controller.Snapshot()), "Preview pages share the same local mode");
        TestAssert.True(preview.Controller.Snapshot().Rows.All(row => !row.Enabled), "Rainbow cannot grant game capabilities");
        preview.Send(OverlayInput.Select); preview.AssertNoGameWork();
    }

    public static void RainbowSurvivesLocalEditorsWithoutCreatingItsOwnPublicationPhase() {
        var rig = new Rig();
        rig.OpenColors(); rig.Select("Smooth Rainbow"); rig.Send(OverlayInput.Back);
        rig.Select("Width Editor");
        TestAssert.True(Mode(rig.Controller.Snapshot()), "Width editor must preserve rainbow mode");
        rig.Send(OverlayInput.IncreaseWidth);
        rig.Send(OverlayInput.Toggle); MenuOpenTestHarness.Settle(rig.Controller, rig.Clock); rig.Send(OverlayInput.Toggle); MenuOpenTestHarness.Settle(rig.Controller, rig.Clock);
        rig.Select("Menu Instructions"); rig.Select("Reposition");
        TestAssert.True(Mode(rig.Controller.Snapshot()), "Position editor must preserve rainbow mode");
        rig.Clock.Advance(TimeSpan.FromMilliseconds(100));
        rig.Send(OverlayInput.MoveInstructionsRight); rig.Send(OverlayInput.Toggle); MenuOpenTestHarness.Settle(rig.Controller, rig.Clock); rig.Send(OverlayInput.Toggle); MenuOpenTestHarness.Settle(rig.Controller, rig.Clock);
        TestAssert.True(Mode(rig.Controller.Snapshot()), "Aborting local editors must not cancel the theme");
        TestAssert.Equal(261, rig.Controller.Snapshot().Appearance?.MenuWidth);
        TestAssert.Equal(new InstructionPosition(263, 472), rig.Controller.Snapshot().Appearance?.InstructionPosition);
        TestAssert.Equal<string?>(null, rig.Controller.ReadMenuPublicationPhase());
        rig.AssertNoGameWork();
    }

    public static void ModeSerializesAsAnAppendedTopLevelBooleanWithoutChangingAppearanceOrOldActionIds() {
        var rig = new Rig();
        var legacy = StateDocument.Create(42, 0, false, new("Apparition", "PREVIEW", Array.Empty<MenuRow>(), ""), 0);
        TestAssert.True(!Mode(legacy), "Older callers must publish the fixed default");
        rig.OpenColors(); var appearance = rig.Controller.Snapshot().Appearance; rig.Select("Smooth Rainbow");
        using var json = JsonDocument.Parse(rig.Controller.Snapshot().Serialize());
        TestAssert.Equal(JsonValueKind.True, json.RootElement.GetProperty("smoothRainbowTheme").ValueKind);
        TestAssert.True(!json.RootElement.GetProperty("appearance").TryGetProperty("smoothRainbowTheme", out _), "Theme mode is separate from menu appearance");
        TestAssert.Equal(appearance, rig.Controller.Snapshot().Appearance);
        TestAssert.Equal(12, (int)MenuActionKind.ResetInstructionPosition);
        TestAssert.True(Enum.TryParse<MenuActionKind>("EnableSmoothRainbowTheme", out var action) && (int)action == 13,
            "The new action must append without changing existing IDs");
    }

    private static void RejectSelections(Rig rig) {
        ulong stale = rig.Controller.LastSequence;
        rig.Send(OverlayInput.Select, focused: false);
        rig.Controller.Consume(new(stale, 1, 42, OverlayInput.Select, true), () => true);
        rig.Controller.Consume(new(rig.Sequence++, 1, 99, OverlayInput.Select, true), () => true);
        rig.Controller.Consume(new(rig.Sequence++, 1, 42, (OverlayInput)999, false), () => true);
        // Focus can change after admission but before the local row is committed
        int reads = 0;
        rig.Controller.Consume(new(rig.Sequence++, 1, 42, OverlayInput.Select, true), () => ++reads == 1);
        // An acknowledged rejection cannot replay once focus becomes available
        ulong rejected = rig.Controller.LastSequence;
        rig.Controller.Consume(new(rejected, 1, 42, OverlayInput.Select, true), () => true);
    }

    internal static bool Mode(StateDocument state) {
        using var json = JsonDocument.Parse(state.Serialize());
        return json.RootElement.GetProperty("smoothRainbowTheme").GetBoolean();
    }

    // Every selection uses the same fresh row path as the production controller
    internal sealed class Rig {
        internal SessionBoundary Session { get; } = new();
        internal ManualTimeProvider Clock { get; } = new();
        internal BackendController Controller { get; }
        internal ulong Sequence { get; set; } = 1;
        internal Rig(bool preview = false) {
            Session.State = Session.State with { Preview = preview };
            Controller = new(42, Session, true, true, Clock, true);
        }
        internal void Send(OverlayInput input, bool focused = true) => Controller.Consume(new(Sequence++, 1, 42, input, true,
            input >= OverlayInput.Close ? Controller.Snapshot().NavigationRevision : null), () => focused);
        internal void OpenColors() {
            if (!Controller.Snapshot().IsOpen) { Send(OverlayInput.Toggle); MenuOpenTestHarness.Settle(Controller, Clock); } MenuOpenTestHarness.Settle(Controller, Clock);
            Select("Menu Customization"); Select("Main Design Color");
        }
        internal void Highlight(string label) {
            var state = Controller.Snapshot();
            int target = state.Rows.ToList().FindIndex(row => row.Label == label);
            TestAssert.True(target >= 0, "Required source row is missing: " + label);
            for (int count = 0; count < state.Rows.Count && Controller.Snapshot().SelectedIndex != target; count++) Send(OverlayInput.Down);
            TestAssert.Equal(label, Controller.Snapshot().Rows[Controller.Snapshot().SelectedIndex].Label);
        }
        internal void Select(string label) { Highlight(label); Send(OverlayInput.Select); }
        internal void AssertNoGameWork() {
            Controller.StartQueuedAction();
            TestAssert.Equal(0, Session.Executed.Count); TestAssert.Equal(0, Session.ShutdownCalls);
            TestAssert.True(!Controller.Busy, "Local presentation cannot create pending game work");
        }
    }
}
