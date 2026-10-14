namespace Apparition.Backend.OpenControls;

// Selector candidates are independent of the applied main chord
public sealed class OpeningControlDraft {
    private OpeningControlDraft(OpeningControlTarget target, int slot) {
        Target = target;
        Slot = slot;
    }

    public static OpeningControlDraft Default { get; } = new(OpeningControlTarget.Main, 1);
    public int Slot { get; }
    public OpeningControlTarget Target { get; }

    public bool TryStageSlot(int slot, out OpeningControlDraft updated, out OpeningControlFailure failure) {
        updated = this;
        failure = OpeningControlFailure.InvalidSlot;
        if (slot is < 1 or > 3) return false;
        if (slot != Slot) updated = new(Target, slot);
        failure = OpeningControlFailure.None;
        return true;
    }

    public bool TryStageTarget(OpeningControlTarget target, out OpeningControlDraft updated, out OpeningControlFailure failure) {
        updated = this;
        failure = OpeningControlFailure.UnknownTarget;
        if (target is not OpeningControlTarget.Main and not OpeningControlTarget.QuickMenu) return false;
        if (target != Target) updated = new(target, Slot);
        failure = OpeningControlFailure.None;
        return true;
    }

    public bool TryApply(MainOpeningChord chord, ulong expectedRevision, OpeningAction? action,
        VerifiedOpeningActions? capabilities, out MainOpeningChord updated, out OpeningControlFailure failure) {
        ArgumentNullException.ThrowIfNull(chord);
        updated = chord;
        failure = OpeningControlFailure.UnsupportedTarget;
        // Staging the source Quick selector cannot create or change an unimplemented mode
        if (Target != OpeningControlTarget.Main) return false;
        return chord.TrySetSlot(expectedRevision, Slot, action, capabilities, out updated, out failure);
    }
}
