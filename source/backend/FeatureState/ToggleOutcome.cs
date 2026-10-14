namespace syndishanx_iw_tool.FeatureState;

// Outcomes describe the backend response without claiming a target is safe
public enum ToggleOutcome {
    Applied,
    Unchanged,
    Failed,
    Cancelled,
    Closed
}
