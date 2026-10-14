using System.Text.Json;
using Apparition.Backend.Controller;
using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class ScrollAnimationSelectionTests {
    public static void NumericPreferenceShowsTrueMillisecondsAndDraftChangesDoNotApplyUntilFreshUse() {
        var rig = new Rig(); rig.OpenPreferences(); rig.Highlight("Scroll Animation Time (ms)");
        var initial = rig.Controller.Snapshot();
        TestAssert.Equal("120 ms", initial.Rows[initial.SelectedIndex].Value);
        TestAssert.Equal(120, Duration(initial));
        ulong menu = Epoch(initial), navigation = initial.NavigationRevision;
        rig.Send(OverlayInput.NextChoice);
        var draft = rig.Controller.Snapshot();
        TestAssert.Equal("130 ms", draft.Rows[draft.SelectedIndex].Value);
        TestAssert.Equal(120, Duration(draft));
        TestAssert.True(draft.Notice.Contains("Candidate: 130 ms; Applied: 120 ms", StringComparison.Ordinal), "Help must distinguish the candidate from the applied duration");
        rig.Send(OverlayInput.Select);
        TestAssert.Equal(130, Duration(rig.Controller.Snapshot()));
        TestAssert.Equal(menu, Epoch(rig.Controller.Snapshot()));
        TestAssert.Equal(navigation, rig.Controller.Snapshot().NavigationRevision);
        rig.AssertNoGameWork();
    }

    public static void NumericCandidatesWrapBothSourceBoundsAndOnlyUseCommitsEachExactDuration() {
        var rig = new Rig(); rig.OpenPreferences(); rig.Highlight("Scroll Animation Time (ms)");
        rig.Send(OverlayInput.PreviousChoice); rig.Send(OverlayInput.PreviousChoice); rig.Send(OverlayInput.Select);
        TestAssert.Equal(100, Duration(rig.Controller.Snapshot()));
        rig.Send(OverlayInput.PreviousChoice);
        TestAssert.Equal("250 ms", rig.CurrentRow().Value);
        TestAssert.Equal(100, Duration(rig.Controller.Snapshot()));
        rig.Send(OverlayInput.Select); rig.Send(OverlayInput.NextChoice);
        TestAssert.Equal("100 ms", rig.CurrentRow().Value);
        for (int duration = 100; duration <= 250; duration += 10) {
            rig.Send(OverlayInput.Select);
            TestAssert.Equal(duration, Duration(rig.Controller.Snapshot()));
            rig.Send(OverlayInput.NextChoice);
        }
        TestAssert.Equal("100 ms", rig.CurrentRow().Value);
        rig.AssertNoGameWork();
    }

    public static void MissingBindingsAndStaleForeignInvalidOrEitherFocusCheckCannotStageCommitOrReplay() {
        var missing = new Rig(controls: false); missing.OpenPreferences(); missing.Highlight("Scroll Animation Time (ms)");
        TestAssert.True(!missing.CurrentRow().Enabled, "Numeric sliders require both exact saved action-slot groups");
        TestAssert.True(missing.Controller.Snapshot().Notice.Contains("+actionslot 3", StringComparison.Ordinal), "Unavailable sliders must explain the missing binding");
        missing.Send(OverlayInput.NextChoice); missing.Send(OverlayInput.Select);
        TestAssert.Equal(120, Duration(missing.Controller.Snapshot()));
        var rig = new Rig(); rig.OpenPreferences(); rig.Highlight("Scroll Animation Time (ms)");
        foreach (var input in new[] { OverlayInput.NextChoice, OverlayInput.Select }) {
            if (input == OverlayInput.Select) rig.Send(OverlayInput.NextChoice);
            string candidate = rig.CurrentRow().Value;
            rig.Send(input, focused: false);
            ulong rejected = rig.Controller.LastSequence;
            rig.Controller.Consume(new(rejected, 1, 42, input, true), () => true);
            rig.Controller.Consume(new(rig.Sequence++, 1, 99, input, true), () => true);
            rig.Controller.Consume(new(rig.Sequence++, 1, 42, input, false), () => true);
            int reads = 0;
            rig.Controller.Consume(new(rig.Sequence++, 1, 42, input, true), () => ++reads == 1);
            TestAssert.Equal(candidate, rig.CurrentRow().Value);
            TestAssert.Equal(120, Duration(rig.Controller.Snapshot()));
        }
        rig.Send(OverlayInput.Select);
        TestAssert.Equal(130, Duration(rig.Controller.Snapshot()));
        rig.AssertNoGameWork(); missing.AssertNoGameWork();
    }

    public static void NumericDraftsResumeAcrossNavigationAndHideWhileANewOwnerStartsAtTheSourceDefault() {
        var rig = new Rig(); rig.OpenPreferences(); rig.Highlight("Scroll Animation Time (ms)");
        rig.Send(OverlayInput.NextChoice); rig.Send(OverlayInput.Back); rig.Select("Menu Preferences");
        TestAssert.Equal("130 ms", rig.CurrentRow().Value);
        rig.Send(OverlayInput.Toggle); MenuOpenTestHarness.Settle(rig.Controller, rig.Clock); rig.Send(OverlayInput.Toggle); MenuOpenTestHarness.Settle(rig.Controller, rig.Clock);
        TestAssert.Equal("130 ms", rig.CurrentRow().Value);
        TestAssert.Equal(120, Duration(rig.Controller.Snapshot()));
        TestAssert.Equal(120, Duration(new BackendController(42, new SessionBoundary()).Snapshot()));
        rig.AssertNoGameWork();
    }

    public static void LocalNumericEditsPreservePreviewCapabilitiesAndAnIndependentPendingSessionOperation() {
        var rig = new Rig(preview: false);
        rig.Session.Pending = new TaskCompletionSource();
        rig.Send(OverlayInput.Toggle); MenuOpenTestHarness.Settle(rig.Controller, rig.Clock); rig.Send(OverlayInput.Select); rig.Controller.StartQueuedAction();
        rig.OpenPreferences(); rig.Highlight("Scroll Animation Time (ms)");
        rig.Send(OverlayInput.NextChoice); rig.Send(OverlayInput.Select); rig.Controller.StartQueuedAction();
        TestAssert.Equal(130, Duration(rig.Controller.Snapshot()));
        TestAssert.True(rig.Controller.Busy, "A local commit must not complete pending session work");
        TestAssert.Equal(1, rig.Session.Executed.Count);
        rig.Session.Pending.SetResult(); rig.Controller.ObserveCompletion();
        TestAssert.Equal(130, Duration(rig.Controller.Snapshot()));
        var preview = new Rig(); preview.OpenPreferences(); preview.Highlight("Scroll Animation Time (ms)");
        preview.Send(OverlayInput.NextChoice); preview.Send(OverlayInput.Select);
        preview.Send(OverlayInput.Back); preview.Send(OverlayInput.Back); preview.Select("Basic Scripts");
        TestAssert.True(preview.Controller.Snapshot().Rows.All(row => !row.Enabled), "Presentation timing cannot grant preview game capabilities");
        preview.AssertNoGameWork();
    }

    public static void RealChangedStyleCommitsInvalidatePresentationButNumericAndSameChoiceCommitsDoNot() {
        var rig = new Rig(); rig.OpenPreferences(); rig.Highlight("Bool Display");
        ulong first = Epoch(rig.Controller.Snapshot()); rig.Send(OverlayInput.Select);
        TestAssert.Equal(first, Epoch(rig.Controller.Snapshot()));
        rig.Send(OverlayInput.NextChoice); rig.Send(OverlayInput.Select);
        ulong style = Epoch(rig.Controller.Snapshot());
        TestAssert.True(style > first, "Changed source style commits explicitly rebuild presentation");
        rig.Highlight("Spotlight Cursor"); ulong beforeBoolean = Epoch(rig.Controller.Snapshot());
        rig.Send(OverlayInput.Select);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(430));
        TestAssert.True(Epoch(rig.Controller.Snapshot()) > beforeBoolean, "A supported changed boolean commit invalidates its rebuilt menu");
        rig.Highlight("Scroll Animation Time (ms)"); ulong numeric = Epoch(rig.Controller.Snapshot());
        rig.Send(OverlayInput.NextChoice); rig.Send(OverlayInput.Select);
        TestAssert.Equal(numeric, Epoch(rig.Controller.Snapshot()));
        rig.Send(OverlayInput.Back); rig.Select("Main Design Color");
        ulong colors = Epoch(rig.Controller.Snapshot()); rig.Send(OverlayInput.Select); rig.Select("Smooth Rainbow");
        TestAssert.Equal(colors, Epoch(rig.Controller.Snapshot()));
        rig.AssertNoGameWork();
    }

    internal static int Duration(StateDocument state) {
        using var json = JsonDocument.Parse(state.Serialize());
        return json.RootElement.GetProperty("appearance").GetProperty("scrollAnimationMilliseconds").GetInt32();
    }
    internal static ulong Epoch(StateDocument state) {
        using var json = JsonDocument.Parse(state.Serialize());
        return json.RootElement.GetProperty("menuRevision").GetUInt64();
    }

    // These fixtures exercise production navigation without linking a game transport
    internal sealed class Rig {
        internal SessionBoundary Session { get; } = new();
        internal ManualTimeProvider Clock { get; } = new();
        internal BackendController Controller { get; }
        internal ulong Sequence { get; set; } = 1;
        internal Rig(bool controls = true, bool preview = true) {
            Session.State = Session.State with { Preview = preview };
            Controller = new(42, Session, choiceControlsAvailable: controls, clock: Clock);
        }
        internal void Send(OverlayInput input, bool focused = true) => Controller.Consume(new(Sequence++, 1, 42, input, true), () => focused);
        internal void OpenPreferences() {
            if (!Controller.Snapshot().IsOpen) { Send(OverlayInput.Toggle); MenuOpenTestHarness.Settle(Controller, Clock); } MenuOpenTestHarness.Settle(Controller, Clock);
            Select("Menu Customization"); Select("Menu Preferences");
        }
        internal StateRow CurrentRow() { var state = Controller.Snapshot(); return state.Rows[state.SelectedIndex]; }
        internal void Highlight(string label) {
            var state = Controller.Snapshot(); int target = state.Rows.ToList().FindIndex(row => row.Label == label);
            TestAssert.True(target >= 0, "Required preference row is missing: " + label);
            for (int count = 0; count < state.Rows.Count && Controller.Snapshot().SelectedIndex != target; count++) Send(OverlayInput.Down);
            TestAssert.Equal(label, CurrentRow().Label);
        }
        internal void Select(string label) { Highlight(label); Send(OverlayInput.Select); }
        internal void AssertNoGameWork() {
            Controller.StartQueuedAction(); TestAssert.Equal(0, Session.Executed.Count); TestAssert.Equal(0, Session.ShutdownCalls);
            TestAssert.True(!Controller.Busy, "Local preferences cannot start a worker");
        }
    }
}
