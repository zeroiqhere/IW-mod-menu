using Apparition.Backend.OpenControls;

namespace Apparition.Backend.Ipc;

// The wire descriptor shares only the validated owner's immutable IDs and revision
public sealed class MainOpeningControlsState {
    private MainOpeningControlsState(MainOpeningChord chord) {
        Revision = chord.Revision;
        Actions = chord.Actions;
    }

    public ulong Revision { get; }
    public IReadOnlyList<OpeningAction> Actions { get; }

    public static MainOpeningControlsState FromChord(MainOpeningChord chord) {
        ArgumentNullException.ThrowIfNull(chord);
        // No physical key data or selector candidates are serialized here
        return new(chord);
    }
}
