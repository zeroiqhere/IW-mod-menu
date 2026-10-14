namespace Apparition.Backend.OpenControls;

// Quick keeps its own policy type while sharing the established bounded tuple rules
public sealed class QuickOpeningChord {
    // This private adapter reuses validation and mutation without changing the applied Main owner
    private readonly MainOpeningChord validatedTuple;

    private QuickOpeningChord(MainOpeningChord validatedTuple) {
        this.validatedTuple = validatedTuple;
    }

    public IReadOnlyList<OpeningAction> Actions => validatedTuple.Actions;
    public ulong Revision => validatedTuple.Revision;

    public static bool TryCreateDefault(VerifiedOpeningActions? capabilities,
        out QuickOpeningChord? chord, out OpeningControlFailure failure) {
        // Both default actions must be proved by the supplied capability owner
        return TryCreate(new[] { OpeningAction.Aim, OpeningAction.SecondaryOffhand }, 1,
            capabilities, out chord, out failure);
    }

    public static bool TryCreate(IReadOnlyList<OpeningAction>? actions, ulong revision,
        VerifiedOpeningActions? capabilities, out QuickOpeningChord? chord,
        out OpeningControlFailure failure) {
        chord = null;
        if (!MainOpeningChord.TryCreate(actions, revision, capabilities, out var prepared, out failure)) return false;
        // The shared validator owns a bounded copy, so caller changes cannot alter Quick
        chord = new QuickOpeningChord(prepared!);
        return true;
    }

    public bool TrySetSlot(ulong expectedRevision, int slot, OpeningAction? action,
        VerifiedOpeningActions? capabilities, out QuickOpeningChord updated,
        out OpeningControlFailure failure) {
        updated = this;
        if (!validatedTuple.TrySetSlot(expectedRevision, slot, action, capabilities,
            out var prepared, out failure)) return false;
        // Accepted None no-ops keep the same public owner and the same revision
        if (!ReferenceEquals(prepared, validatedTuple)) updated = new QuickOpeningChord(prepared);
        return true;
    }
}
