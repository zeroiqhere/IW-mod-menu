using syndishanx_iw_tool.Players;

namespace syndishanx_iw_tool.Features.Godmode;

// The registry gate owns all observations and watermarks for one group operation
internal sealed class GodmodeCohortGuard {
    private readonly PlayerCohortCapture captured;
    private readonly Func<PlayerCohortObservation> read;
    private readonly Dictionary<PlayerIdentity, int> commandTimes;

    internal GodmodeCohortGuard(PlayerCohortCapture captured, Func<PlayerCohortObservation> read) {
        this.captured = captured;
        this.read = read;
        commandTimes = captured.Slots.Where(slot => slot.Player is not null)
            .ToDictionary(slot => slot.Player!.Identity, slot => slot.Player!.CommandTime);
    }

    internal void Validate(bool requireEnableEligible) {
        PlayerCohortObservation observation = read();
        if (!captured.Matches(observation, requireEnableEligible))
            throw new InvalidOperationException("The complete captured lobby or player eligibility changed");
        foreach (PlayerSlotObservation slot in observation.Slots) {
            if (slot.Player is not { } player) continue;
            // A rollback above the initial timestamp still invalidates this operation
            if (player.CommandTime < commandTimes[player.Identity])
                throw new InvalidOperationException("A captured lobby player command time rolled back");
        }
        foreach (PlayerSlotObservation slot in observation.Slots) {
            if (slot.Player is { } player) commandTimes[player.Identity] = player.CommandTime;
        }
    }
}
