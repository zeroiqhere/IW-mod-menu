namespace Apparition.Backend.QuickMenu;

// Opening alpha is independent from the source monitor's input wait
public sealed record QuickOpeningFrame(bool HudVisible, bool InputsLocked, int? OpeningElapsedMilliseconds, int OpeningDurationMilliseconds);
public sealed record QuickOpeningObservation(QuickOpeningTimeline State, QuickOpeningFrame Frame);
