using syndishanx_iw_tool.Overlay;

namespace Apparition.Backend.Controller;

public sealed partial class BackendController {
    private bool TryHandleRejectedPreference(MenuAction action, UseIntent? intent) {
        // Rejection belongs to these source BOOL handlers, not to unavailable IW features
        if (navigation.Page != MenuPage.Preferences || action.Kind != MenuActionKind.ChangePreference) return false;
        string? reason = action.Slot switch {
            // Stealth has priority when more than one source restriction applies
            (int)MenuPreference.OptionCounter when appearance.StealthUI =>
                "You Can't Use The Option Counter While Stealth UI Is Enabled",
            (int)MenuPreference.OptionCounter when !MenuDesignDraft.AllowsOptionCounter(appearance.Design) =>
                "You Can't Use The Option Counter With This Design",
            (int)MenuPreference.ColoredCursor when !MenuDesignDraft.AllowsColoredCursor(appearance.Design) =>
                "You Can't Use Colored Cursor With This Design",
            _ => null
        };
        if (reason is null) return false;

        // Reserve the redraw and callback clock before changing feedback or receipt state
        var callback = PrepareLocalOptionCallback("boolean");
        var legacyRefresh = callback is null ? LocalMenuPreferenceRefreshState.Start(appearance, clock) : null;
        if (legacyRefresh is not null) _ = checked(navigation.MenuRevision + 1);
        appearanceError = reason;
        CommitOptionCallback(callback, intent);
        if (legacyRefresh is not null) {
            // Legacy callers keep their finite wait without borrowing a source owner's receipt
            preferenceRefresh = legacyRefresh;
            preferenceRefreshPhase = "pending";
            CancelUseContinuation();
        }
        return true;
    }
}
