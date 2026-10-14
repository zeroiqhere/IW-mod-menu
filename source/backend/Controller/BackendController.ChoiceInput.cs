using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Apparition.Backend.Controller;

public sealed partial class BackendController {
    private void HandleOrdinaryChoiceInput(CommandEnvelope command, Func<bool> foregroundAllowed) {
        if (!navigation.IsOpen || !OrdinaryMonitorVisit) return;
        // Supplied epochs describe the captured visit, even when its visible row stayed equal
        if (command.NavigationRevision is { } capturedNavigation && capturedNavigation != navigation.Revision
            || command.LocalAdmissionRevision is { } capturedAdmission && capturedAdmission != session.ReadLocalMenuAdmission()?.Revision) return;
        var selected = navigation.Handle(OverlayInput.Select);
        bool localSlider = navigation.Page == MenuPage.Preferences && selected?.Kind == MenuActionKind.ChangePreference
            && (selected.Slot is (int)MenuPreference.Design or (int)MenuPreference.ScrollAnimationTime
                || MenuChoiceDrafts.IsChoice(selected.Slot));
        if (localSlider && AllowsLocalAppearance(selected!, foregroundAllowed)) {
            // Existing local drafts remain editable during independent game operations and previews
            var wait = PrepareMonitorWait("choice", command, foregroundAllowed);
            if (command.MenuRevision is not null && wait is null) return;
            int direction = command.Input == OverlayInput.PreviousChoice ? -1 : 1;
            if (selected!.Slot == (int)MenuPreference.Design && designDraft.TryMove(direction, out var design))
                designDraft = design;
            else if (selected.Slot == (int)MenuPreference.ScrollAnimationTime && scrollAnimationDraft.TryMove(direction, out var numeric))
                scrollAnimationDraft = numeric;
            else if (MenuChoiceDrafts.IsChoice(selected.Slot) && choices.TryMove(selected.Slot, direction, out var moved))
                choices = moved;
            CommitMonitorWait(wait);
            return;
        }
        var state = retainedState ?? session.ReadMenuState();
        // Declaration shape remains separate from missing feature capability or displayed values
        if (localSlider || Busy || state.Busy || !foregroundAllowed() || navigation.SelectedIndex < 0
            || navigation.SelectedIndex >= navigation.Rows.Count || navigation.Rows[navigation.SelectedIndex].IsSpacer) return;
        string? kind = navigation.Rows[navigation.SelectedIndex].SourceChoiceRole switch {
            MenuSourceChoiceRole.NonSlider => "choiceNoop",
            MenuSourceChoiceRole.Slider or MenuSourceChoiceRole.IncrementSlider => "choice",
            _ => null
        };
        // Unknown extensions have no source branch proof; unsupported sliders still own their caller wait
        if (kind is null) return;
        var declarationWait = PrepareMonitorWait(kind, command, foregroundAllowed);
        // No timer or invented value is created for actorless, preview or unverified visits
        CommitMonitorWait(declarationWait);
    }
}
