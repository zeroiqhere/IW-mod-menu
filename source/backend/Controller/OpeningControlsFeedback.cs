using Apparition.Backend.OpenControls;

namespace Apparition.Backend.Controller;

// Rejected local edits explain the constraint without claiming that a key was changed
internal static class OpeningControlsFeedback {
    internal static string Describe(OpeningControlFailure failure, bool quick = false) => failure switch {
        OpeningControlFailure.DuplicateAction => "This action is already assigned to the " + (quick ? "Quick" : "main") + " opening chord",
        OpeningControlFailure.MissingPreviousSlot => "Fill the previous bind slot before assigning this slot",
        OpeningControlFailure.ConflictingOffhands => "Frag and Secondary Offhand cannot be paired in the opening chord",
        OpeningControlFailure.FirstSlotRequired => "The first bind slot cannot be None",
        OpeningControlFailure.RevisionExhausted => "The opening control revision is exhausted. Restart the menu",
        OpeningControlFailure.UnavailableAction => "No verified saved binding is available for this action",
        _ => "The opening control change was rejected: " + failure
    };
}
