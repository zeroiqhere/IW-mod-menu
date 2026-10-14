using syndishanx_iw_tool.Players;

namespace syndishanx_iw_tool.Features.Godmode;

// Factories must only bind validated identities and must not write or start workers
public sealed partial class PlayerGodmodeRegistry {
    // Reference ownership survives close so cleanup retries cannot transfer an adapter
    internal bool OwnsFeature(IPlayerGodmodeFeature feature) {
        lock (entriesLock) return entries.Values.Any(entry => ReferenceEquals(entry.Feature, feature));
    }

    private async Task<Entry> BindAsync(PlayerIdentity identity) {
        Entry? sameSlot;
        lock (entriesLock) {
            if (entries.TryGetValue(identity, out Entry? existing)) {
                return existing;
            }
            sameSlot = entries.Values.FirstOrDefault(entry => entry.Identity.Slot == identity.Slot);
        }
        if (HasPendingCleanup()) {
            throw new InvalidOperationException("Unconfirmed cleanup prevents binding a different player identity");
        }
        if (sameSlot is not null) {
            if (sameSlot.Feature.Active || PendingCleanup(sameSlot)) {
                throw new InvalidOperationException("The previous identity in this slot still owns a change");
            }
            GodmodeCloseResult outcome = await CloseEntryAsync(sameSlot).ConfigureAwait(false);
            if (!outcome.SafeToRelease) {
                throw new InvalidOperationException("The previous identity could not be safely released");
            }
            lock (entriesLock) {
                // Only a joined, restored old binding can leave the registry
                entries.Remove(sameSlot.Identity);
            }
        }
        var created = new Entry(identity, factory(identity)
            ?? throw new InvalidOperationException("The player adapter could not be created"));
        lock (entriesLock) {
            // Reusing an adapter would merge two identities into one writer
            if (entries.Values.Any(entry => ReferenceEquals(entry.Feature, created.Feature))) {
                throw new InvalidOperationException("A player adapter cannot be shared between different identities");
            }
            entries.Add(identity, created);
        }
        return created;
    }
}
