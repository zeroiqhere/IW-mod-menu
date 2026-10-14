using System.Text.Json;
using Apparition.Backend.Controller;
using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class MenuDesignTests
{
    internal static readonly string[] Names = { "Apparition", "Classic", "Native", "AIO", "Basic" };

    public static void DesignIsTheFirstStagedChoiceAndWrapsFiveExactNamesWithoutChangingAppliedPresentation()
    {
        var rig = new Rig(); rig.Open(); TestAssert.Equal(12, rig.Current().Rows.Count); TestAssert.Equal("Design", rig.Current().Rows[0].Label);
        foreach (string name in Names.Skip(1).Append(Names[0]))
        {
            rig.Send(OverlayInput.NextChoice); TestAssert.Equal(name, rig.Current().Rows[0].Value);
            TestAssert.Equal("Apparition", Design(rig.Current())); TestAssert.True(Phase(rig.Current()) is null, "Draft motion cannot start a rebuild");
        }
        rig.Send(OverlayInput.PreviousChoice); TestAssert.Equal("Basic", rig.Current().Rows[0].Value);
        rig.Send(OverlayInput.Back); rig.Select("Menu Preferences"); TestAssert.Equal("Basic", rig.Current().Rows[0].Value);
        rig.Send(OverlayInput.Toggle); MenuOpenTestHarness.Settle(rig.Controller, rig.Clock); rig.Send(OverlayInput.Toggle); MenuOpenTestHarness.Settle(rig.Controller, rig.Clock); TestAssert.Equal("Basic", rig.Current().Rows[0].Value); rig.NoGameWork();
    }

    public static void ChangedDesignUsesFiftyMillisecondRebuildAnd250MillisecondCooldownWithOneInvalidation()
    {
        foreach (string name in Names.Skip(1))
        {
            var rig = new Rig(); rig.Open(); rig.Stage(name); var old = rig.Current(); rig.Send(OverlayInput.Select);
            TestAssert.Equal("rebuild", Phase(rig.Current())); TestAssert.Equal("Apparition", Design(rig.Current()));
            TestAssert.True(rig.Current().IsOpen && rig.Current().Rows.Count == 12, "The owner retains its logical menu during HUD replacement");
            TestAssert.Equal(old.MenuRevision, rig.Current().MenuRevision); TestAssert.Equal(old.NavigationRevision, rig.Current().NavigationRevision);
            rig.Clock.Advance(TimeSpan.FromMilliseconds(49)); TestAssert.Equal("rebuild", Phase(rig.Current())); TestAssert.Equal("Apparition", Design(rig.Current()));
            rig.Clock.Advance(TimeSpan.FromMilliseconds(1)); var applied = rig.Current(); TestAssert.Equal("cooldown", Phase(applied)); TestAssert.Equal(name, Design(applied));
            TestAssert.Equal(old.MenuRevision + 1, applied.MenuRevision); TestAssert.Equal(old.NavigationRevision, applied.NavigationRevision);
            rig.Clock.Advance(TimeSpan.FromMilliseconds(199)); TestAssert.Equal("cooldown", Phase(rig.Current()));
            rig.Clock.Advance(TimeSpan.FromMilliseconds(1)); TestAssert.True(Phase(rig.Current()) is null, "Both independent waits finish at250ms, not300ms or150ms");
            TestAssert.Equal(applied.MenuRevision, rig.Current().MenuRevision); TestAssert.Equal(0, rig.Current().SelectedIndex);
            rig.Send(OverlayInput.Back); TestAssert.Equal("Menu Preferences", rig.Current().Rows[rig.Current().SelectedIndex].Label); rig.NoGameWork();
        }
    }

    public static void SameDesignStillPausesInputWithoutRebuildInvalidationOrForcedChanges()
    {
        var rig = new Rig(); rig.Open(); var before = rig.Current(); rig.Send(OverlayInput.Select);
        TestAssert.Equal("cooldown", Phase(rig.Current())); TestAssert.Equal(before.Appearance, rig.Current().Appearance);
        foreach (var input in Enum.GetValues<OverlayInput>()) rig.Send(input);
        TestAssert.Equal(before.NavigationRevision, rig.Current().NavigationRevision); TestAssert.Equal(before.MenuRevision, rig.Current().MenuRevision);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(249)); TestAssert.Equal("cooldown", Phase(rig.Current()));
        rig.Clock.Advance(TimeSpan.FromMilliseconds(1)); TestAssert.True(Phase(rig.Current()) is null, "A no-op handler still consumes the monitor cooldown");
        TestAssert.Equal(before.MenuRevision, rig.Current().MenuRevision); rig.Send(OverlayInput.Down); TestAssert.Equal(1, rig.Current().SelectedIndex); rig.NoGameWork();
    }

    public static void DesignForcesOnlyItsIncompatibleFlagsOffAndReturningDoesNotRestoreThem()
    {
        foreach (string name in Names.Skip(1))
        {
            var rig = new Rig(); rig.Open();
            rig.Select("Option Counter");
            rig.Clock.Advance(TimeSpan.FromMilliseconds(250));
            rig.Current();
            rig.Highlight("Design"); rig.Stage(name); rig.Send(OverlayInput.Select);
            var actual = rig.Current(); bool cursor = name is not "Classic" and not "Native"; bool counter = name is not "AIO" and not "Basic";
            TestAssert.Equal(cursor, actual.Appearance!.ColoredCursor); TestAssert.Equal(counter, actual.Appearance.OptionCounter);
            TestAssert.True(actual.Rows.First(row => row.Label == "Colored Cursor").Enabled,
                "The source Colored Cursor BOOL remains selectable under every design");
            TestAssert.True(actual.Rows.First(row => row.Label == "Option Counter").Enabled,
                "The source Option Counter BOOL remains selectable under every design");
            rig.Clock.Advance(TimeSpan.FromMilliseconds(250)); rig.Current(); rig.Stage("Apparition"); rig.Send(OverlayInput.Select); rig.Clock.Advance(TimeSpan.FromMilliseconds(250));
            TestAssert.Equal(cursor, rig.Current().Appearance!.ColoredCursor); TestAssert.Equal(counter, rig.Current().Appearance!.OptionCounter);
            TestAssert.True(rig.Current().Rows.First(row => row.Label == "Colored Cursor").Enabled && rig.Current().Rows.First(row => row.Label == "Option Counter").Enabled,
                "Returning enables compatible actions without restoring their previous values"); rig.NoGameWork();
        }
    }

    public static void MissingBindingsStaleForeignAndBothFocusChecksCannotStageCommitOrReplayDesign()
    {
        var missing = new Rig(false); missing.Open(); TestAssert.True(!missing.Current().Rows[0].Enabled, "Design requires exact saved choice controls");
        missing.Send(OverlayInput.NextChoice); missing.Send(OverlayInput.Select); TestAssert.Equal("Apparition", missing.Current().Rows[0].Value); TestAssert.True(Phase(missing.Current()) is null, "Unavailable design cannot start a cooldown");
        var rig = new Rig(); rig.Open(); rig.Stage("Classic"); var before = rig.Current();
        foreach (var input in new[] { OverlayInput.NextChoice, OverlayInput.Select })
        {
            rig.Send(input, false); ulong stale = rig.Controller.LastSequence;
            rig.Controller.Consume(new(stale, 1, 42, input, true), () => true);
            rig.Controller.Consume(new(rig.Sequence++, 1, 99, input, true), () => true);
            rig.Controller.Consume(new(rig.Sequence++, 1, 42, input, false), () => true);
            int focus = 0; rig.Controller.Consume(new(rig.Sequence++, 1, 42, input, true), () => ++focus == 1);
            TestAssert.Equal(before.Rows[0].Value, rig.Current().Rows[0].Value); TestAssert.True(Phase(rig.Current()) is null, "Rejected input cannot create a transition");
        }
        rig.NoGameWork(); missing.NoGameWork();
    }

    public static void HiddenOwnerClockAndPendingSessionCompletionRemainIndependentOfTheDesignRebuild()
    {
        var rig = new Rig(preview: false); rig.Session.Pending = new TaskCompletionSource();
        rig.Send(OverlayInput.Toggle); MenuOpenTestHarness.Settle(rig.Controller, rig.Clock); rig.Send(OverlayInput.Select); rig.Controller.StartQueuedAction(); rig.Select("Menu Customization"); rig.Select("Menu Preferences");
        rig.Stage("Native"); rig.Send(OverlayInput.Select); var hidden = StateFocusProjection.Apply(rig.Current(), false);
        TestAssert.True(!hidden.IsOpen && Phase(hidden) is null, "Focus hides transition metadata without ending it");
        int reads = rig.Session.MenuStateReads; rig.Session.RejectMenuStateReads = true; rig.Session.Pending.SetResult();
        rig.Clock.Advance(TimeSpan.FromMilliseconds(50)); TestAssert.Equal("design:cooldown+opening:2", rig.Controller.ReadMenuPublicationPhase());
        rig.Clock.Advance(TimeSpan.FromMilliseconds(200)); rig.Controller.ReadMenuPublicationPhase(); TestAssert.True(rig.Controller.Busy, "The hidden owner advances opening without completing pending session work");
        TestAssert.Equal(reads, rig.Session.MenuStateReads); TestAssert.True(rig.Controller.Busy, "A phase query cannot observe a task completion");
        rig.Session.RejectMenuStateReads = false; rig.Controller.ObserveCompletion(); TestAssert.Equal("Native", Design(rig.Current()));
        TestAssert.Equal(1, rig.Session.Executed.Count); TestAssert.Equal(0, rig.Session.ShutdownCalls);
    }

    public static void ShutdownKeepsCommittedDesignAndCancelsAnyLaterRebuildOrResurrection()
    {
        var rig = new Rig(); rig.Open(); rig.Stage("Classic"); rig.Send(OverlayInput.Select); rig.Controller.RequestShutdown();
        var closed = rig.Controller.ClosedState(); TestAssert.True(!closed.IsOpen && Phase(closed) is null, "Owner cleanup cancels the local transition");
        TestAssert.Equal("Classic", Design(closed)); TestAssert.True(!closed.Appearance!.ColoredCursor, "Committed normalization survives cleanup");
        rig.Clock.Advance(TimeSpan.FromSeconds(1)); rig.Controller.ReadMenuPublicationPhase(); TestAssert.Equal(closed.MenuRevision, rig.Current().MenuRevision); rig.NoGameWork();
    }

    public static void RebuildClockFaultsRestartOnlyTheCurrentPhaseWithoutRevertingAppliedGeometry()
    {
        var rig = new Rig(); rig.Open(); rig.Stage("Classic"); rig.Send(OverlayInput.Select); var before = rig.Current();
        rig.Clock.Advance(TimeSpan.FromMilliseconds(25)); rig.Current(); rig.Clock.RollBackTimestamp(TimeSpan.FromMilliseconds(10));
        TestAssert.Equal("rebuild", Phase(rig.Current())); rig.Clock.Advance(TimeSpan.FromMilliseconds(50)); var applied = rig.Current();
        TestAssert.Equal("Classic", Design(applied)); TestAssert.Equal(before.MenuRevision + 1, applied.MenuRevision);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(100)); rig.Current(); rig.Clock.RollBackTimestamp(TimeSpan.FromMilliseconds(10));
        TestAssert.Equal("cooldown", Phase(rig.Current())); TestAssert.Equal("Classic", Design(rig.Current()));
        rig.Clock.Advance(TimeSpan.FromMilliseconds(199)); TestAssert.Equal("cooldown", Phase(rig.Current()));
        rig.Clock.Advance(TimeSpan.FromMilliseconds(1)); TestAssert.True(Phase(rig.Current()) is null, "Restarted remaining cooldown stays bounded200ms");
        TestAssert.Equal(applied.MenuRevision, rig.Current().MenuRevision); rig.NoGameWork();
    }

    internal static string? Phase(StateDocument state) { using var json = JsonDocument.Parse(state.Serialize()); return json.RootElement.TryGetProperty("menuDesignTransition", out var value) && value.ValueKind == JsonValueKind.Object ? value.GetProperty("phase").GetString() : null; }
    internal static string? Design(StateDocument state) { using var json = JsonDocument.Parse(state.Serialize()); return json.RootElement.GetProperty("appearance").TryGetProperty("design", out var value) ? value.GetString() : null; }
    internal sealed class Rig
    {
        internal SessionBoundary Session { get; } = new(); internal ManualTimeProvider Clock { get; } = new(); internal BackendController Controller { get; }
        internal ulong Sequence { get; set; } = 1;
        internal ManualTimeProvider SetupClock { get; }
        internal Rig(bool controls = true, bool preview = true, TimeProvider? clock = null, bool widthControls = false) { SetupClock = clock is IManualClockFixture fixture ? fixture.Time : Clock; Session.State = Session.State with { Preview = preview }; Controller = new(42, Session, controls, widthEditorControlsAvailable: widthControls, clock: clock ?? Clock); }
        internal StateDocument Current() => Controller.Snapshot();
        internal void Send(OverlayInput input, bool focused = true) => Controller.Consume(new(Sequence++, 1, 42, input, true, input >= OverlayInput.Close ? Current().NavigationRevision : null), () => focused);
        internal void Open() { Send(OverlayInput.Toggle); MenuOpenTestHarness.Settle(Controller, SetupClock); Select("Menu Customization"); Select("Menu Preferences"); }
        internal void Highlight(string label) { var rows = Current().Rows; int target = rows.ToList().FindIndex(row => row.Label == label); TestAssert.True(target >= 0, "Required row missing: " + label); for (int index = 0; index < rows.Count && Current().SelectedIndex != target; index++) Send(OverlayInput.Down); }
        internal void Select(string label) { Highlight(label); Send(OverlayInput.Select); }
        internal void Stage(string name) { Highlight("Design"); for (int moves = 0; moves < Names.Length && Current().Rows[0].Value != name; moves++) Send(OverlayInput.NextChoice); TestAssert.Equal(name, Current().Rows[0].Value); }
        internal void NoGameWork() { Controller.StartQueuedAction(); TestAssert.Equal(0, Session.Executed.Count); TestAssert.Equal(0, Session.ShutdownCalls); }
    }
}
