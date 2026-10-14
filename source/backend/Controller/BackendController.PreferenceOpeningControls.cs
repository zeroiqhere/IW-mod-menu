using Apparition.Backend.OpenControls;

namespace Apparition.Backend.Controller;

// Saved action IDs are preferences; every active chord is rebuilt with current verified bindings
public sealed partial class BackendController {
    private ulong preferenceOpeningWatermark;
    private bool preferenceOpeningExhausted;
    private void RestorePreferenceOpeningControls(bool loaded) {
        ulong? previous = OpeningAdmissionRevision;
        if (previous is { } observed && observed > preferenceOpeningWatermark) preferenceOpeningWatermark = observed;
        mainOpeningChord = null;openingPolicies = null;
        appliedOpeningSelector = candidateOpeningSelector = OpeningControlDraft.Default;
        openingControlsError = null;
        if (openingActions is null) return;
        if (preferenceOpeningExhausted || preferenceOpeningWatermark == ulong.MaxValue) {
            preferenceOpeningExhausted = true;
            preferenceOpeningError = "Opening controls could not be restored; their revision is exhausted";
            return;
        }
        ulong revision = ++preferenceOpeningWatermark;
        bool mainAccepted = MainOpeningChord.TryCreate(configuredMainActions, revision, openingActions, out var main, out _);
        bool quickAccepted = QuickOpeningChord.TryCreate(configuredQuickActions, revision, openingActions, out var quick, out _);
        if (!mainAccepted && MainOpeningChord.TryCreateDefault(openingActions, out var fallbackMain, out _))
            MainOpeningChord.TryCreate(fallbackMain!.Actions, revision, openingActions, out main, out _);
        if (!quickAccepted && QuickOpeningChord.TryCreateDefault(openingActions, out var fallbackQuick, out _))
            QuickOpeningChord.TryCreate(fallbackQuick!.Actions, revision, openingActions, out quick, out _);
        if (main is not null && quick is not null)
            QuickOpeningPolicies.TryCreate(main, quick, revision, configuredQuickDisabled, out openingPolicies, out _);
        else mainOpeningChord = main;
        if (loaded && (!mainAccepted || !quickAccepted))
            preferenceOpeningError = "Saved opening controls are unavailable with the current verified inputs; the verified default is used where available";
    }

    private void SaveAppliedOpeningPreferences(bool quick = false, bool disabled = false) {
        // An unrelated appearance commit keeps configured tuples that cannot currently be activated
        if (!quick && !disabled) configuredMainActions = CurrentMainOpeningChord?.Actions;
        if (quick && openingPolicies is not null)
            configuredQuickActions = openingPolicies.Quick.Actions;
        if (disabled && openingPolicies is not null)
            configuredQuickDisabled = openingPolicies.QuickDisabled;
        SaveCommittedPreferences();
    }
}
