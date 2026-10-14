namespace Apparition.Backend.OpenControls;

// Creation and mutation share the same bounded action rules
internal static class OpeningChordValidation {
    internal static bool TryValidate(ReadOnlySpan<OpeningAction> actions,
        VerifiedOpeningActions? capabilities, out OpeningControlFailure failure) {
        failure = OpeningControlFailure.InvalidCapabilities;
        if (capabilities is null) return false;
        failure = OpeningControlFailure.InvalidChord;
        if (actions.Length is < 1 or > 3) return false;

        ushort seen = 0;
        foreach (var action in actions) {
            if (!OpeningActionCatalog.TryFind(action, out _)) {
                failure = OpeningControlFailure.UnknownAction;
                return false;
            }
            ushort bit = (ushort)(1 << (int)action);
            if ((seen & bit) != 0) {
                failure = OpeningControlFailure.DuplicateAction;
                return false;
            }
            if (!capabilities.Contains(action)) {
                failure = OpeningControlFailure.UnavailableAction;
                return false;
            }
            seen |= bit;
        }
        // The source permits replacing an offhand in its own slot, but never pairing both
        ushort offhands = (1 << (int)OpeningAction.Frag) | (1 << (int)OpeningAction.SecondaryOffhand);
        if ((seen & offhands) == offhands) {
            failure = OpeningControlFailure.ConflictingOffhands;
            return false;
        }
        failure = OpeningControlFailure.None;
        return true;
    }
}
