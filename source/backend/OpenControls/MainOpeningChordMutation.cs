namespace Apparition.Backend.OpenControls;

// Slot edits prepare a complete candidate before replacing the policy owner
internal static class MainOpeningChordMutation {
    internal static bool TryApply(MainOpeningChord current, ulong expectedRevision, int slot,
        OpeningAction? action, VerifiedOpeningActions? capabilities,
        out MainOpeningChord updated, out OpeningControlFailure failure) {
        updated = current;
        failure = OpeningControlFailure.StaleRevision;
        if (expectedRevision != current.Revision) return false;
        failure = OpeningControlFailure.InvalidSlot;
        if (slot is < 1 or > 3) return false;
        failure = OpeningControlFailure.InvalidCapabilities;
        if (capabilities is null) return false;
        failure = OpeningControlFailure.FirstSlotRequired;
        if (slot == 1 && action is null) return false;

        if (action is { } chosen) {
            failure = OpeningControlFailure.UnknownAction;
            if (!OpeningActionCatalog.TryFind(chosen, out _)) return false;
            failure = OpeningControlFailure.DuplicateAction;
            // Reselecting the same action in its own slot also rejects in the source
            if (current.Actions.Contains(chosen)) return false;
        }
        failure = OpeningControlFailure.MissingPreviousSlot;
        if (slot > current.Actions.Count + 1) return false;

        int count = action is null ? slot - 1 : Math.Max(slot, current.Actions.Count);
        var candidate = new OpeningAction[count];
        for (int index = 0; index < Math.Min(count, current.Actions.Count); index++) {
            candidate[index] = current.Actions[index];
        }
        if (action is { } selected) candidate[slot - 1] = selected;
        if (!OpeningChordValidation.TryValidate(candidate, capabilities, out failure)) return false;

        // None at the first unused slot is accepted without changing the input policy
        if (action is null && count == current.Actions.Count) return true;
        failure = OpeningControlFailure.RevisionExhausted;
        if (current.Revision == ulong.MaxValue) return false;
        ulong nextRevision = checked(current.Revision + 1);
        if (!MainOpeningChord.TryCreate(candidate, nextRevision, capabilities, out var prepared, out failure)) return false;
        // Rejections above retain both the old tuple and the old revision
        updated = prepared!;
        return true;
    }
}
