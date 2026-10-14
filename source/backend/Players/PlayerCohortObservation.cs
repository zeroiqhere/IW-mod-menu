namespace syndishanx_iw_tool.Players;

// Capacity describes supported storage, never the number of participants
public sealed record PlayerCohortSession(int ProcessId, long ModuleBase, long ServerClients, int Capacity);

public enum PlayerSlotPresence { Unknown, Present, ConfirmedAbsent }

// Only a source with established absence semantics may report ConfirmedAbsent
public sealed record PlayerSlotObservation(int Slot, PlayerSlotPresence Presence,
    OwnedPlayerFlagsState? Player = null, string? FailureReason = null);

// The copy prevents a caller from changing membership while an action waits
public sealed class PlayerCohortObservation {
    public PlayerCohortSession? Session { get; }
    public IReadOnlyList<PlayerSlotObservation> Slots { get; }

    public PlayerCohortObservation(PlayerCohortSession? session, IReadOnlyList<PlayerSlotObservation> slots) {
        ArgumentNullException.ThrowIfNull(slots);
        Session = session;
        Slots = Array.AsReadOnly(slots.ToArray());
    }
}
