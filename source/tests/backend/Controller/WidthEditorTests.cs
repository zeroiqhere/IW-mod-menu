using System.Text;
using System.Text.Json;
using Apparition.Backend.Controller;
using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class WidthEditorTests {
    private static readonly OverlayInput[] EditorInputs = { OverlayInput.IncreaseWidth, OverlayInput.DecreaseWidth,
        OverlayInput.IncreaseWidthStep, OverlayInput.DecreaseWidthStep, OverlayInput.FinishWidthEditor };
    public static void ParentHelpExplainsOpeningTheEditorBeforeItsControlsBecomeActive() {
        var clock = new ManualTimeProvider();
        var controller = new BackendController(42, new SessionBoundary(), true, true, clock);
        ulong sequence = OpenCustomization(controller, clock);
        HighlightWidth(controller, ref sequence);
        // Parent controls still scroll and select until a separate editor visit begins
        void CheckParentHelp() {
            var parent = controller.Snapshot();
            TestAssert.True(parent.WidthEditor is null && parent.Notice.Contains("Select to open Width Editor", StringComparison.Ordinal),
                "Normal customization help must describe opening the editor");
            TestAssert.True(!parent.Notice.Contains("Fire increases width", StringComparison.Ordinal),
                "Inactive resize keys cannot be advertised as current parent actions");
        }
        CheckParentHelp();
        Send(controller, sequence++, OverlayInput.Select);
        TestAssert.True(controller.Snapshot().WidthEditor is not null, "Select must activate the dedicated editor");
        Send(controller, sequence++, OverlayInput.FinishWidthEditor);
        TestAssert.Equal("rebuild", controller.Snapshot().MenuUnlock);
        clock.Advance(TimeSpan.FromMilliseconds(150));
        CheckParentHelp();
        Send(controller, sequence++, OverlayInput.Select);
        Send(controller, sequence++, OverlayInput.Toggle); MenuOpenTestHarness.Settle(controller, clock);
        Send(controller, sequence++, OverlayInput.Toggle); MenuOpenTestHarness.Settle(controller, clock);
        CheckParentHelp();
    }

    public static void WidthEditorUsesAnEmptyDedicatedPanelAndCommitsOnlyLocalBoundedAppearance() {
        var (controller, session, sequence) = OpenEditor();
        var opened = controller.Snapshot();
        TestAssert.Equal("Width Editor", opened.Title);
        TestAssert.True(opened.IsOpen && !opened.IsRootMenu && opened.Rows.Count == 0, "The editor must use dedicated metadata instead of fake rows");
        TestAssert.Equal(1, opened.WidthEditor?.Step);
        TestAssert.Equal(260, opened.Appearance?.MenuWidth);
        for (int index = 0; index < 20; index++) Send(controller, sequence++, OverlayInput.IncreaseWidthStep);
        TestAssert.Equal(10, controller.Snapshot().WidthEditor?.Step);
        for (int index = 0; index < 30; index++) Send(controller, sequence++, OverlayInput.IncreaseWidth);
        TestAssert.Equal(MenuAppearance.Default with { MenuWidth = 500 }, controller.Snapshot().Appearance);
        for (int index = 0; index < 40; index++) Send(controller, sequence++, OverlayInput.DecreaseWidth);
        TestAssert.Equal(200, controller.Snapshot().Appearance?.MenuWidth);
        TestAssert.Equal(opened.NavigationRevision, controller.Snapshot().NavigationRevision);
        for (int index = 0; index < 20; index++) Send(controller, sequence++, OverlayInput.DecreaseWidthStep);
        TestAssert.Equal(1, controller.Snapshot().WidthEditor?.Step);
        controller.StartQueuedAction();
        TestAssert.Equal(0, session.Executed.Count);
        TestAssert.Equal(0, session.ShutdownCalls);
        TestAssert.True(!controller.ExitRequested, "Width adjustments cannot request game cleanup");
    }

    public static void FinishReturnsToCustomizationAndEachVisitResetsOnlyItsOffset() {
        var clock = new ManualTimeProvider();
        var (controller, _, sequence) = OpenEditor(clock: clock);
        Send(controller, sequence++, OverlayInput.IncreaseWidthStep);
        Send(controller, sequence++, OverlayInput.IncreaseWidth);
        ulong epoch = controller.Snapshot().NavigationRevision;
        Send(controller, sequence++, OverlayInput.FinishWidthEditor);
        var parent = controller.Snapshot();
        TestAssert.Equal("Menu Customization", parent.Title);
        TestAssert.Equal("Width Editor", parent.Rows[parent.SelectedIndex].Label);
        TestAssert.True(parent.IsOpen && parent.WidthEditor is null && parent.NavigationRevision > epoch, "Melee finishes locally into the parent");
        TestAssert.Equal("rebuild", parent.MenuUnlock);
        clock.Advance(TimeSpan.FromMilliseconds(150));
        Send(controller, sequence++, OverlayInput.Select);
        TestAssert.Equal(1, controller.Snapshot().WidthEditor?.Step);
        TestAssert.Equal(262, controller.Snapshot().Appearance?.MenuWidth);
        TestAssert.True(controller.Snapshot().NavigationRevision > parent.NavigationRevision, "Entry must disarm older editor contexts");
    }

    public static void UnrelatedInputsCannotNavigateOrCommitChoicesWhileTheEditorOwnsItsControls() {
        var (controller, _, sequence) = OpenEditor();
        var initial = controller.Snapshot();
        foreach (var input in new[] { OverlayInput.Up, OverlayInput.Down, OverlayInput.Select,
            OverlayInput.PreviousChoice, OverlayInput.NextChoice, OverlayInput.Back }) Send(controller, sequence++, input);
        var final = controller.Snapshot();
        TestAssert.Equal(initial.Appearance, final.Appearance);
        TestAssert.Equal(initial.WidthEditor, final.WidthEditor);
        TestAssert.Equal(initial.NavigationRevision, final.NavigationRevision);
        TestAssert.Equal("Width Editor", final.Title);
        TestAssert.True(final.IsOpen, "An unscoped Back cannot bypass Finish's epoch check");
    }

    public static void MissingBindingsDisableTheEditorWithoutInventingAttackFromDown() {
        foreach (var availability in new[] { (false, false), (true, false), (false, true) }) {
            var session = new SessionBoundary();
            var clock = new ManualTimeProvider(); var controller = new BackendController(42, session, availability.Item1, availability.Item2, clock);
            ulong sequence = OpenCustomization(controller, clock);
            var rows = controller.Snapshot().Rows;
            TestAssert.Equal("Open Controls", rows[1].Label);
            TestAssert.Equal("Width Editor", rows[2].Label);
            TestAssert.Equal("Reposition Menu", rows[3].Label);
            TestAssert.True(!rows[3].Enabled && rows[3].Value == "UNAVAILABLE", "The old fixture has no verified reposition capability");
            TestAssert.Equal("Menu Instructions", rows[4].Label);
            TestAssert.Equal("Main Design Color", rows[5].Label);
            TestAssert.Equal("Menu Preferences", rows[6].Label);
            TestAssert.True(!rows[2].Enabled, "Missing verified controls require actionable saved-binding help");
            HighlightWidth(controller, ref sequence);
            TestAssert.True(controller.Snapshot().Notice.Contains("+attack", StringComparison.Ordinal), "The disabled editor must explain exact saved controls");
            Send(controller, sequence++, OverlayInput.Select);
            foreach (var input in EditorInputs) Send(controller, sequence++, input);
            TestAssert.Equal("Menu Customization", controller.Snapshot().Title);
            TestAssert.Equal(MenuAppearance.Default, controller.Snapshot().Appearance);
            TestAssert.Equal(0, session.Executed.Count);
        }
    }

    public static void EveryEditorCommandRequiresFreshExactPidEpochAndBothFocusChecks() {
        foreach (var input in EditorInputs) {
            var (controller, _, sequence) = OpenEditor();
            var initial = controller.Snapshot();
            TestAssert.True(initial.WidthEditor is not null, "Every gate case must begin in a real editor visit");
            controller.Consume(new(sequence++, 1, 99, input, true, initial.NavigationRevision), () => true);
            controller.Consume(new(sequence++, 1, 42, input, true), () => true);
            controller.Consume(new(sequence++, 1, 42, input, true, initial.NavigationRevision - 1), () => true);
            controller.Consume(new(sequence++, 1, 42, input, false, initial.NavigationRevision), () => true);
            controller.Consume(new(sequence++, 1, 42, input, true, initial.NavigationRevision), () => false);
            int focusReads = 0;
            controller.Consume(new(sequence++, 1, 42, input, true, initial.NavigationRevision), () => ++focusReads == 1);
            ulong rejected = sequence - 1;
            Send(controller, rejected, input);
            var final = controller.Snapshot();
            TestAssert.Equal(initial.Appearance, final.Appearance);
            TestAssert.Equal(initial.WidthEditor, final.WidthEditor);
            TestAssert.Equal(initial.NavigationRevision, final.NavigationRevision);
            TestAssert.Equal(rejected, controller.LastSequence);
        }
    }

    public static void OldEditorPacketsCannotApplyAfterFinishAndReenterEvenAtTheSameWidth() {
        var clock = new ManualTimeProvider();
        var (controller, _, sequence) = OpenEditor(clock: clock);
        ulong epoch = controller.Snapshot().NavigationRevision;
        Send(controller, sequence++, OverlayInput.FinishWidthEditor);
        clock.Advance(TimeSpan.FromMilliseconds(150));
        Send(controller, sequence++, OverlayInput.Select);
        foreach (var input in EditorInputs) controller.Consume(new(sequence++, 1, 42, input, true, epoch), () => true);
        TestAssert.Equal(260, controller.Snapshot().Appearance?.MenuWidth);
        TestAssert.Equal(1, controller.Snapshot().WidthEditor?.Step);
        TestAssert.True(controller.Snapshot().IsOpen, "An old Finish cannot leave a later visit");
    }

    public static void ClosingAndShutdownLeaveTheEditorBeforeHidingWithoutResumingItsSpecialControls() {
        foreach (bool shutdown in new[] { false, true }) {
            var clock = new ManualTimeProvider(); var (controller, session, sequence) = OpenEditor(clock: clock);
            Send(controller, sequence++, OverlayInput.IncreaseWidth);
            if (shutdown) controller.RequestShutdown();
            else {
                Send(controller, sequence++, OverlayInput.Toggle);
            }
            MenuOpenTestHarness.Settle(controller, clock);
            var closed = controller.Snapshot();
            TestAssert.True(!closed.IsOpen && closed.WidthEditor is null, "Hiding must dispose the editor visit");
            TestAssert.Equal("Menu Customization", closed.Title);
            TestAssert.Equal(261, closed.Appearance?.MenuWidth);
            if (!shutdown) {
                Send(controller, sequence++, OverlayInput.Toggle); MenuOpenTestHarness.Settle(controller, clock);
                TestAssert.Equal("Menu Customization", controller.Snapshot().Title);
                TestAssert.Equal("Width Editor", controller.Snapshot().Rows[controller.Snapshot().SelectedIndex].Label);
            }
            TestAssert.Equal(0, session.Executed.Count);
            TestAssert.Equal(0, session.ShutdownCalls);
        }
    }

    public static void EditorProtocolRequiresStrictUnsignedEpochAndSerializesExactMetadataOnlyWhileOpen() {
        string[] names = { "increaseWidth", "decreaseWidth", "increaseWidthStep", "decreaseWidthStep", "finishWidthEditor" };
        for (int index = 0; index < names.Length; index++) {
            var valid = Parse(names[index], ",\"navigationRevision\":0");
            TestAssert.True(valid?.Valid == true && valid.Input == EditorInputs[index], "Exact editor command spelling must parse");
            foreach (string suffix in new[] { "", ",\"navigationRevision\":null", ",\"navigationRevision\":true", ",\"navigationRevision\":1.0" }) {
                TestAssert.True(Parse(names[index], suffix) is { Sequence: 1, Valid: false }, "Missing or malformed editor epochs must remain acknowledgeable");
            }
        }
        var (controller, _, sequence) = OpenEditor();
        using (var json = JsonDocument.Parse(controller.Snapshot().Serialize())) {
            TestAssert.Equal(260, json.RootElement.GetProperty("appearance").GetProperty("menuWidth").GetInt32());
            var metadata = json.RootElement.GetProperty("widthEditor");
            TestAssert.Equal(1, metadata.EnumerateObject().Count());
            TestAssert.Equal(1, metadata.GetProperty("step").GetInt32());
        }
        Send(controller, sequence++, OverlayInput.FinishWidthEditor);
        using var normal = JsonDocument.Parse(controller.Snapshot().Serialize());
        TestAssert.Equal(JsonValueKind.Null, normal.RootElement.GetProperty("widthEditor").ValueKind);
        TestAssert.Equal(10, (int)MenuPage.WidthEditor);
        TestAssert.Equal(8, (int)OverlayInput.IncreaseWidth);
        TestAssert.Equal(12, (int)OverlayInput.FinishWidthEditor);
    }

    public static void QuickExitCloseLeavesTheEditorBeforeHidingButMeleeFinishAlwaysReturnsOpen() {
        var (off, _, offSequence) = OpenEditor();
        Send(off, offSequence++, OverlayInput.Close);
        TestAssert.True(off.Snapshot().WidthEditor is not null, "The editor cannot override the existing Quick Exit off gate");
        foreach (bool finish in new[] { false, true }) {
            var (controller, session, sequence) = OpenEditor(quickExit: true);
            Send(controller, sequence++, OverlayInput.IncreaseWidth);
            Send(controller, sequence++, finish ? OverlayInput.FinishWidthEditor : OverlayInput.Close);
            var parent = controller.Snapshot();
            TestAssert.Equal("Menu Customization", parent.Title);
            TestAssert.True(parent.WidthEditor is null && parent.IsOpen == finish,
                "Finish returns to the parent even with Quick Exit on, while accepted Close hides it");
            TestAssert.True(parent.Appearance is { QuickExit: true, MenuWidth: 261 }, "Neither path changes committed appearance");
            TestAssert.Equal(0, session.Executed.Count);
            TestAssert.Equal(0, session.ShutdownCalls);
            TestAssert.True(!controller.ExitRequested, "Finishing and hiding cannot become session Exit");
        }
    }

    public static void LocalWidthChangesRemainIndependentOfAPendingSessionOperation() {
        var session = new SessionBoundary { Pending = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var clock = new ManualTimeProvider(); var controller = new BackendController(42, session, true, true, clock);
        ulong sequence = 1;
        Send(controller, sequence++, OverlayInput.Toggle); MenuOpenTestHarness.Settle(controller, clock);
        Send(controller, sequence++, OverlayInput.Select);
        controller.StartQueuedAction();
        for (int index = 0; index < 2; index++) Send(controller, sequence++, OverlayInput.Down);
        Send(controller, sequence++, OverlayInput.Select);
        HighlightWidth(controller, ref sequence);
        Send(controller, sequence++, OverlayInput.Select);
        TestAssert.True(controller.Busy && controller.Snapshot().WidthEditor is not null, "Local editing must coexist with an already pending operation");
        Send(controller, sequence++, OverlayInput.IncreaseWidth);
        Send(controller, sequence++, OverlayInput.FinishWidthEditor);
        TestAssert.Equal(261, controller.Snapshot().Appearance?.MenuWidth);
        TestAssert.True(controller.Busy && controller.Snapshot().WidthEditor is null, "Finish cannot complete or cancel session work");
        controller.StartQueuedAction();
        TestAssert.Equal(1, session.Executed.Count);
        TestAssert.Equal(MenuActionKind.Connect, session.Executed[0].Kind);
        TestAssert.Equal(0, session.ShutdownCalls);
        session.Pending.SetResult();
        controller.ObserveCompletion();
        TestAssert.Equal(261, controller.Snapshot().Appearance?.MenuWidth);
    }

    public static void EntryRechecksFocusAndFinalClosedStateDisposesItsEditorVisit() {
        var session = new SessionBoundary();
        var clock = new ManualTimeProvider(); var controller = new BackendController(42, session, true, true, clock);
        ulong sequence = OpenCustomization(controller, clock);
        HighlightWidth(controller, ref sequence);
        int reads = 0;
        controller.Consume(new(sequence++, 1, 42, OverlayInput.Select, true), () => ++reads == 1);
        TestAssert.Equal("Menu Customization", controller.Snapshot().Title);
        TestAssert.True(controller.Snapshot().WidthEditor is null, "A post-queue focus change cannot enter the editor");
        Send(controller, sequence++, OverlayInput.Select);
        TestAssert.True(controller.Snapshot().WidthEditor is not null, "A fresh focused selection must remain available");
        var closed = controller.ClosedState();
        TestAssert.True(!closed.IsOpen && closed.WidthEditor is null, "Final shutdown must not retain special controls");
        TestAssert.Equal("Menu Customization", closed.Title);
        TestAssert.Equal(0, session.Executed.Count);
        TestAssert.Equal(0, session.ShutdownCalls);
    }

    private static CommandEnvelope? Parse(string input, string suffix) => CommandEnvelope.Parse(Encoding.UTF8.GetBytes(
        "{\"version\":1,\"sequence\":1,\"processId\":42,\"input\":\"" + input + "\"" + suffix + "}"));
    internal static void HighlightWidth(BackendController controller, ref ulong sequence) {
        var rows = controller.Snapshot().Rows;
        int target = rows.ToList().FindIndex(row => row.Label == "Width Editor");
        TestAssert.True(target >= 0, "The source Width Editor row must remain present");
        for (int moved = 0; moved < rows.Count && controller.Snapshot().SelectedIndex != target; moved++) Send(controller, sequence++, OverlayInput.Down);
    }
    internal static (BackendController Controller, SessionBoundary Session, ulong Sequence) OpenEditor(bool quickExit = false, TimeProvider? clock = null) {
        var session = new SessionBoundary();
        session.State = session.State with { Preview = true };
        var ownedClock = clock ?? new ManualTimeProvider();
        var controller = new BackendController(42, session, true, true, ownedClock);
        ulong sequence = OpenCustomization(controller, (ManualTimeProvider)ownedClock);
        if (quickExit) {
            int preferences = controller.Snapshot().Rows.ToList().FindIndex(row => row.Label == "Menu Preferences");
            TestAssert.True(preferences >= 0, "Customization must expose the actual preference page");
            for (int index = 0; index < preferences; index++) Send(controller, sequence++, OverlayInput.Down);
            Send(controller, sequence++, OverlayInput.Select);
            int quickExitRow = controller.Snapshot().Rows.ToList().FindIndex(row => row.Label == "Quick Exit");
            TestAssert.True(quickExitRow >= 0, "The editor fixture requires the actual Quick Exit preference");
            for (int index = 0; index < quickExitRow; index++) Send(controller, sequence++, OverlayInput.Down);
            Send(controller, sequence++, OverlayInput.Select);
            TestAssert.True(ownedClock is ManualTimeProvider, "Quick Exit setup requires an owned deterministic clock");
            ((ManualTimeProvider)ownedClock).Advance(TimeSpan.FromMilliseconds(430)); controller.Snapshot();
            Send(controller, sequence++, OverlayInput.Back);
        }
        HighlightWidth(controller, ref sequence);
        Send(controller, sequence++, OverlayInput.Select);
        return (controller, session, sequence);
    }
    private static ulong OpenCustomization(BackendController controller, ManualTimeProvider clock) {
        ulong sequence = 1;
        Send(controller, sequence++, OverlayInput.Toggle); MenuOpenTestHarness.Settle(controller, clock);
        for (int index = 0; index < 2; index++) Send(controller, sequence++, OverlayInput.Down);
        Send(controller, sequence++, OverlayInput.Select);
        return sequence;
    }
    internal static void Send(BackendController controller, ulong sequence, OverlayInput input) => controller.Consume(
        new(sequence, 1, 42, input, true, input >= OverlayInput.Close ? controller.Snapshot().NavigationRevision : null), () => true);
}
