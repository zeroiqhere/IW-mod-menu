using Apparition.Backend.OpenControls;

namespace syndishanx_iw_tool.Overlay;

// Input policies and cached row presentation remain separate immutable owners
public sealed record OpeningControlsView(MainOpeningChord Chord, MainOpeningChord DisplayChord,
    OpeningControlDraft AppliedSelector, OpeningControlDraft CandidateSelector,
    VerifiedOpeningActions Capabilities, bool SelectorControlsAvailable,
    QuickOpeningPolicies? Policies = null, QuickOpeningPolicies? DisplayPolicies = null, bool LocalOpeningVerified = false) {
    // Cached permission changes guidance only; fresh session validation still admits each opening
    public string QuickOpeningGuide => !LocalOpeningVerified
        ? "Quick opening is unavailable until local player life and menu authorization are verified"
        : Policies?.QuickDisabled == true ? "Quick Menu opening is disabled by its local preference"
        : "Hold all assigned Quick actions while alive" + (Capabilities.Contains(OpeningAction.Aim) && Capabilities.Contains(OpeningAction.Jump)
            ? "; Aim + Jump opens the Quick Menu while dead" : "; dead-player opening requires verified Aim and Jump bindings");

    public bool CanEdit => SelectorControlsAvailable;
    public bool CanConfigureQuick => Policies is not null;

    // A captured pair cannot supply Quick authority when no actual pair was verified
    public QuickOpeningPolicies? PresentedPolicies => Policies is null ? null : DisplayPolicies ?? Policies;
}
