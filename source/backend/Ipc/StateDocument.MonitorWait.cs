namespace Apparition.Backend.Ipc;

public sealed partial record StateDocument {
    private void ValidateMonitorWait() {
        if (MenuMonitorWait is not { } wait) return;
        // A source visit needs current actor proof, but a root Back may have closed its body
        if (!wait.IsSupported() || MenuSurface is not ("main" or "quick")
            || LocalMenuEligibility is not { HasMenu: true, Life: "alive" or "dead" }
            || wait.Kind is ("choice" or "choiceNoop") && (!IsOpen || Rows is null || Rows.Count == 0
                || SelectedIndex < 0 || SelectedIndex >= Rows.Count || Rows[SelectedIndex].IsSpacer)
            || WidthEditor is not null || InstructionPositionEditor is not null || MenuPositionEditor is not null
            || MenuCredits is not null || MenuUnlock is not null || MenuOptionCallback is not null
            || MenuPreferenceRefresh is not null || MenuPreferenceRebuild is not null
            || MenuDesignTransition is not null || MenuOpenTransition is not null) {
            throw new InvalidOperationException("Monitor wait requires an exclusive admitted ordinary source visit");
        }
    }
}
