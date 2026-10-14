namespace Apparition.Backend.Ipc;

public sealed partial record StateDocument {
    private void ValidateUseContinuation() {
        ValidateDesignUseContinuation();
        if (MenuUseContinuation is not { } receipt) return;
        // The wire receipt carries no private target and cannot supply missing source authority
        if (!receipt.IsSupported() || !IsOpen || MenuSurface is not ("main" or "quick")
            || receipt.Sequence > LastSequence || LocalMenuEligibility is not { HasMenu: true, Life: "alive" or "dead" } proof
            || receipt.AdmissionRevision != proof.Revision || Rows is null || Rows.Count == 0
            || SelectedIndex < 0 || SelectedIndex >= Rows.Count || Rows[SelectedIndex].IsSpacer
            || MenuOptionCallback is { } callback && (callback.Revision != receipt.Revision || callback.Kind != receipt.Kind)
            || MenuMonitorWait is not null || WidthEditor is not null || InstructionPositionEditor is not null
            || MenuPositionEditor is not null || MenuCredits is not null || MenuUnlock is not null
            || MenuPreferenceRefresh is not null || MenuPreferenceRebuild is not null
            || MenuOpenTransition is not null) {
            throw new InvalidOperationException("Use continuation requires its exact admitted ordinary source invocation");
        }
    }

    private void ValidateDesignUseContinuation() {
        if (MenuDesignTransition is null || MenuOptionCallback is null && MenuUseContinuation is null) return;
        // The design wait can share input only with its exact plain caller pair
        if (MenuOptionCallback is not { Kind: "plain", Phase: "cooldown" } callback
            || MenuUseContinuation is not { Kind: "plain" } receipt || callback.Revision != receipt.Revision) {
            throw new InvalidOperationException("Design transition Use requires its exact plain cooldown receipt");
        }
    }
}
