namespace Apparition.Backend.OpenControls;

// Rejections are separate from the immutable policy returned to the caller
public enum OpeningControlFailure {
    None,
    InvalidCapabilities,
    InvalidChord,
    InvalidRevision,
    UnknownAction,
    UnavailableAction,
    DuplicateAction,
    ConflictingOffhands,
    InvalidSlot,
    FirstSlotRequired,
    MissingPreviousSlot,
    StaleRevision,
    RevisionExhausted,
    UnknownTarget,
    UnsupportedTarget
}
