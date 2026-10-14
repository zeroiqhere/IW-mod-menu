using Apparition.Backend.Controller;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class InstructionPositionEditorTests {
    internal sealed class Visit {
        private readonly bool preview;
        public ManualTimeProvider Clock { get; } = new();
        public SessionBoundary Session { get; } = new();
        public BackendController Controller { get; }
        public ulong Sequence = 1;
        public Visit(bool controls = true, bool preview = true) {
            this.preview = preview;
            Controller = new(42, Session, clock: Clock, instructionPositionEditorControlsAvailable: controls);
        }
        private bool initialized;
        public void Initialize() {
            if (initialized) return;
            initialized = true;
            Session.State = Session.State with { Preview = preview };
            Send(OverlayInput.Toggle); MenuOpenTestHarness.Settle(Controller, Clock); Select("Menu Customization"); Select("Menu Instructions");
        }
        public void Send(OverlayInput input) => Controller.Consume(new(Sequence++, 1, 42, input, true,
            input >= OverlayInput.Close ? Controller.Snapshot().NavigationRevision : null), () => true);
        public void Choose(string label) {
            var current = Controller.Snapshot(); int row = current.Rows.ToList().FindIndex(value => value.Label == label);
            TestAssert.True(row >= 0, "The actual current menu must expose " + label);
            for (int moved = 0; moved < current.Rows.Count && Controller.Snapshot().SelectedIndex != row; moved++) Send(OverlayInput.Down);
        }
        public void Select(string label) { Choose(label); Send(OverlayInput.Select); }
        public void Enter() { Initialize(); Select("Reposition"); }
        public void Editing() { Enter(); Clock.Advance(TimeSpan.FromMilliseconds(100)); Controller.Snapshot(); }
        public void FinishAndUnlock() {
            Send(OverlayInput.FinishInstructionPositionEditor); Clock.Advance(TimeSpan.FromMilliseconds(100)); Controller.Snapshot();
            Clock.Advance(TimeSpan.FromMilliseconds(150)); Controller.Snapshot();
        }
        public void AssertNoGameWork() {
            Controller.StartQueuedAction(); TestAssert.Equal(0, Session.Executed.Count); TestAssert.Equal(0, Session.ShutdownCalls);
            TestAssert.True(!Controller.ExitRequested, "Position changes cannot become a game session exit");
        }

    }

    internal static Visit Open(bool controls = true) {
        var visit = new Visit(controls);
        visit.Initialize(); return visit;
    }
    public static void RepositionNeedsVisibleInstructionsAndExactControlsButResetAlwaysRemainsAvailable() {
        foreach (bool controls in new[] { false, true }) {
            var visit = Open(controls); var normal = visit.Controller.Snapshot();
            TestAssert.Equal(controls, normal.Rows[4].Enabled);
            TestAssert.True(normal.Rows[5].Enabled && normal.Rows[5].Value == "", "Reset is local and does not need movement controls");
            visit.Select("Disable"); var hidden = visit.Controller.Snapshot();
            TestAssert.True(!hidden.Rows[4].Enabled && hidden.Rows[5].Enabled, "Hidden instructions cannot enter the editor but can reset");
            visit.Select("Reposition"); TestAssert.Equal("Menu Instructions", visit.Controller.Snapshot().Title);
            visit.Select("Reset Position"); TestAssert.True(visit.Controller.Snapshot().Appearance?.InstructionPosition is null, "Hidden reset must derive defaults");
            visit.AssertNoGameWork();
        }
    }
    public static void EntryEditingAndExitUseExactHundredMillisecondPhasesThenExistingSoftUnlock() {
        var visit = Open(); visit.Enter(); var entering = visit.Controller.Snapshot();
        TestAssert.Equal("entering", entering.InstructionPositionEditor?.Phase);
        TestAssert.Equal("Menu Instructions", entering.Title);
        TestAssert.True(entering.IsOpen && !entering.IsRootMenu && entering.Rows.Count == 0 && entering.SelectedIndex == 0
            && entering.WidthEditor is null && entering.MenuUnlock is null, "The editor needs dedicated metadata rather than fabricated rows");
        ulong epoch = entering.NavigationRevision;
        visit.Clock.Advance(TimeSpan.FromMilliseconds(99)); TestAssert.Equal("position:entering", visit.Controller.ReadMenuPublicationPhase());
        visit.Send(OverlayInput.MoveInstructionsRight); TestAssert.True(visit.Controller.Snapshot().Appearance?.InstructionPosition is null, "Entering controls remain locked");
        visit.Clock.Advance(TimeSpan.FromMilliseconds(1)); TestAssert.Equal("position:editing", visit.Controller.ReadMenuPublicationPhase());
        visit.Send(OverlayInput.MoveInstructionsRight); visit.Send(OverlayInput.MoveInstructionsDown);
        TestAssert.Equal(new InstructionPosition(263, 480), visit.Controller.Snapshot().Appearance?.InstructionPosition);
        TestAssert.Equal(epoch, visit.Controller.Snapshot().NavigationRevision);
        visit.Send(OverlayInput.FinishInstructionPositionEditor); TestAssert.Equal("exiting", visit.Controller.Snapshot().InstructionPositionEditor?.Phase);
        visit.Clock.Advance(TimeSpan.FromMilliseconds(99)); visit.Send(OverlayInput.MoveInstructionsRight);
        TestAssert.Equal(new InstructionPosition(263, 480), visit.Controller.Snapshot().Appearance?.InstructionPosition);
        visit.Clock.Advance(TimeSpan.FromMilliseconds(1)); TestAssert.Equal("rebuild", visit.Controller.ReadMenuPublicationPhase());
        var parent = visit.Controller.Snapshot(); TestAssert.Equal("Menu Instructions", parent.Title);
        TestAssert.True(parent.InstructionPositionEditor is null && parent.MenuUnlock == "rebuild", "Exit must restore its parent and start SoftUnlock");
        TestAssert.Equal("Reposition", parent.Rows[parent.SelectedIndex].Label);
        visit.Clock.Advance(TimeSpan.FromMilliseconds(50)); TestAssert.Equal("pause", visit.Controller.ReadMenuPublicationPhase());
        visit.Clock.Advance(TimeSpan.FromMilliseconds(100)); TestAssert.True(visit.Controller.ReadMenuPublicationPhase() is null, "The full150ms pause must finish");
        visit.AssertNoGameWork();
    }
    public static void EveryMovementAndFinishRequireFreshExactPidEpochAndBothFocusChecks() {
        foreach (var input in PositionInputs) {
            var visit = Open(); visit.Editing(); var before = visit.Controller.Snapshot();
            visit.Controller.Consume(new(visit.Sequence++, 1, 99, input, true, before.NavigationRevision), () => true);
            visit.Controller.Consume(new(visit.Sequence++, 1, 42, input, true), () => true);
            visit.Controller.Consume(new(visit.Sequence++, 1, 42, input, true, before.NavigationRevision - 1), () => true);
            visit.Controller.Consume(new(visit.Sequence++, 1, 42, input, true, before.NavigationRevision), () => false);
            int reads = 0;
            visit.Controller.Consume(new(visit.Sequence++, 1, 42, input, true, before.NavigationRevision), () => ++reads == 1);
            visit.Send(input == OverlayInput.FinishInstructionPositionEditor ? OverlayInput.Up : OverlayInput.Select);
            ulong stale = visit.Sequence - 2;
            visit.Controller.Consume(new(stale, 1, 42, input, true, before.NavigationRevision), () => true);
            var after = visit.Controller.Snapshot();
            TestAssert.Equal(before.Appearance, after.Appearance); TestAssert.Equal(before.InstructionPositionEditor, after.InstructionPositionEditor);
            TestAssert.Equal(before.NavigationRevision, after.NavigationRevision); visit.AssertNoGameWork();
        }
    }
    public static void OrdinaryNavigationAndOtherEditorInputsCannotActDuringAPositionVisit() {
        var visit = Open(); visit.Editing(); var before = visit.Controller.Snapshot();
        foreach (var input in new[] { OverlayInput.Up, OverlayInput.Down, OverlayInput.Select, OverlayInput.Back,
            OverlayInput.PreviousChoice, OverlayInput.NextChoice, OverlayInput.IncreaseWidth, OverlayInput.FinishWidthEditor }) visit.Send(input);
        var after = visit.Controller.Snapshot(); TestAssert.Equal(before.Appearance, after.Appearance);
        TestAssert.Equal(before.InstructionPositionEditor, after.InstructionPositionEditor); TestAssert.Equal(before.NavigationRevision, after.NavigationRevision);
        visit.AssertNoGameWork();
    }
    public static void ToggleAndShutdownAbortToTheParentWithoutStartingUnlockOrResumingTheEditor() {
        foreach (bool shutdown in new[] { false, true }) {
            var visit = Open(); visit.Editing(); visit.Send(OverlayInput.MoveInstructionsLeft);
            if (shutdown) visit.Controller.RequestShutdown(); else visit.Send(OverlayInput.Toggle); MenuOpenTestHarness.Settle(visit.Controller, visit.Clock);
            var stopped = shutdown ? visit.Controller.ClosedState() : visit.Controller.Snapshot();
            TestAssert.True(!stopped.IsOpen && stopped.InstructionPositionEditor is null && stopped.MenuUnlock is null, "Abort must clear transient editor metadata");
            TestAssert.Equal(new InstructionPosition(247, 472), stopped.Appearance?.InstructionPosition);
            if (!shutdown) {
                visit.Send(OverlayInput.Toggle); MenuOpenTestHarness.Settle(visit.Controller, visit.Clock); var reopened = visit.Controller.Snapshot();
                TestAssert.Equal("Menu Instructions", reopened.Title); TestAssert.True(reopened.InstructionPositionEditor is null, "Reopen must resume the parent");
                TestAssert.Equal("Reposition", reopened.Rows[reopened.SelectedIndex].Label); visit.AssertNoGameWork();
            }
        }
    }
    public static void OldEditorCommandsCannotApplyAfterAbortAndASecondVisit() {
        var visit = Open(); visit.Editing(); ulong oldEpoch = visit.Controller.Snapshot().NavigationRevision;
        visit.Send(OverlayInput.Toggle); MenuOpenTestHarness.Settle(visit.Controller, visit.Clock); visit.Send(OverlayInput.Toggle); MenuOpenTestHarness.Settle(visit.Controller, visit.Clock); visit.Select("Reposition");
        visit.Clock.Advance(TimeSpan.FromMilliseconds(100)); visit.Controller.Snapshot();
        foreach (var input in PositionInputs) visit.Controller.Consume(new(visit.Sequence++, 1, 42, input, true, oldEpoch), () => true);
        TestAssert.True(visit.Controller.Snapshot().Appearance?.InstructionPosition is null, "Retired visit packets cannot move the reopened editor");
        TestAssert.Equal("editing", visit.Controller.Snapshot().InstructionPositionEditor?.Phase); visit.AssertNoGameWork();
    }
    public static void ResetClearsOnlyPositionAndAlternateChangesUseTheirCorrectDerivedDefaults() {
        var visit = Open(); visit.Select("Alternate Style"); visit.Editing(); visit.Send(OverlayInput.MoveInstructionsRight);
        TestAssert.Equal(new InstructionPosition(328, 472), visit.Controller.Snapshot().Appearance?.InstructionPosition);
        visit.FinishAndUnlock(); var before = visit.Controller.Snapshot().Appearance!; visit.Select("Reset Position");
        TestAssert.Equal(before with { InstructionPosition = null }, visit.Controller.Snapshot().Appearance);
        visit.Select("Disable"); visit.Select("Reset Position");
        TestAssert.True(visit.Controller.Snapshot().Appearance is { InstructionsVisible: false, AlternateInstructions: true, InstructionPosition: null },
            "Hidden reset must not change visibility or style"); visit.AssertNoGameWork();
    }
    public static void FreshEntryFocusLossRejectsTheEditorUntilAnotherDeliberateSelect() {
        var visit = Open(); visit.Choose("Reposition");
        int reads = 0;
        ulong rejected = visit.Sequence++;
        visit.Controller.Consume(new(rejected, 1, 42, OverlayInput.Select, true), () => ++reads == 1);
        TestAssert.True(visit.Controller.Snapshot().InstructionPositionEditor is null,
            "Focus lost after the selected row refresh cannot enter the editor");
        visit.Controller.Consume(new(rejected, 1, 42, OverlayInput.Select, true), () => true);
        TestAssert.True(visit.Controller.Snapshot().InstructionPositionEditor is null, "Acknowledged entry cannot replay");
        visit.Send(OverlayInput.Select);
        TestAssert.Equal("entering", visit.Controller.Snapshot().InstructionPositionEditor?.Phase);
        visit.AssertNoGameWork();
    }
    public static void FocusRejectedAndReplayedResetCannotClearCommittedCoordinates() {
        var visit = Open(); visit.Editing(); visit.Send(OverlayInput.MoveInstructionsRight); visit.FinishAndUnlock();
        visit.Choose("Reset Position");
        var committed = visit.Controller.Snapshot().Appearance;
        int reads = 0;
        ulong rejected = visit.Sequence++;
        visit.Controller.Consume(new(rejected, 1, 42, OverlayInput.Select, true), () => ++reads == 1);
        visit.Controller.Consume(new(rejected, 1, 42, OverlayInput.Select, true), () => true);
        visit.Controller.Consume(new(visit.Sequence++, 1, 99, OverlayInput.Select, true), () => true);
        TestAssert.Equal(committed, visit.Controller.Snapshot().Appearance);
        visit.Send(OverlayInput.Select);
        TestAssert.Equal(committed! with { InstructionPosition = null }, visit.Controller.Snapshot().Appearance);
        visit.AssertNoGameWork();
    }
    public static void CheapPhaseQueriesAdvanceEditorAndParentWithoutReadingOrObservingSessionWork() {
        var visit = Open(); visit.Enter(); visit.Session.RejectMenuStateReads = true;
        int reads = visit.Session.MenuStateReads;
        visit.Clock.Advance(TimeSpan.FromMilliseconds(100)); TestAssert.Equal("position:editing", visit.Controller.ReadMenuPublicationPhase());
        TestAssert.Equal(reads, visit.Session.MenuStateReads); TestAssert.True(visit.Controller.ReadMenuUnlockPhase() is null, "Legacy unlock query remains unlock-only");
        visit.Session.RejectMenuStateReads = false; visit.Send(OverlayInput.FinishInstructionPositionEditor);
        visit.Session.RejectMenuStateReads = true; reads = visit.Session.MenuStateReads;
        visit.Clock.Advance(TimeSpan.FromMilliseconds(100)); TestAssert.Equal("rebuild", visit.Controller.ReadMenuPublicationPhase());
        TestAssert.Equal(reads, visit.Session.MenuStateReads);
    }
    public static void PendingGameWorkRemainsIndependentAndCheapPhaseQueriesDoNotObserveItsCompletion() {
        var visit = new Visit(preview: false); visit.Initialize();
        visit.Send(OverlayInput.Back); visit.Send(OverlayInput.Back);
        visit.Session.Pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        visit.Select("Connect after spawning"); visit.Controller.StartQueuedAction();
        visit.Select("Menu Customization"); visit.Select("Menu Instructions"); visit.Select("Reposition");
        visit.Clock.Advance(TimeSpan.FromMilliseconds(100)); visit.Send(OverlayInput.MoveInstructionsRight);
        TestAssert.True(visit.Controller.Busy && visit.Controller.Snapshot().Appearance?.InstructionPosition == new InstructionPosition(263, 472),
            "Local editor movement must remain usable during the original pending operation");
        visit.Session.Pending.SetResult(); visit.Session.RejectMenuStateReads = true;
        int reads = visit.Session.MenuStateReads;
        TestAssert.Equal("position:editing", visit.Controller.ReadMenuPublicationPhase());
        TestAssert.True(visit.Controller.Busy && visit.Session.MenuStateReads == reads,
            "A cheap phase query cannot read or observe completed game work");
        visit.Session.RejectMenuStateReads = false; visit.Controller.ObserveCompletion();
        TestAssert.True(!visit.Controller.Busy, "Only explicit completion observation may release the operation");
        TestAssert.Equal(1, visit.Session.Executed.Count); TestAssert.Equal(0, visit.Session.ShutdownCalls);
        visit.FinishAndUnlock(); TestAssert.Equal(new InstructionPosition(263, 472), visit.Controller.Snapshot().Appearance?.InstructionPosition);
    }

    public static void ClockRollbackAndSignedOverflowRestartTheCurrentTimedPhaseWithoutChangingCoordinates() {
        foreach (bool exiting in new[] { false, true }) {
            var visit = Open(); visit.Enter();
            if (exiting) { visit.Clock.Advance(TimeSpan.FromMilliseconds(100)); visit.Send(OverlayInput.FinishInstructionPositionEditor); }
            string phase = exiting ? "exiting" : "entering";
            visit.Clock.Advance(TimeSpan.FromMilliseconds(75)); visit.Controller.ReadMenuPublicationPhase();
            visit.Clock.RollBackTimestamp(TimeSpan.FromMilliseconds(50)); TestAssert.Equal("position:" + phase, visit.Controller.ReadMenuPublicationPhase());
            visit.Clock.Advance(TimeSpan.FromMilliseconds(99)); TestAssert.Equal("position:" + phase, visit.Controller.ReadMenuPublicationPhase());
            visit.Clock.Advance(TimeSpan.FromMilliseconds(1)); TestAssert.Equal(exiting ? "rebuild" : "position:editing", visit.Controller.ReadMenuPublicationPhase());
            TestAssert.True(visit.Controller.Snapshot().Appearance?.InstructionPosition is null, "Clock faults cannot invent coordinates");
        }
        var overflow = Open(); overflow.Clock.RollBackTimestamp(TimeSpan.MaxValue); overflow.Clock.RollBackTimestamp(TimeSpan.FromTicks(1));
        overflow.Enter(); overflow.Clock.Advance(TimeSpan.MaxValue); overflow.Clock.Advance(TimeSpan.FromTicks(1));
        TestAssert.Equal("position:entering", overflow.Controller.ReadMenuPublicationPhase());
        overflow.Clock.Advance(TimeSpan.FromMilliseconds(100)); TestAssert.Equal("position:editing", overflow.Controller.ReadMenuPublicationPhase());
    }
    internal static OverlayInput[] PositionInputs => [OverlayInput.MoveInstructionsUp, OverlayInput.MoveInstructionsDown,
        OverlayInput.MoveInstructionsLeft, OverlayInput.MoveInstructionsRight, OverlayInput.FinishInstructionPositionEditor];
}
