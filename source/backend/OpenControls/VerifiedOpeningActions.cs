namespace Apparition.Backend.OpenControls;

// This set records only actions proved by a caller's private saved-binding reader
public sealed class VerifiedOpeningActions {
    private readonly ushort bits;
    private readonly IReadOnlyList<OpeningAction> actions;

    private VerifiedOpeningActions(OpeningAction[] copy, ushort verifiedBits) {
        actions = Array.AsReadOnly(copy);
        bits = verifiedBits;
    }

    public IReadOnlyList<OpeningAction> Actions => actions;

    public bool Contains(OpeningAction action) {
        // Reject the full ID before a shift can mask an unknown value into a known bit
        return OpeningActionCatalog.TryFind(action, out _) && (bits & (1 << (int)action)) != 0;
    }

    public static bool TryCreate(IReadOnlyList<OpeningAction>? input,
        out VerifiedOpeningActions? verified, out OpeningControlFailure failure) {
        verified = null;
        failure = OpeningControlFailure.InvalidCapabilities;
        if (input is null) return false;
        int count = input.Count;
        if (count < 0 || count > OpeningActionCatalog.Items.Count) return false;

        // Copy a bounded number of indexed values rather than trusting an unbounded enumerator
        var copy = new OpeningAction[count];
        ushort verifiedBits = 0;
        for (int index = 0; index < count; index++) {
            var action = input[index];
            if (!OpeningActionCatalog.TryFind(action, out _)) {
                failure = OpeningControlFailure.UnknownAction;
                return false;
            }
            ushort bit = (ushort)(1 << (int)action);
            if ((verifiedBits & bit) != 0) {
                failure = OpeningControlFailure.DuplicateAction;
                return false;
            }
            verifiedBits |= bit;
            copy[index] = action;
        }
        // An empty set is meaningful: no action was proved, so every action remains unavailable
        verified = new(copy, verifiedBits);
        failure = OpeningControlFailure.None;
        return true;
    }
}
