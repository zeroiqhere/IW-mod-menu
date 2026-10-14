using Apparition.Backend.Controller;
using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;
using System.Text.Json;

namespace Backend.Tests;

// Instruction settings belong to the menu and never grant a gameplay capability
internal static class MenuInstructionsTests {
    public static void InstructionDefaultsAndSerializedNamesMatchTheSourceWithoutChangingExistingEnumIds() {
        TestAssert.Equal(10, (int)MenuPage.WidthEditor);
        TestAssert.Equal(11, (int)MenuPage.Instructions);
        TestAssert.Equal(7, (int)MenuPreference.QuickExit);
        TestAssert.Equal(8, (int)MenuPreference.AlternateInstructions);
        TestAssert.Equal(9, (int)MenuPreference.InstructionsBackground);
        TestAssert.Equal(10, (int)MenuPreference.AdaptiveInstructions);
        var initial = MenuAppearance.Default;
        TestAssert.True(!initial.AlternateInstructions && initial.InstructionsBackground && !initial.AdaptiveInstructions,
            "Unsaved source defaults must keep the ordinary instruction style and background");
        using var json = JsonDocument.Parse(new BackendController(42, new SessionBoundary()).Snapshot().Serialize());
        var appearance = json.RootElement.GetProperty("appearance");
        TestAssert.True(!appearance.GetProperty("alternateInstructions").GetBoolean(), "Alternate style starts off");
        TestAssert.True(appearance.GetProperty("instructionsBackground").GetBoolean(), "Instruction background starts visible");
        TestAssert.True(!appearance.GetProperty("adaptiveInstructions").GetBoolean(), "Adaptive position starts off");
    }

    public static void InstructionsPageExposesSixSourceRowsWithBindingGatedRepositionAndLocalReset() {
        var state = new SessionBoundary().State;
        var customization = OverlayMenuBuilder.Build(MenuPage.Customization, state);
        TestAssert.True(customization.Rows.Select(row => row.Label).SequenceEqual(new[] {
            "Menu Credits", "Open Controls", "Width Editor", "Reposition Menu", "Menu Instructions", "Main Design Color", "Menu Preferences"
        }), "The new child must follow Width Editor without changing existing controls");
        var page = OverlayMenuBuilder.Build(MenuPage.Instructions, state);
        TestAssert.Equal("Menu Instructions", page.Title);
        TestAssert.True(page.Rows.Select(row => row.Label).SequenceEqual(new[] {
            "Disable", "Alternate Style", "Disable Background", "Adaptive Position", "Reposition", "Reset Position"
        }), "Rows must match the source order");
        TestAssert.True(page.Rows.Take(4).All(row => row.Enabled && row.Value == "OFF"
            && row.Action.Kind == MenuActionKind.ChangePreference), "Only implemented local toggles can be available");
        TestAssert.True(!page.Rows[4].Enabled && page.Rows[4].Value == "UNAVAILABLE"
            && page.Rows[4].Detail.Contains("+actionslot 1", StringComparison.Ordinal), "Reposition requires exact saved movement controls");
        TestAssert.True(page.Rows[5].Enabled && page.Rows[5].Value == ""
            && page.Rows[5].Action.Kind == MenuActionKind.ResetInstructionPosition, "Reset is an implemented local appearance action");
    }

    public static void InstructionToggleHelpersIsolateFieldsAndInvertDisableValues() {
        var initial = MenuAppearance.Default;
        foreach (var change in new[] {
            (MenuPreference.InstructionsVisible, initial with { InstructionsVisible = false }),
            (MenuPreference.AlternateInstructions, initial with { AlternateInstructions = true }),
            (MenuPreference.InstructionsBackground, initial with { InstructionsBackground = false }),
            (MenuPreference.AdaptiveInstructions, initial with { AdaptiveInstructions = true })
        }) {
            TestAssert.True(initial.TryChange((int)change.Item1, out var changed), "Supported local instruction toggle must apply");
            TestAssert.Equal(change.Item2, changed);
            TestAssert.True(changed.TryChange((int)change.Item1, out var restored), "A second local toggle must restore its default");
            TestAssert.Equal(initial, restored);
        }
        var disabled = OverlayMenuBuilder.Build(MenuPage.Instructions, new SessionBoundary().State,
            appearance: initial with { InstructionsVisible = false, InstructionsBackground = false });
        TestAssert.Equal("ON", disabled.Rows[0].Value);
        TestAssert.Equal("ON", disabled.Rows[2].Value);
    }

    public static void AlternateStyleClearsAdaptiveAndRefusesAdaptiveUntilAlternateIsDisabled() {
        var visit = OpenInstructions();
        visit.Select("Adaptive Position");
        TestAssert.True(visit.Controller.Snapshot().Appearance?.AdaptiveInstructions == true, "Adaptive position must apply locally");
        visit.Select("Alternate Style");
        var alternate = visit.Controller.Snapshot();
        TestAssert.True(alternate.Appearance is { AlternateInstructions: true, AdaptiveInstructions: false },
            "The source clears adaptive position when switching alternate style");
        TestAssert.True(alternate.Rows[3].Enabled && OverlayMenuBuilder.Build(MenuPage.Instructions, visit.Session.State, appearance: alternate.Appearance).Rows[3].Detail.Contains("Alternate", StringComparison.Ordinal),
            "The selectable source adaptive action must explain its rejected setting");
        visit.Select("Adaptive Position");
        TestAssert.Equal(alternate.Appearance, visit.Controller.Snapshot().Appearance);
        TestAssert.True(!alternate.Appearance!.TryChange((int)MenuPreference.AdaptiveInstructions, out var refused),
            "Direct helper calls cannot bypass the source dependency");
        TestAssert.Equal(alternate.Appearance, refused);
        visit.Select("Alternate Style"); visit.Select("Adaptive Position");
        TestAssert.True(visit.Controller.Snapshot().Appearance is { AlternateInstructions: false, AdaptiveInstructions: true },
            "Disabling alternate style restores the adaptive action");
        TestAssert.True(!(MenuAppearance.Default with { AlternateInstructions = true, AdaptiveInstructions = true }).IsSupported(),
            "An impossible combined style cannot become a supported producer snapshot");
        AssertNoGameDispatch(visit);
    }

    public static void VisibilityRemainsOnItsDedicatedPageAndPersistsAcrossPreferences() {
        var visit = OpenInstructions(); visit.Select("Disable");
        TestAssert.True(visit.Controller.Snapshot().Appearance?.InstructionsVisible == false, "Disable must hide the actual instructions");
        TestAssert.Equal("ON", visit.Controller.Snapshot().Rows[0].Value);
        visit.Send(OverlayInput.Back);
        visit.Select("Menu Preferences");
        TestAssert.True(visit.Controller.Snapshot().Rows.All(row => row.Label != "Menu Instructions"), "Preferences must not duplicate the source instruction page");
        TestAssert.True(visit.Controller.Snapshot().Appearance?.InstructionsVisible == false, "The dedicated setting remains committed across pages");
        visit.Send(OverlayInput.Back);
        visit.Select("Menu Instructions");
        visit.Select("Disable");
        TestAssert.True(visit.Controller.Snapshot().Appearance?.InstructionsVisible == true, "The dedicated toggle restores visibility");
        TestAssert.Equal("OFF", visit.Controller.Snapshot().Rows[0].Value);
        AssertNoGameDispatch(visit);
    }

    public static void StaleForeignInvalidAndFreshFocusLossCannotChangeInstructionsOrReplay() {
        var visit = OpenInstructions(); visit.Choose("Alternate Style");
        ulong rejected = visit.Sequence++;
        visit.Controller.Consume(new(rejected, 1, 42, OverlayInput.Select, true), () => false);
        visit.Controller.Consume(new(rejected, 1, 42, OverlayInput.Select, true), () => true);
        visit.Controller.Consume(new(visit.Sequence++, 1, 99, OverlayInput.Select, true), () => true);
        visit.Controller.Consume(new(visit.Sequence++, 1, 42, null, false), () => true);
        int focusReads = 0;
        visit.Controller.Consume(new(visit.Sequence++, 1, 42, OverlayInput.Select, true), () => ++focusReads == 1);
        TestAssert.Equal(MenuAppearance.Default, visit.Controller.Snapshot().Appearance);
        TestAssert.True(visit.Controller.Snapshot().Notice.Contains("focus changed", StringComparison.OrdinalIgnoreCase),
            "Fresh focus loss must explain the deliberate retry");
        visit.Send(OverlayInput.Select);
        TestAssert.True(visit.Controller.Snapshot().Appearance?.AlternateInstructions == true, "Fresh focused Select must apply exactly once");
        AssertNoGameDispatch(visit);
    }

    public static void InstructionSettingsRemainLocalInPreviewAndWhileBackendWorkIsBusy() {
        foreach (bool busy in new[] { false, true }) {
            var visit = OpenInstructions(preview: true, busy: busy);
            visit.Select("Disable Background"); visit.Select("Alternate Style"); visit.Select("Disable");
            var current = visit.Controller.Snapshot();
            TestAssert.True(current.Appearance is { InstructionsVisible: false, InstructionsBackground: false, AlternateInstructions: true },
                "Local instruction edits must remain available without gameplay capabilities");
            TestAssert.True(current.Context.StartsWith("PREVIEW", StringComparison.Ordinal), "Local changes cannot claim a connection");
            if (busy) TestAssert.True(current.Context.Contains("WORKING", StringComparison.Ordinal), "Local changes must retain backend work status");
            visit.Send(OverlayInput.Back); visit.Send(OverlayInput.Back); visit.Select("Basic Scripts");
            TestAssert.True(visit.Controller.Snapshot().Rows.All(row => !row.Enabled), "Appearance cannot enable preview game features");
            AssertNoGameDispatch(visit);
        }
    }

    public static void InstructionEditsCannotReplaceOrCompleteAPendingGameOperation() {
        var session = new SessionBoundary { Pending = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var visit = new Visit(session);
        visit.Send(OverlayInput.Toggle); MenuOpenTestHarness.Settle(visit.Controller, visit.Clock); visit.Select("Connect after spawning");
        visit.Controller.StartQueuedAction();
        TestAssert.True(visit.Controller.Busy, "The test must start a real queued controller operation");
        visit.Select("Menu Customization"); visit.Select("Menu Instructions");
        visit.Select("Adaptive Position"); visit.Select("Disable Background"); visit.Select("Alternate Style");
        var edited = visit.Controller.Snapshot();
        TestAssert.True(edited.Appearance is { AlternateInstructions: true, InstructionsBackground: false, AdaptiveInstructions: false },
            "Local edits must remain usable while the existing operation is pending");
        TestAssert.True(visit.Controller.Busy && !session.Pending.Task.IsCompleted
            && edited.Context.Contains("WORKING", StringComparison.Ordinal), "Local edits cannot complete or hide backend work");
        visit.Controller.StartQueuedAction();
        TestAssert.Equal(1, session.Executed.Count);
        TestAssert.Equal(MenuActionKind.Connect, session.Executed[0].Kind);
        TestAssert.Equal(0, session.ShutdownCalls);
        session.Pending.SetResult(); visit.Controller.ObserveCompletion();
        TestAssert.True(!visit.Controller.Busy, "Only the original task completion may release pending work");
        TestAssert.Equal(edited.Appearance, visit.Controller.Snapshot().Appearance);
    }

    public static void ClosingAndReopeningInstructionsRetainsItsCursorValuesAndParentPath() {
        var visit = OpenInstructions(); visit.Select("Disable Background");
        var before = visit.Controller.Snapshot(); visit.Send(OverlayInput.Toggle); MenuOpenTestHarness.Settle(visit.Controller, visit.Clock);
        TestAssert.True(!visit.Controller.Snapshot().IsOpen, "Toggle must hide the current instruction page");
        visit.Send(OverlayInput.Toggle); MenuOpenTestHarness.Settle(visit.Controller, visit.Clock); var reopened = visit.Controller.Snapshot();
        TestAssert.Equal(before.Appearance, reopened.Appearance);
        TestAssert.Equal(before.SelectedIndex, reopened.SelectedIndex);
        TestAssert.Equal("Menu Instructions", reopened.Title);
        visit.Send(OverlayInput.Back); var parent = visit.Controller.Snapshot();
        TestAssert.Equal("Menu Customization", parent.Title);
        TestAssert.Equal("Menu Instructions", parent.Rows[parent.SelectedIndex].Label);
        AssertNoGameDispatch(visit);
    }

    public static void MissingPositionBindingsCannotEnterAnEditorAndResetNeverDispatchesGameWork() {
        var visit = OpenInstructions();
        foreach (string label in new[] { "Reposition", "Reset Position" }) {
            visit.Select(label);
            TestAssert.Equal(MenuAppearance.Default, visit.Controller.Snapshot().Appearance);
            TestAssert.Equal("Menu Instructions", visit.Controller.Snapshot().Title);
        }
        AssertNoGameDispatch(visit);
    }

    private static void AssertNoGameDispatch(Visit visit) {
        visit.Controller.StartQueuedAction();
        TestAssert.Equal(0, visit.Session.Executed.Count);
        TestAssert.Equal(0, visit.Session.ShutdownCalls);
        TestAssert.True(!visit.Controller.ExitRequested, "Instruction settings cannot request session exit");
    }

    private static Visit OpenInstructions(bool preview = false, bool busy = false) {
        var session = new SessionBoundary(); session.State = session.State with { Preview = preview, Busy = busy };
        var visit = new Visit(session);
        visit.Send(OverlayInput.Toggle); MenuOpenTestHarness.Settle(visit.Controller, visit.Clock); visit.Select("Menu Customization"); visit.Select("Menu Instructions");
        TestAssert.Equal("Menu Instructions", visit.Controller.Snapshot().Title);
        return visit;
    }

    private sealed class Visit(SessionBoundary session) {
        public ManualTimeProvider Clock { get; } = new();
        private BackendController? controller;
        public BackendController Controller => controller ??= new BackendController(42, Session, clock: Clock);
        public SessionBoundary Session { get; } = session;
        public ulong Sequence = 1;
        public void Send(OverlayInput input) => Controller.Consume(new(Sequence++, 1, 42, input, true), () => true);
        public void Choose(string label) {
            var current = Controller.Snapshot();
            int target = current.Rows.ToList().FindIndex(row => row.Label == label);
            TestAssert.True(target >= 0, "The current actual menu must expose " + label);
            for (int move = 0; move < current.Rows.Count && Controller.Snapshot().SelectedIndex != target; move++) Send(OverlayInput.Down);
        }
        public void Select(string label) { Choose(label); Send(OverlayInput.Select); }
    }
}
