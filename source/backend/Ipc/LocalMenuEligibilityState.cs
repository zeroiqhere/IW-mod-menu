namespace Apparition.Backend.Ipc;

// This descriptor contains copied menu facts and an opaque session admission epoch
public sealed record LocalMenuEligibilityState(ulong Revision, string Life,
    bool? HasMenu, bool? ControlsDisabled, bool Blocked) {
    // A supported shape cannot authenticate an actor or authorize a fresh opening
    public bool IsSupported() => Revision > 0 && Life is "unknown" or "alive" or "dead";
}
