using System.Text.Json;
using Apparition.Backend.Controller;
using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class MenuPositionEditorTests
{
    public static void EntryRequiresExactAvailableControlsAndImmediatelyPublishesTheSourceEmptyPanel()
    {
        var missing = new Rig(false); missing.Open();
        TestAssert.Equal("UNAVAILABLE", missing.Current().Rows[3].Value);
        TestAssert.True(!missing.Current().Rows[3].Enabled, "Missing exact movement and grenade bindings cannot enable the editor");
        missing.Select("Reposition Menu"); TestAssert.True(!IsEditor(missing.Current()), "Disabled selection must remain on its parent");
        var rig = new Rig(); rig.Enter(); var state = rig.Current();
        TestAssert.True(IsEditor(state) && state.IsOpen && !state.IsRootMenu && state.Rows.Count == 0 && state.SelectedIndex == 0,
            "Reposition starts immediately in a dedicated empty child panel");
        TestAssert.Equal("Menu Customization", state.Title); TestAssert.Equal(1, Step(state));
        TestAssert.True(state.Notice.Contains("Position X: 0 Y: 85; movement increment: 1", StringComparison.Ordinal),
            "The independent notice must expose coordinates and movement offset for recovery");
        TestAssert.True(state.MenuUnlock is null && state.WidthEditor is null && state.InstructionPositionEditor is null,
            "The new editor cannot coexist with another transient mode");
        TestAssert.True(Position(state) is null, "Entry retains the derived source default without inventing a stored position");
        rig.NoGameWork(); missing.NoGameWork();
    }

    public static void LiveMovementUsesTheCurrentSaturatingOffsetAndDoesNotChangeEitherEpoch()
    {
        var rig = new Rig(); rig.Enter(); var initial = rig.Current();
        rig.Send(OverlayInput.MoveMenuRight); rig.Send(OverlayInput.MoveMenuDown);
        TestAssert.Equal((1, 86), Position(rig.Current())!.Value);
        for (int step = 0; step < 20; step++) rig.Send(OverlayInput.IncreaseMenuPositionStep);
        TestAssert.Equal(10, Step(rig.Current()));
        rig.Send(OverlayInput.MoveMenuLeft); rig.Send(OverlayInput.MoveMenuUp);
        TestAssert.Equal((-9, 76), Position(rig.Current())!.Value);
        TestAssert.Equal(MenuAppearance.Default with { MenuPosition = new(-9, 76) }, rig.Current().Appearance);
        for (int step = 0; step < 20; step++) rig.Send(OverlayInput.DecreaseMenuPositionStep);
        TestAssert.Equal(1, Step(rig.Current()));
        TestAssert.Equal(initial.NavigationRevision, rig.Current().NavigationRevision);
        TestAssert.Equal(initial.MenuRevision, rig.Current().MenuRevision);
        rig.NoGameWork();
    }

    public static void FinishRestoresItsExactParentCursorThenRunsTheExistingTimedUnlockAndResetsOnlyTheVisitStep()
    {
        var rig = new Rig(); rig.Enter(); rig.Send(OverlayInput.MoveMenuRight); rig.Send(OverlayInput.IncreaseMenuPositionStep);
        ulong editorMenu = rig.Current().MenuRevision!.Value;
        rig.Send(OverlayInput.FinishMenuPositionEditor);
        var parent = rig.Current();
        TestAssert.True(!IsEditor(parent) && parent.MenuUnlock == "rebuild" && parent.MenuRevision > editorMenu,
            "Finish rebuilds the actual parent rather than exiting the session");
        TestAssert.Equal("Reposition Menu", parent.Rows[parent.SelectedIndex].Label);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(49)); TestAssert.Equal("rebuild", rig.Current().MenuUnlock);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(1)); TestAssert.Equal("pause", rig.Current().MenuUnlock);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(99)); TestAssert.Equal("pause", rig.Current().MenuUnlock);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(1)); TestAssert.True(rig.Current().MenuUnlock is null, "Unlock completes at 150 ms");
        rig.Send(OverlayInput.Select); TestAssert.Equal(1, Step(rig.Current()));
        TestAssert.Equal((1, 85), Position(rig.Current())!.Value); rig.NoGameWork();
    }

    public static void EveryEditorCommandRequiresFreshPidEpochAndBothFocusChecksAndCannotReplay()
    {
        var rig = new Rig(); rig.Enter();
        foreach (var input in Commands())
        {
            var original = rig.Current(); rig.Send(input, focused: false);
            ulong rejected = rig.Controller.LastSequence;
            rig.Controller.Consume(new(rejected, 1, 42, input, true, original.NavigationRevision), () => true);
            rig.Controller.Consume(new(rig.Sequence++, 1, 99, input, true, original.NavigationRevision), () => true);
            rig.Controller.Consume(new(rig.Sequence++, 1, 42, input, false, original.NavigationRevision), () => true);
            rig.Controller.Consume(new(rig.Sequence++, 1, 42, input, true, null), () => true);
            rig.Controller.Consume(new(rig.Sequence++, 1, 42, input, true, original.NavigationRevision + 1), () => true);
            // Focus can change after admission but before the local coordinate commit
            int reads = 0;
            rig.Controller.Consume(new(rig.Sequence++, 1, 42, input, true, original.NavigationRevision), () => ++reads == 1);
            TestAssert.True(IsEditor(rig.Current()), "Rejected Finish cannot leave its editor");
            TestAssert.Equal(Position(original), Position(rig.Current())); TestAssert.Equal(Step(original), Step(rig.Current()));
        }
        rig.Send(OverlayInput.MoveMenuRight); TestAssert.Equal((1, 85), Position(rig.Current())!.Value); rig.NoGameWork();
    }

    public static void OrdinaryInputsOtherEditorsAndOldVisitPacketsCannotChangeTheSpecialPanel()
    {
        var rig = new Rig(); rig.Enter(); var visit = rig.Current();
        foreach (var input in new[]{OverlayInput.Up,OverlayInput.Down,OverlayInput.Select,OverlayInput.Back,OverlayInput.PreviousChoice,
            OverlayInput.NextChoice,OverlayInput.IncreaseWidth,OverlayInput.FinishWidthEditor,OverlayInput.MoveInstructionsRight,OverlayInput.FinishInstructionPositionEditor}) rig.Send(input);
        TestAssert.True(IsEditor(rig.Current()), "Ordinary navigation cannot own reposition controls");
        TestAssert.Equal(visit.NavigationRevision, rig.Current().NavigationRevision);
        // Reopening creates a new visit even when the same parent row is selected
        rig.Send(OverlayInput.Toggle); MenuOpenTestHarness.Settle(rig.Controller, rig.Clock); rig.Send(OverlayInput.Toggle); MenuOpenTestHarness.Settle(rig.Controller, rig.Clock); rig.Send(OverlayInput.Select);
        foreach (var input in Commands()) rig.Controller.Consume(new(rig.Sequence++, 1, 42, input, true, visit.NavigationRevision), () => true);
        TestAssert.True(IsEditor(rig.Current()) && Position(rig.Current()) is null && Step(rig.Current()) == 1,
            "A second visit rejects every command from the retired visit"); rig.NoGameWork();
    }

    public static void ToggleAndShutdownLeaveTheEditorBeforeHidingWithoutStartingUnlockOrDiscardingPosition()
    {
        foreach (bool shutdown in new[] { false, true })
        {
            var rig = new Rig(); rig.Enter(); rig.Send(OverlayInput.MoveMenuLeft);
            if (shutdown)
            {
                rig.Controller.RequestShutdown(); var closed = rig.Controller.ClosedState();
                TestAssert.True(!closed.IsOpen && !IsEditor(closed) && closed.MenuUnlock is null, "Cleanup must clear editor metadata");
            }
            else
            {
                rig.Send(OverlayInput.Toggle); MenuOpenTestHarness.Settle(rig.Controller, rig.Clock); TestAssert.True(!rig.Current().IsOpen && !IsEditor(rig.Current()) && rig.Current().MenuUnlock is null, "Toggle aborts without a timed finish");
                rig.Send(OverlayInput.Toggle); MenuOpenTestHarness.Settle(rig.Controller, rig.Clock); TestAssert.Equal("Reposition Menu", rig.Current().Rows[rig.Current().SelectedIndex].Label);
            }
            TestAssert.Equal((-1, 85), Position(rig.Current())!.Value); rig.NoGameWork();
        }
    }

    public static void EntryRechecksFocusAndLocalPositionCannotFinishOrReplacePendingGameWork()
    {
        var rig = new Rig(); rig.Open(); rig.Highlight("Reposition Menu"); int reads = 0;
        rig.Controller.Consume(new(rig.Sequence++, 1, 42, OverlayInput.Select, true), () => ++reads == 1);
        TestAssert.True(!IsEditor(rig.Current()), "Entry requires a fresh focus check immediately before activation");
        rig.Send(OverlayInput.Select); TestAssert.True(IsEditor(rig.Current()), "A fresh focused Use can still enter"); rig.NoGameWork();
        var pending = new Rig(preview: false); pending.Session.Pending = new TaskCompletionSource();
        pending.Send(OverlayInput.Toggle); MenuOpenTestHarness.Settle(pending.Controller, pending.Clock); pending.Send(OverlayInput.Select); pending.Controller.StartQueuedAction();
        pending.Open(); pending.Select("Reposition Menu"); pending.Send(OverlayInput.MoveMenuDown); pending.Send(OverlayInput.FinishMenuPositionEditor);
        TestAssert.True(pending.Controller.Busy && pending.Current().MenuUnlock == "rebuild", "Local finish cannot complete pending session work");
        TestAssert.Equal(1, pending.Session.Executed.Count); pending.Session.Pending.SetResult(); pending.Controller.ObserveCompletion();
        TestAssert.Equal((0, 86), Position(pending.Current())!.Value); TestAssert.Equal(0, pending.Session.ShutdownCalls);
    }

    internal static bool IsEditor(StateDocument state) { using var json = JsonDocument.Parse(state.Serialize()); return json.RootElement.TryGetProperty("menuPositionEditor", out var value) && value.ValueKind == JsonValueKind.Object; }
    internal static int Step(StateDocument state) { using var json = JsonDocument.Parse(state.Serialize()); return json.RootElement.GetProperty("menuPositionEditor").GetProperty("step").GetInt32(); }
    internal static (int, int)? Position(StateDocument state) { using var json = JsonDocument.Parse(state.Serialize()); var p = json.RootElement.GetProperty("appearance").GetProperty("menuPosition"); return p.ValueKind == JsonValueKind.Null ? null : (p.GetProperty("x").GetInt32(), p.GetProperty("y").GetInt32()); }
    internal static OverlayInput[] Commands() => new[]{OverlayInput.MoveMenuUp,OverlayInput.MoveMenuDown,OverlayInput.MoveMenuLeft,OverlayInput.MoveMenuRight,
        OverlayInput.IncreaseMenuPositionStep,OverlayInput.DecreaseMenuPositionStep,OverlayInput.FinishMenuPositionEditor};
    internal sealed class Rig
    {
        internal SessionBoundary Session { get; } = new(); internal ManualTimeProvider Clock { get; } = new(); internal BackendController Controller { get; }
        internal ulong Sequence { get; set; } = 1;
        internal Rig(bool controls = true, bool preview = true) { Session.State = Session.State with { Preview = preview }; Controller = new(42, Session, clock: Clock, menuPositionEditorControlsAvailable: controls); }
        internal StateDocument Current() => Controller.Snapshot();
        internal void Send(OverlayInput input, bool focused = true) => Controller.Consume(new(Sequence++, 1, 42, input, true, input >= OverlayInput.Close ? Current().NavigationRevision : null), () => focused);
        internal void Open() { if (!Current().IsOpen) { Send(OverlayInput.Toggle); MenuOpenTestHarness.Settle(Controller, Clock); } Select("Menu Customization"); }
        internal void Enter() { Open(); Select("Reposition Menu"); }
        internal void Highlight(string label)
        {
            var state = Current(); int index = state.Rows.ToList().FindIndex(row => row.Label == label); TestAssert.True(index >= 0, "Required menu row missing: " + label);
            for (int count = 0; count < state.Rows.Count && Current().SelectedIndex != index; count++) Send(OverlayInput.Down); TestAssert.Equal(label, Current().Rows[Current().SelectedIndex].Label);
        }
        internal void Select(string label) { Highlight(label); Send(OverlayInput.Select); }
        internal void NoGameWork() { Controller.StartQueuedAction(); TestAssert.Equal(0, Session.Executed.Count); TestAssert.Equal(0, Session.ShutdownCalls); }
    }
}
