using Apparition.Backend.Ipc;
using Apparition.Backend.OpenControls;
using syndishanx_iw_tool.Overlay;

namespace Apparition.Backend.Controller;

// Opening choices configure local immutable policy and never reach the game session
public sealed partial class BackendController {
    private VerifiedOpeningActions? openingActions;
    private MainOpeningChord? mainOpeningChord;
    private QuickOpeningPolicies? openingPolicies;
    private OpeningControlDraft appliedOpeningSelector = OpeningControlDraft.Default;
    private OpeningControlDraft candidateOpeningSelector = OpeningControlDraft.Default;
    private string? openingControlsError;

    // Exactly one applied owner is active: the verified pair or the Main-only fallback
    private MainOpeningChord? CurrentMainOpeningChord => openingPolicies?.Main ?? mainOpeningChord;
    private ulong? OpeningAdmissionRevision => openingPolicies?.Revision ?? mainOpeningChord?.Revision;

    private void ConfigureOpeningControls(VerifiedOpeningActions? verified) {
        openingActions = verified;
        if (verified is null) return;
        if (QuickOpeningPolicies.TryCreateDefault(verified, out var pair, out _)) {
            openingPolicies = pair;
        } else if (MainOpeningChord.TryCreateDefault(verified, out var chord, out _)) {
            // Missing Quick evidence must not remove an already verified Main default
            mainOpeningChord = chord;
        }
    }

    private OpeningControlsView? ReadOpeningControlsView() {
        var actual = CurrentMainOpeningChord;
        if (actual is null || openingActions is null) return null;
        // The callback owns old paint only until its successful source redraw
        bool retainCallbackRows = optionCallback is { PresentationApplied: false };
        var displayedPolicies = retainCallbackRows && optionCallbackOpeningPolicies is { } previousPolicies
            ? previousPolicies : preferenceRefresh?.RowsOpeningPolicies(openingPolicies) ?? openingPolicies;
        var displayed = displayedPolicies?.Main
            ?? (retainCallbackRows ? optionCallbackMainOpeningChord : null)
            ?? preferenceRefresh?.RowsOpeningChord(actual) ?? actual;
        var selector = preferenceRefresh?.RowsOpeningSelector(appliedOpeningSelector) ?? appliedOpeningSelector;
        return new(actual, displayed, selector, candidateOpeningSelector,
            openingActions, choiceControlsAvailable, openingPolicies, displayedPolicies, HasVerifiedOpeningOwner);
    }

    private bool AllowsCapturedOpeningScope(CommandEnvelope command) {
        // Legacy commands omit this field; supplied captures must match on every page
        if (command.OpeningControlsRevision is not { } captured) return true;
        return OpeningAdmissionRevision is { } revision && captured == revision
            && command.NavigationRevision == navigation.Revision
            && command.Input is OverlayInput.Toggle or OverlayInput.Select or OverlayInput.PreviousChoice or OverlayInput.NextChoice;
    }

    private bool HandleOpeningControlsCommand(CommandEnvelope command, Func<bool> foregroundAllowed) {
        if (navigation.Page != MenuPage.OpenControls
            || command.Input is not (OverlayInput.Select or OverlayInput.PreviousChoice or OverlayInput.NextChoice)) return false;
        // Both epochs come from the worker's observed frame, never from the current owner
        if (!navigation.IsOpen || CurrentMainOpeningChord is null || openingActions is null
            || command.NavigationRevision != navigation.Revision
            || command.OpeningControlsRevision != OpeningAdmissionRevision) return true;
        // Capture the written Select before a selector can replace its navigation or row scope
        var useIntent = command.Input == OverlayInput.Select
            ? CaptureUseIntent(command, foregroundAllowed, retainedState ?? session.ReadMenuState()) : null;
        var action = navigation.Handle(OverlayInput.Select);
        if (action is null || !AllowsLocalAppearance(action, foregroundAllowed)) return true;
        if (command.Input is OverlayInput.PreviousChoice or OverlayInput.NextChoice) {
            if (action.Kind != MenuActionKind.ChangeOpeningSelector) return true;
            var wait = PrepareMonitorWait("choice");
            int direction = command.Input == OverlayInput.PreviousChoice ? -1 : 1;
            if (action.Slot == 0) {
                var target = candidateOpeningSelector.Target == OpeningControlTarget.Main
                    ? OpeningControlTarget.QuickMenu : OpeningControlTarget.Main;
                candidateOpeningSelector.TryStageTarget(target, out var staged, out _);
                candidateOpeningSelector = staged;
            } else if (action.Slot == 1) {
                int slot = (candidateOpeningSelector.Slot - 1 + direction + 3) % 3 + 1;
                candidateOpeningSelector.TryStageSlot(slot, out var staged, out _);
                candidateOpeningSelector = staged;
            }
            CommitMonitorWait(wait);
            return true;
        }
        if (action.Kind == MenuActionKind.ChangeOpeningSelector) {
            ApplyOpeningSelector(action.Slot, useIntent);
        } else if (action.Kind == MenuActionKind.ChangeMainOpeningAction) {
            ApplyOpeningAction(command.OpeningControlsRevision!.Value, action.Slot, useIntent);
        }
        return true;
    }

    private void ApplyOpeningSelector(int selector, UseIntent? useIntent) {
        var main = CurrentMainOpeningChord;
        if (main is null) return;
        var next = appliedOpeningSelector;
        string? feedback = null;
        bool redraw = selector == 1;
        if (selector == 0) {
            if (candidateOpeningSelector.Target == OpeningControlTarget.QuickMenu && openingPolicies is null) {
                feedback = "Quick controls are unavailable without a verified Secondary Offhand binding. Applied controls remain Apparition";
            } else {
                appliedOpeningSelector.TryStageTarget(candidateOpeningSelector.Target, out next, out _);
                redraw = next.Target != appliedOpeningSelector.Target;
            }
        } else if (selector == 1) {
            appliedOpeningSelector.TryStageSlot(candidateOpeningSelector.Slot, out next, out _);
        } else return;

        bool changed = next.Target != appliedOpeningSelector.Target || next.Slot != appliedOpeningSelector.Slot;
        var cause = (next.Target == OpeningControlTarget.QuickMenu, redraw) switch {
            (true, true) => MenuPreferenceRefreshCause.QuickOpeningSelector,
            (true, false) => MenuPreferenceRefreshCause.QuickOpeningSelectorNoRedraw,
            (false, true) => MenuPreferenceRefreshCause.MainOpeningSelector,
            (false, false) => MenuPreferenceRefreshCause.MainOpeningSelectorNoRedraw
        };
        // One clock owns a typed source callback; actorless preview keeps its legacy refresh
        var callback = PrepareLocalOptionCallback("plain");
        var started = callback is null ? LocalMenuPreferenceRefreshState.StartOpeningControls(appearance, main,
            appliedOpeningSelector, cause, clock, openingPolicies) : null;
        // A changed editor target or slot invalidates captured commands independently from tuple epochs
        if (changed) _ = checked(navigation.Revision + 1);
        if (redraw) {
            // SetRows may already own the one structural context change when None appears
            _ = checked(navigation.MenuRevision + 1);
            var view = ReadOpeningControlsView()! with { AppliedSelector = next };
            var fresh = OpeningControlsMenuRows.Build(view);
            ulong previousContext = navigation.MenuRevision;
            navigation.SetRows(fresh);
            if (navigation.MenuRevision == previousContext) navigation.InvalidatePresentation();
            CancelOpening();
        }
        if (changed) navigation.InvalidateActionContext();
        // A failed clock or counter above cannot partially replace the selector
        appliedOpeningSelector = next;
        openingControlsError = feedback;
        if (callback is not null) CommitOptionCallback(callback, useIntent);
        else {
            preferenceRefresh = started;
            preferenceRefreshPhase = started!.IsNoRedrawSelector ? "selector" : "cooldown";
        }
    }

    private void ApplyOpeningAction(ulong expectedRevision, int actionId, UseIntent? useIntent) {
        var main = CurrentMainOpeningChord;
        if (main is null || openingActions is null) return;
        OpeningAction? selected = actionId == -1 ? null : (OpeningAction)actionId;
        var preparedMain = main;
        var preparedPolicies = openingPolicies;
        bool quick = appliedOpeningSelector.Target == OpeningControlTarget.QuickMenu;
        bool accepted;
        OpeningControlFailure failure;
        if (openingPolicies is { } pair) {
            accepted = quick
                ? pair.TrySetQuickSlot(expectedRevision, appliedOpeningSelector.Slot, selected, openingActions,
                    out preparedPolicies, out failure)
                : pair.TrySetMainSlot(expectedRevision, appliedOpeningSelector.Slot, selected, openingActions,
                    out preparedPolicies, out failure);
        } else {
            accepted = appliedOpeningSelector.TryApply(main, expectedRevision, selected,
                openingActions, out preparedMain, out failure);
        }
        var cause = quick ? MenuPreferenceRefreshCause.QuickOpeningControls : MenuPreferenceRefreshCause.MainOpeningControls;
        var previousPolicies = openingPolicies;
        var callback = PrepareLocalOptionCallback("boolean");
        var started = callback is null ? LocalMenuPreferenceRefreshState.StartOpeningControls(appearance, main,
            appliedOpeningSelector, cause, clock, openingPolicies) : null;
        // Typed callbacks already reserved this redraw in their preparation
        if (callback is null) _ = checked(navigation.MenuRevision + 1);
        // Even a rejected bool callback keeps its source refresh; only success can change policy
        if (accepted) {
            if (openingPolicies is not null) openingPolicies = preparedPolicies;
            else mainOpeningChord = preparedMain;
            SaveAppliedOpeningPreferences(quick: quick);
        }
        openingControlsError = accepted ? null : OpeningControlsFeedback.Describe(failure, quick);
        if (callback is not null) {
            CommitOptionCallback(callback, useIntent, previousOpeningPolicies: previousPolicies,
                previousMainOpeningChord: main);
        } else {
            preferenceRefresh = started;
            preferenceRefreshPhase = "pending";
        }
    }

    private void ApplyQuickMenuDisabled(ulong expectedRevision, UseIntent? useIntent) {
        if (openingPolicies is not { } pair) return;
        bool accepted = pair.TrySetQuickDisabled(expectedRevision, !pair.QuickDisabled, out var prepared, out var failure);
        var callback = PrepareLocalOptionCallback("boolean");
        var started = callback is null ? LocalMenuPreferenceRefreshState.StartOpeningControls(appearance, pair.Main,
            appliedOpeningSelector, MenuPreferenceRefreshCause.QuickMenuDisabled, clock, pair) : null;
        // The legacy refresh reserves the epoch already covered by typed callback preparation
        if (callback is null) _ = checked(navigation.MenuRevision + 1);
        if (accepted) {
            openingPolicies = prepared;
            SaveAppliedOpeningPreferences(disabled: true);
        }
        openingControlsError = accepted ? null : OpeningControlsFeedback.Describe(failure, true);
        if (callback is not null) CommitOptionCallback(callback, useIntent, previousOpeningPolicies: pair);
        else {
            preferenceRefresh = started;
            preferenceRefreshPhase = "pending";
        }
    }
}
