namespace Apparition.Backend.Ipc;

// Presentation focus changes do not mutate the owner's retained navigation
internal static class StateFocusProjection {
    public static StateDocument Apply(StateDocument state, bool foregroundAllowed) {
        ArgumentNullException.ThrowIfNull(state);
        // Closed display frames carry no active editor or menu-rebuild metadata
        return foregroundAllowed ? state : state with { IsOpen = false, WidthEditor = null, MenuUnlock = null, InstructionPositionEditor = null, MenuPositionEditor = null, MenuCredits = null, MenuPreferenceRefresh = null, MenuDesignTransition = null, MenuOpening = null, MenuOpenTransition = null, MenuPreferenceRebuild = null, MenuOptionCallback = null, MenuBooleanPaint = null, MenuMonitorWait = null, MenuMonitorContinuation = null, MenuUseContinuation = null, InstructionTheme = null, ChromeTheme = null, InstructionHud = state.InstructionHud is null ? null : syndishanx_iw_tool.Overlay.InstructionHudState.Absent };
    }
}
