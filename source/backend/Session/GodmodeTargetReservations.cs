using syndishanx_iw_tool.Players;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Apparition.Backend.Session;

// One connection prevents separate features from owning overlapping actor storage
internal sealed class GodmodeTargetReservations {
    private sealed record Reservation(PlayerIdentity Identity, bool Retained);
    private readonly object gate = new();
    private readonly Dictionary<object, Reservation> owners = new(ReferenceEqualityComparer.Instance);

    public void Acquire(object owner, PlayerIdentity identity) {
        ArgumentNullException.ThrowIfNull(owner);
        // These guards reject malformed inputs; the source still supplies actor authentication
        if (identity is null || identity.ServerCount != 4 || !HasSupportedShape(identity)
            || !HasSpan(identity.Entity, 0x3F8) || !HasSpan(identity.Client, 0x4C70)
            || !HasSpan(identity.ServerClient, 652117)) {
            throw new InvalidOperationException("The Godmode target has an unsupported identity");
        }
        lock (gate) {
            if (owners.TryGetValue(owner, out var existing)) {
                if (existing.Identity != identity)
                    throw new InvalidOperationException("The original Godmode owner cannot change targets");
                return;
            }
            // A different GUID or name cannot give another writer the same slot or pointers
            if (owners.Values.Any(entry => entry.Identity.Slot == identity.Slot
                || entry.Identity.Entity == identity.Entity || entry.Identity.Client == identity.Client)) {
                throw new InvalidOperationException("This player already has a Godmode owner in this connection");
            }
            owners.Add(owner, new(identity, false));
        }
    }

    public void Retain(object owner) {
        ArgumentNullException.ThrowIfNull(owner);
        lock (gate) {
            if (!owners.TryGetValue(owner, out var existing))
                throw new InvalidOperationException("The Godmode writer has no reserved target");
            // Retained writers keep their target even after OFF or a failed capture
            owners[owner] = existing with { Retained = true };
        }
    }

    public void ReleaseUnbound(object owner) {
        ArgumentNullException.ThrowIfNull(owner);
        lock (gate) {
            if (!owners.TryGetValue(owner, out var existing)) return;
            if (existing.Retained)
                throw new InvalidOperationException("A retained Godmode writer cannot transfer its target");
            owners.Remove(owner);
        }
    }

    private static bool HasSupportedShape(PlayerIdentity identity) {
        try {
            return new LocalPlayerObservation(1, identity, LocalPlayerLife.Unknown, null, null,
                "Reservation shape check").IsSupported();
        } catch (ArgumentException) { return false; }
    }

    // Full spans leave both readers and the four-byte flags field inside canonical addresses
    private static bool HasSpan(long address, int length) => address >= 0x10000
        && address % 8 == 0 && address <= 0x7FFFFFFF0000 - length;
}
