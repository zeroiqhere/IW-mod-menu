namespace syndishanx_iw_tool.Players;

// A capture describes one complete observed cohort, not a permanent lobby generation
public sealed partial class PlayerCohortCapture {
    public PlayerCohortSession Session { get; }
    public IReadOnlyList<PlayerSlotObservation> Slots { get; }
    public IReadOnlyList<PlayerIdentity> Identities { get; }

    private PlayerCohortCapture(PlayerCohortObservation observation) {
        Session = observation.Session!;
        Slots = Array.AsReadOnly(observation.Slots.OrderBy(slot => slot.Slot).ToArray());
        Identities = Array.AsReadOnly(Slots.Where(slot => slot.Presence == PlayerSlotPresence.Present)
            .Select(slot => slot.Player!.Identity).ToArray());
    }

    public static PlayerCohortCapture? TryCreate(PlayerCohortObservation? observation) =>
        Valid(observation) ? new(observation!) : null;

    public bool Matches(PlayerCohortObservation? observation, bool requireEnableEligible = false) {
        PlayerCohortCapture? current = TryCreate(observation);
        if (current is null || current.Session != Session) return false;
        for (int index = 0; index < Slots.Count; index++) {
            PlayerSlotObservation original = Slots[index];
            PlayerSlotObservation sampled = current.Slots[index];
            // Flags can change without joining, leaving or replacing an actor
            if (original.Presence != sampled.Presence) return false;
            if (original.Presence != PlayerSlotPresence.Present) continue;
            if (sampled.Player!.Identity != original.Player!.Identity
                || sampled.Player.CommandTime < original.Player.CommandTime
                || requireEnableEligible && !sampled.Player.IsEnableEligible) return false;
        }
        return true;
    }
}
