namespace syndishanx_iw_tool.FeatureState;

// Keep the requested value and the last confirmed value together
public readonly record struct ToggleResult(
    ToggleOutcome Outcome,
    bool RequestedState,
    bool AppliedState,
    string? ErrorMessage = null) {
    public bool Succeeded => Outcome is ToggleOutcome.Applied or ToggleOutcome.Unchanged;
}
