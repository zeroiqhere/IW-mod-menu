using Apparition.Backend.Preferences;
using Apparition.Backend.Session;
using Apparition.Backend.OpenControls;
using syndishanx_iw_tool.Overlay;

namespace Apparition.Backend.Controller;

// Account-keyed choices stay in memory for this backend process, without writing account data to disk
public sealed partial class BackendController {
    private static readonly ICommittedMenuPreferencesStore processPreferenceStore = new MemoryCommittedMenuPreferencesStore();
    private readonly ICommittedMenuPreferencesStore preferenceStore;
    private LocalMenuPreferenceScope? preferenceScope;
    private bool preferenceSavingClosed;
    private string? preferenceStorageError;
    private string? preferenceOpeningError;
    private IReadOnlyList<OpeningAction>? configuredMainActions = Array.AsReadOnly(new[] { OpeningAction.Aim, OpeningAction.Melee });
    private IReadOnlyList<OpeningAction>? configuredQuickActions = Array.AsReadOnly(new[] { OpeningAction.Aim, OpeningAction.SecondaryOffhand });
    private bool configuredQuickDisabled;

    private void SynchronizeCommittedPreferences() {
        if (preferenceSavingClosed || finished) return;
        var current = session.ReadLocalMenuPreferenceScope();
        if (ReferenceEquals(current, preferenceScope)) return;
        // An unknown owner cannot carry an editor candidate or delayed save into another account
        pendingPreferenceSave = null;
        preferenceSaveReady = false;
        if (preferenceScope is not null) {
            CancelPreferenceRefresh();
            widthEditor = null;menuPositionEditor = null;instructionPositionEditor = null;
            menuCredits = null;creditsFrame = null;
            menuUnlock = LocalMenuUnlockState.None;
            navigation.Close();
        }
        preferenceScope = current;
        preferenceStorageError = preferenceOpeningError = null;
        appearance = MenuAppearance.Default;
        accent = MenuThemes.DefaultAccent;
        smoothRainbowTheme = false;
        configuredMainActions = Array.AsReadOnly(new[] { OpeningAction.Aim, OpeningAction.Melee });
        configuredQuickActions = Array.AsReadOnly(new[] { OpeningAction.Aim, OpeningAction.SecondaryOffhand });
        configuredQuickDisabled = false;
        CommittedMenuPreferences? saved = null;
        bool found = false;
        try {
            if (current is not null) found = preferenceStore.TryRead(current, out saved);
        } catch (Exception) {
            // A failed read must never overwrite the older stored value with defaults
            preferenceStorageError = "Saved preferences could not be loaded. Defaults are active";
        }
        if (found && saved is not null) {
            appearance = saved.Appearance;
            accent = saved.Accent;
            smoothRainbowTheme = saved.SmoothRainbowTheme;
            configuredMainActions = saved.MainOpeningActions;
            configuredQuickActions = saved.QuickOpeningActions;
            configuredQuickDisabled = saved.QuickDisabled;
        }
        // Reload creates fresh drafts and capabilities, never stored HUD or action epochs
        choices = MenuChoiceDrafts.FromAppearance(appearance);
        scrollAnimationDraft = ScrollAnimationDraft.FromAppearance(appearance);
        designDraft = MenuDesignDraft.FromAppearance(appearance);
        RestorePreferenceOpeningControls(found);
        if (current is not null && !found && preferenceStorageError is null) SaveCommittedPreferences();
    }

    private CommittedMenuPreferences CaptureCommittedPreferences() => new(appearance, accent,
        smoothRainbowTheme, configuredMainActions, configuredQuickActions, configuredQuickDisabled);

    private bool CanSavePreferences(LocalMenuPreferenceScope scope) => !finished && !preferenceSavingClosed && !ExitRequested
        && ReferenceEquals(preferenceScope, scope) && ReferenceEquals(session.ReadLocalMenuPreferenceScope(), scope);

    private void SaveCommittedPreferences() {
        var scope = preferenceScope;
        if (scope is null || !CanSavePreferences(scope)) return;
        WriteCommittedPreferences(scope, CaptureCommittedPreferences());
    }

    private void WriteCommittedPreferences(LocalMenuPreferenceScope scope, CommittedMenuPreferences preferences) {
        if (!CanSavePreferences(scope)) return;
        try {
            preferenceStore.Write(scope, preferences);
            preferenceStorageError = null;
        } catch (Exception) {
            // Live controls stay applied while the frame reports the failed saved state
            preferenceStorageError = "Preferences could not be saved. The current settings remain active";
        }
    }
}
