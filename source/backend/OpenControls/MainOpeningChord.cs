namespace Apparition.Backend.OpenControls;

// A policy owner contains only ordered action IDs and its own positive revision
public sealed class MainOpeningChord {
    private readonly IReadOnlyList<OpeningAction> actions;

    private MainOpeningChord(OpeningAction[] copy, ulong revision) {
        actions = Array.AsReadOnly(copy);
        Revision = revision;
    }

    public IReadOnlyList<OpeningAction> Actions => actions;
    public ulong Revision { get; }

    public static bool TryCreateDefault(VerifiedOpeningActions? capabilities,
        out MainOpeningChord? chord, out OpeningControlFailure failure) {
        // A missing verified default action must not be replaced with a guessed binding
        return TryCreate(new[] { OpeningAction.Aim, OpeningAction.Melee }, 1, capabilities, out chord, out failure);
    }

    public static bool TryCreate(IReadOnlyList<OpeningAction>? input, ulong revision,
        VerifiedOpeningActions? capabilities, out MainOpeningChord? chord, out OpeningControlFailure failure) {
        chord = null;
        failure = OpeningControlFailure.InvalidRevision;
        if (revision == 0) return false;
        failure = OpeningControlFailure.InvalidChord;
        if (input is null) return false;
        int count = input.Count;
        if (count is < 1 or > 3) return false;

        var copy = new OpeningAction[count];
        for (int index = 0; index < count; index++) copy[index] = input[index];
        if (!OpeningChordValidation.TryValidate(copy, capabilities, out failure)) return false;
        // Publish the immutable owner only after the complete tuple has passed every rule
        chord = new(copy, revision);
        return true;
    }

    public bool TrySetSlot(ulong expectedRevision, int slot, OpeningAction? action,
        VerifiedOpeningActions? capabilities, out MainOpeningChord updated, out OpeningControlFailure failure) {
        return MainOpeningChordMutation.TryApply(this, expectedRevision, slot, action, capabilities, out updated, out failure);
    }
}
