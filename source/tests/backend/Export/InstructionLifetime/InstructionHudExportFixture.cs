using Apparition.Backend.Controller;
using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

// Commands retain the exact controller scope while the inert session owns the proof
internal sealed class InstructionHudExportFixture {
    public ManualTimeProvider Clock { get; } = new();
    public ScopedOpeningFixture.OpeningSession Session { get; } = new();
    public BackendController Controller { get; }
    private ulong sequence;

    public InstructionHudExportFixture() {
        Controller = new(42, Session, choiceControlsAvailable: true, widthEditorControlsAvailable: true,
            clock: Clock, instructionPositionEditorControlsAvailable: true, menuPositionEditorControlsAvailable: true,
            openingActions: QuickOpeningTestSupport.AllCapabilities());
    }
    public StateDocument Current() => Controller.Snapshot();
    public void Send(OverlayInput input) {
        var frame = Current();
        bool disableQuick = input == OverlayInput.Select && frame.Rows.Count > 0
            && frame.Rows[frame.SelectedIndex].Label == "Disable Quick Menu";
        Controller.Consume(new(++sequence, 1, 42, input, true,
            input >= OverlayInput.Close || disableQuick ? frame.NavigationRevision : null,
            disableQuick ? frame.OpeningPolicies!.Revision : null,
            input == OverlayInput.JumpBack ? Session.Admission.Revision : null), () => true);
    }
    public void Open(OverlayInput input = OverlayInput.OpenMain) {
        var frame = Current();
        Controller.Consume(new(++sequence, 1, 42, input, true, frame.NavigationRevision,
            frame.OpeningPolicies!.Revision, Session.Admission.Revision), () => true);
        Controller.StartQueuedAction();
        TestAssert.True(SpinWait.SpinUntil(() => { Controller.ObserveCompletion(); return !Controller.Busy; }, TimeSpan.FromSeconds(3)),
            "The inert owned opening must finish its fresh validation");
    }
    public void Wait(int milliseconds) {
        Clock.Advance(TimeSpan.FromMilliseconds(milliseconds));
        Current();
    }
    public void SettleMain() {
        Open();
        Wait(50);
        Wait(700);
    }
    // Setup navigation waits explicitly for a previously admitted mechanical branch
    public void CompleteMonitorWait() {
        if (Current().MenuMonitorWait is { } wait) Wait(wait.Kind == "choice" ? 180 : 250);
    }
    public void StageChoice(OverlayInput input) {
        Send(input);
        Wait(180);
    }
    public void Highlight(string label) {
        CompleteMonitorWait();
        var frame = Current();
        int index = frame.Rows.ToList().FindIndex(row => row.Label == label);
        TestAssert.True(index >= 0, "Missing actual row: " + label);
        for (int scanned = 0; scanned < frame.Rows.Count && Current().SelectedIndex != index; scanned++) Send(OverlayInput.Down);
        TestAssert.Equal(index, Current().SelectedIndex);
    }
    public void Select(string label) { Highlight(label); Send(OverlayInput.Select); }
    public void Preferences() { Select("Menu Customization"); Select("Menu Preferences"); }
    public void BeginClassicDesign() {
        SettleMain();
        Preferences();
        Highlight("Design");
        StageChoice(OverlayInput.NextChoice);
        Send(OverlayInput.Select);
    }
    public void NoGameWrites() => TestAssert.Equal(0, Session.Executed.Count);
}
