using Apparition.Backend.Preferences;
using Apparition.Backend.Session;

namespace Apparition.Backend.Controller;

// Deferred saves retain their original account and value until the source HUD boundary commits
public sealed partial class BackendController {
    private enum PreferenceSaveBoundary { Rebuild, SoftUnlock }
    private sealed record PendingPreferenceSave(LocalMenuPreferenceScope Scope,
        CommittedMenuPreferences Preferences, PreferenceSaveBoundary Boundary);
    private PendingPreferenceSave? pendingPreferenceSave;
    private bool preferenceSaveReady;
    private bool deferEditorPreferenceExit;

    private void DeferPreferenceSave(PreferenceSaveBoundary boundary) {
        var scope = preferenceScope;
        if (scope is null || !CanSavePreferences(scope)) return;
        pendingPreferenceSave = new(scope, CaptureCommittedPreferences(), boundary);
        preferenceSaveReady = false;
    }

    private void CommitDeferredPreferenceSave() {
        var pending = pendingPreferenceSave;
        if (pending is null) return;
        if (!CanSavePreferences(pending.Scope)) { pendingPreferenceSave = null;preferenceSaveReady = false;return; }
        bool ready = pending.Boundary == PreferenceSaveBoundary.Rebuild ? preferenceSaveReady
            : menuUnlock.StartedTimestamp is null;
        if (!ready) return;
        pendingPreferenceSave = null;preferenceSaveReady = false;
        WriteCommittedPreferences(pending.Scope, pending.Preferences);
    }
}
