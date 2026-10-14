using Apparition.Backend.OpenControls;

namespace syndishanx_iw_tool.Overlay;

// Logical captions remain readable when a private saved binding cannot supply a key label
internal static class OpeningControlsMenuRows {
    private static readonly string[] Labels = {
        "Action Slot 1", "Action Slot 2", "Action Slot 3", "Action Slot 4", "Melee", "Aim", "Attack",
        "Sprint", "Use", "Frag", "Secondary Offhand", "Stance", "Jump"
    };
    private const string QuickOpeningUnavailable =
        "Quick opening is unavailable until local player life and menu authorization are verified";

    internal static string AppliedSummary(OpeningControlsView view) {
        // Actual policy readback stays current while checkbox presentation waits for its redraw
        string main = FormatChord(view.Policies?.Main.Actions ?? view.Chord.Actions);
        string summary = "Applied main chord: " + main + ". Hold all assigned actions to open";
        // Selected-row details are hidden during cooldown, so applied selectors stay in the notice
        summary += ". Editor applied: " + TargetLabel(view.AppliedSelector.Target)
            + "; slot: " + view.AppliedSelector.Slot.ToString(System.Globalization.CultureInfo.InvariantCulture);
        bool captured = !ReferenceEquals(view.Chord, view.DisplayChord);
        if (view.Policies is { } policies) {
            summary += ". Configured Quick chord: " + FormatChord(policies.Quick.Actions)
                + ". Quick Menu preference: " + (policies.QuickDisabled ? "disabled" : "enabled")
                + ". " + view.QuickOpeningGuide;
            captured |= !ReferenceEquals(policies, view.PresentedPolicies);
        }
        return captured ? summary + ". Checkboxes refresh after the current wait" : summary;
    }

    internal static IReadOnlyList<MenuRow> Build(OpeningControlsView? view) {
        bool enabled = view?.CanEdit == true;
        var applied = view?.AppliedSelector ?? OpeningControlDraft.Default;
        var candidate = view?.CandidateSelector ?? OpeningControlDraft.Default;
        string candidateTarget = TargetLabel(candidate.Target);
        string appliedTarget = TargetLabel(applied.Target);
        // The applied family, not its staged selector, chooses checkbox readback
        IReadOnlyList<OpeningAction>? displayed = applied.Target == OpeningControlTarget.Main
            ? view?.PresentedPolicies?.Main.Actions ?? view?.DisplayChord.Actions
            : view?.PresentedPolicies?.Quick.Actions;
        bool actionsEnabled = enabled && displayed is not null;
        string targetHelp = view?.CanConfigureQuick == true
            ? "Use applies this settings target. " + view.QuickOpeningGuide
            : "Use applies. Quick configuration is unavailable because its saved bindings are not verified";
        var rows = new List<MenuRow>(17) {
            new("Menu", candidateTarget, enabled, new(MenuActionKind.ChangeOpeningSelector, Slot: 0),
                "Candidate: " + candidateTarget + "; applied: " + appliedTarget + ". " + targetHelp),
            new("Bind Slot", candidate.Slot.ToString(System.Globalization.CultureInfo.InvariantCulture), enabled,
                new(MenuActionKind.ChangeOpeningSelector, Slot: 1),
                "Candidate slot: " + candidate.Slot + "; applied slot: " + applied.Slot + ". Use applies"),
            // A source blank occupies a row but is never an unavailable feature or a selection
            new("", "", false, new(MenuActionKind.None), IsSpacer: true)
        };
        if (applied.Slot > 1) {
            bool empty = displayed is null || applied.Slot > displayed.Count;
            rows.Add(new("None", actionsEnabled ? empty ? "ON" : "OFF" : "UNAVAILABLE", actionsEnabled,
                new(MenuActionKind.ChangeMainOpeningAction, Slot: -1),
                "Clear this slot and every following slot; the first slot cannot be cleared"));
        }
        foreach (var item in OpeningActionCatalog.Items) {
            bool available = actionsEnabled && view!.Capabilities.Contains(item.Action);
            bool selected = displayed is not null && applied.Slot <= displayed.Count
                && displayed[applied.Slot - 1] == item.Action;
            // Both families carry the existing logical action; the owner routes by applied target
            rows.Add(new(Labels[(int)item.Action], available ? selected ? "ON" : "OFF" : "UNAVAILABLE", available,
                new(MenuActionKind.ChangeMainOpeningAction, Slot: (int)item.Action),
                available ? "Use assigns this action to the applied slot. "
                    + (applied.Target == OpeningControlTarget.Main ? "All assigned actions must be held to open" : view!.QuickOpeningGuide)
                    : "No verified saved binding is available for this action. Save it in IW controls and restart the menu",
                OpeningAction: item.Action));
        }
        return rows.AsReadOnly();
    }

    internal static MenuRow DisableQuickPreference(OpeningControlsView? view) {
        bool available = view?.CanConfigureQuick == true;
        // The actual pair controls admission; the captured pair supplies delayed checkbox paint
        bool disabled = view?.PresentedPolicies?.QuickDisabled == true;
        return new("Disable Quick Menu", available ? disabled ? "ON" : "OFF" : "UNAVAILABLE", available,
            new(MenuActionKind.ChangePreference, Slot: (int)MenuPreference.DisableQuickMenu),
            available ? "Use toggles the configured Quick Menu preference without changing its chord. " + (view?.QuickOpeningGuide ?? QuickOpeningUnavailable)
                : "Quick configuration is unavailable. Save Aim and Secondary Offhand bindings in IW controls, then restart the menu");
    }

    private static string TargetLabel(OpeningControlTarget target) =>
        target == OpeningControlTarget.Main ? "Apparition" : "Quick Menu";

    // Only validated immutable action tuples reach this bounded source label table
    private static string FormatChord(IReadOnlyList<OpeningAction> actions) =>
        string.Join(" + ", actions.Select(action => Labels[(int)action]));
}
