using Apparition.Backend.Controller;
using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

// The real controller owns navigation; only its initial checked counter is supplied
internal sealed class InstructionHudBoundaryFixture {
    public ManualTimeProvider Clock { get; } = new();
    public ScopedOpeningFixture.OpeningSession Session { get; } = new();
    public BackendController Controller { get; }
    private ulong sequence;
    public InstructionHudBoundaryFixture(ulong counter, OverlayNavigation? navigation = null, ulong themeCounter = 0) {
        Controller = new(42, Session, navigation ?? new(), false, choiceControlsAvailable: true,
            clock: Clock, openingActions: QuickOpeningTestSupport.AllCapabilities(), instructionCreationRevision: counter, instructionThemeRevision: themeCounter);
    }
    public StateDocument Current() => Controller.Snapshot();
    public void Open() {
        var frame = Current();
        Controller.Consume(new(++sequence, 1, 42, OverlayInput.OpenMain, true, frame.NavigationRevision,
            frame.OpeningPolicies!.Revision, Session.Admission.Revision), () => true);
        Controller.StartQueuedAction();
        TestAssert.True(SpinWait.SpinUntil(() => { Controller.ObserveCompletion(); return !Controller.Busy; }, TimeSpan.FromSeconds(3)),
            "The owned boundary opening must finish its proof");
        Clock.Advance(TimeSpan.FromMilliseconds(50));
        Current();
        Clock.Advance(TimeSpan.FromMilliseconds(700));
        Current();
    }
    public void Send(OverlayInput input) => Controller.Consume(new(++sequence, 1, 42, input, true), () => true);
    public void StageChoice(OverlayInput input) {
        Send(input);
        Clock.Advance(TimeSpan.FromMilliseconds(180));
        Current();
    }
    public void Select(string label) {
        if (Current().MenuMonitorWait is { } wait) {
            Clock.Advance(TimeSpan.FromMilliseconds(wait.Kind == "choice" ? 180 : 250));
            Current();
        }
        var frame = Current();
        int target = frame.Rows.ToList().FindIndex(row => row.Label == label);
        TestAssert.True(target >= 0, "The boundary fixture requires the actual row " + label);
        for (int scan = 0; scan < 128 && Current().SelectedIndex != target; scan++) Send(OverlayInput.Down);
        TestAssert.Equal(target, Current().SelectedIndex);
        Send(OverlayInput.Select);
    }
}
