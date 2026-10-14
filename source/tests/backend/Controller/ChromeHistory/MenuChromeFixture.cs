using Apparition.Backend.Controller;
using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

// Source-scoped UI visits use the real controller and an inert connected session
internal sealed class MenuChromeFixture {
    public ManualTimeProvider Clock { get; }
    public ScopedOpeningFixture.OpeningSession Session { get; } = new();
    public BackendController Controller { get; }
    private ulong sequence;

    public MenuChromeFixture(ulong chromeRevision = 0, OverlayNavigation? navigation = null, TimeProvider? clock = null, ulong themeRevision = 0) {
        Clock = clock is IManualClockFixture manual ? manual.Time : new();
        TimeProvider sourceClock = clock ?? Clock;
        if (chromeRevision != 0 || navigation is not null || themeRevision != 0) {
            Controller = new(42, Session, navigation ?? new(), false, choiceControlsAvailable: true, clock: sourceClock,
                openingActions: QuickOpeningTestSupport.AllCapabilities(), menuChromeRevision: chromeRevision, chromeThemeRevision: themeRevision);
            return;
        }
        Controller = new(42, Session, choiceControlsAvailable: true, widthEditorControlsAvailable: true,
            clock: sourceClock, instructionPositionEditorControlsAvailable: true, menuPositionEditorControlsAvailable: true,
            openingActions: QuickOpeningTestSupport.AllCapabilities());
    }

    public StateDocument Current() => Controller.Snapshot();
    public void Send(OverlayInput input) {
        var frame = Current();
        Controller.Consume(new(++sequence, 1, 42, input, true,
            input >= OverlayInput.Close ? frame.NavigationRevision : null), () => true);
    }
    public void Open(OverlayInput input = OverlayInput.OpenMain) {
        var frame = Current();
        Controller.Consume(new(++sequence, 1, 42, input, true, frame.NavigationRevision,
            frame.OpeningPolicies?.Revision ?? frame.MainOpeningControls!.Revision, Session.Admission.Revision), () => true);
        Controller.StartQueuedAction();
        TestAssert.True(SpinWait.SpinUntil(() => { Controller.ObserveCompletion(); return !Controller.Busy; }, TimeSpan.FromSeconds(3)),
            "The owned opening must finish its retained validation");
    }
    public void SettleMain() {
        Open();
        Clock.Advance(TimeSpan.FromMilliseconds(50));
        Current();
        Clock.Advance(TimeSpan.FromMilliseconds(700));
        Current();
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
        var state = Current();
        int target = state.Rows.ToList().FindIndex(row => row.Label == label);
        TestAssert.True(target >= 0, "Missing actual row: " + label);
        for (int scanned = 0; scanned < state.Rows.Count && Current().SelectedIndex != target; scanned++) Send(OverlayInput.Down);
        TestAssert.Equal(target, Current().SelectedIndex);
    }
    public void Select(string label) {
        Highlight(label);
        Send(OverlayInput.Select);
    }
    public void Preferences() {
        Select("Menu Customization");
        Select("Menu Preferences");
    }
    public void Wait(int milliseconds) {
        Clock.Advance(TimeSpan.FromMilliseconds(milliseconds));
        Current();
    }
    public void NoGameWrites() => TestAssert.Equal(0, Session.Executed.Count);
}
