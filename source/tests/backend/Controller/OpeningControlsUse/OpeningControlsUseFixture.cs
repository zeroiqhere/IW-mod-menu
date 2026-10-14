using Apparition.Backend.Controller;
using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

// The fixture keeps real controller admission while commands match native captures
internal sealed class OpeningControlsUseFixture {
    internal sealed record CommandCapture(StateDocument ObservedFrame, CommandEnvelope WireCommand,
        StateDocument ImmediateFrame, StateDocument? WaitFrame = null);

    private readonly ScopedOpeningFixture scoped;
    private readonly TimeProvider clock;

    public BackendController Controller => scoped.Controller;
    public ScopedOpeningFixture.OpeningSession Session => scoped.Session;
    public OverlayNavigation Navigation => scoped.Navigation;
    public TimeProvider Clock => clock;
    public int SessionActionCount => Session.Executed.Count;
    public bool Focus { get => scoped.Focus; set => scoped.Focus = value; }
    public StateDocument Frame => Controller.Snapshot();

    private OpeningControlsUseFixture(ScopedOpeningFixture scoped, TimeProvider clock) {
        this.scoped = scoped;
        this.clock = clock;
    }

    public static OpeningControlsUseFixture Ready(MenuPage page = MenuPage.OpenControls,
        string? selectedLabel = null, TimeProvider? clock = null, ulong booleanPaintRevision = 0) {
        var activeClock = clock ?? new ManualTimeProvider();
        var scoped = new ScopedOpeningFixture(clock: activeClock, booleanPaintRevision: booleanPaintRevision,
            choiceControlsAvailable: true);
        var fixture = new OpeningControlsUseFixture(scoped, activeClock);
        // The ordinary source visit is ready only after the initial 50 plus 550 ms loop waits
        scoped.Open(OverlayInput.OpenMain);
        fixture.AdvanceClock(50);
        _ = scoped.Controller.Snapshot();
        fixture.AdvanceClock(550);
        _ = scoped.Controller.Snapshot();
        scoped.Navigation.OpenPage(page);
        _ = scoped.Controller.Snapshot();
        if (selectedLabel is not null) fixture.SelectRow(selectedLabel);
        return fixture;
    }

    public StateDocument SelectRow(string label) {
        var frame = Frame;
        int index = frame.Rows.ToList().FindIndex(row => row.Label == label);
        TestAssert.True(index >= 0, "Required source row is missing: " + label);
        Navigation.SelectIndex(index);
        return Frame;
    }

    public CommandEnvelope NativeCommand(OverlayInput input, StateDocument observedFrame,
        ulong? navigationRevision = null, ulong? openingPolicyRevision = null) {
        bool capturesOpeningPolicy = input is OverlayInput.Select or OverlayInput.PreviousChoice or OverlayInput.NextChoice;
        ulong? policy = capturesOpeningPolicy
            ? openingPolicyRevision ?? observedFrame.OpeningPolicies?.Revision ?? observedFrame.MainOpeningControls?.Revision
            : null;
        bool mechanicalChoice = input is OverlayInput.PreviousChoice or OverlayInput.NextChoice;
        // Native Select sends its observed navigation and policy epochs only
        return new(checked(Controller.LastSequence + 1), 1, 42, input, true,
            navigationRevision ?? observedFrame.NavigationRevision, policy,
            mechanicalChoice ? Session.Admission.Revision : null,
            mechanicalChoice ? observedFrame.MenuRevision : null);
    }

    public CommandCapture Capture(OverlayInput input, ulong? navigationRevision = null,
        ulong? openingPolicyRevision = null) {
        var observed = Frame;
        var command = NativeCommand(input, observed, navigationRevision, openingPolicyRevision);
        Controller.Consume(command, () => scoped.Focus);
        return new(observed, command, Frame);
    }

    public CommandCapture StageChoice(OverlayInput input) {
        if (input is not (OverlayInput.PreviousChoice or OverlayInput.NextChoice))
            throw new ArgumentOutOfRangeException(nameof(input));
        var capture = Capture(input);
        AdvanceClock(180);
        return capture with { WaitFrame = Frame };
    }

    public StateDocument Wait(int milliseconds) {
        if (milliseconds < 0) throw new ArgumentOutOfRangeException(nameof(milliseconds));
        AdvanceClock(milliseconds);
        return Frame;
    }

    private void AdvanceClock(int milliseconds) {
        switch (clock) {
            case ManualTimeProvider manual:
                manual.Advance(TimeSpan.FromMilliseconds(milliseconds));
                break;
            case LocalOptionCallbackControllerTests.SwitchableClock switchable:
                switchable.Advance(milliseconds);
                break;
            default:
                throw new InvalidOperationException("The controller fixture requires a controllable source clock");
        }
    }
}
