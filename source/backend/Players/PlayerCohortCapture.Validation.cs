namespace syndishanx_iw_tool.Players;

public sealed partial class PlayerCohortCapture {
    private static bool Valid(PlayerCohortObservation? observation) {
        if (observation?.Session is not { ProcessId: > 0, ModuleBase: > 0, ServerClients: > 0, Capacity: 4 } session
            || observation.Slots.Count != 4 || observation.Slots.Any(slot => slot is null)
            || !observation.Slots.Select(slot => slot.Slot).Order().SequenceEqual(new[] { 0, 1, 2, 3 })) return false;
        var members = new List<PlayerIdentity>();
        foreach (PlayerSlotObservation slot in observation.Slots) {
            if (slot.Presence == PlayerSlotPresence.ConfirmedAbsent && slot.Player is null) continue;
            if (slot.Presence != PlayerSlotPresence.Present || slot.Player is not { CommandTime: >= 0 } player
                || player.Identity is not { Entity: > 0, Client: > 0, ServerClient: > 0, ServerCount: 4 } identity
                || identity.Slot != slot.Slot || identity.ServerClients != session.ServerClients
                || string.IsNullOrWhiteSpace(identity.Name) || identity.Name.Any(char.IsControl)
                || identity.Guid is null || identity.Guid.Length != 16 || !identity.Guid.All(Uri.IsHexDigit)) return false;
            members.Add(identity);
        }
        // Empty supported storage cannot authorize an aggregate game operation
        return members.Count > 0
            && members.Select(member => member.Entity).Distinct().Count() == members.Count
            && members.Select(member => member.Client).Distinct().Count() == members.Count
            && members.Select(member => member.ServerClient).Distinct().Count() == members.Count
            && members.Select(member => member.Guid).Distinct(StringComparer.OrdinalIgnoreCase).Count() == members.Count;
    }
}
