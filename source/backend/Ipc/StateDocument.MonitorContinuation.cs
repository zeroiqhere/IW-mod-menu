namespace Apparition.Backend.Ipc;

public sealed partial record StateDocument {
    private void ValidateMonitorContinuation() {
        if (MenuMonitorContinuation is not { } receipt) return;
        // The parent proof and exact invocation remain required after its timer normally ends
        if (!receipt.IsSupported() || receipt.Sequence > LastSequence || MenuSurface is not ("main" or "quick")
            || LocalMenuEligibility is not { HasMenu: true, Life: "alive" or "dead" } proof
            || receipt.AdmissionRevision != proof.Revision
            || receipt.Kind != "back" && !IsOpen || Rows is null
            || Rows.Count == 0 && (IsOpen || SelectedIndex != 0)
            || Rows.Count > 0 && (SelectedIndex < 0 || SelectedIndex >= Rows.Count || Rows[SelectedIndex].IsSpacer)
            || MenuMonitorWait is { } wait && (wait.Revision != receipt.Revision || wait.Kind != receipt.Kind)
            || MenuUseContinuation is not null || MenuOptionCallback is not null
            || WidthEditor is not null || InstructionPositionEditor is not null || MenuPositionEditor is not null
            || MenuCredits is not null || MenuUnlock is not null || MenuPreferenceRefresh is not null
            || MenuPreferenceRebuild is not null || MenuDesignTransition is not null || MenuOpenTransition is not null) {
            throw new InvalidOperationException("Monitor continuation requires its exact admitted source invocation");
        }
    }
}
