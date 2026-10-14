using syndishanx_iw_tool.Players;

namespace syndishanx_iw_tool.Features.Godmode;

// Shutdown joins every bound adapter before exposing release eligibility
public sealed partial class PlayerGodmodeRegistry {
    public async Task<GodmodeRegistryCloseResult> CloseAsync() {
        await gate.WaitAsync().ConfigureAwait(false);
        try {
            Entry[] owned;
            lock (entriesLock) {
                closed = true;
                owned = entries.Values.ToArray();
                // Closed permanently invalidates every open receipt without needing another epoch
                Volatile.Write(ref operating, owned.Select(entry => entry.Identity).ToArray());
            }
            bool stopped = true;
            bool restored = true;
            foreach (Entry entry in owned) {
                // A failed member must not prevent shutdown of the others
                GodmodeCloseResult outcome = await CloseEntryAsync(entry).ConfigureAwait(false);
                stopped &= outcome.Stopped;
                restored &= outcome.Restored;
            }
            return new(stopped, restored, BuildSnapshot(owned.Select(entry => entry.Identity).ToArray()));
        } finally {
            Volatile.Write(ref operating, Array.Empty<PlayerIdentity>());
            gate.Release();
        }
    }

    private async Task<GodmodeCloseResult> CloseEntryAsync(Entry entry) {
        GodmodeCloseResult outcome;
        try {
            outcome = await entry.Feature.CloseAsync().ConfigureAwait(false);
        } catch (Exception exception) {
            outcome = new(false, false, exception.Message);
        }
        try {
            // Contradictory adapter state cannot release workers or owned flags
            if (entry.Feature.Active) {
                outcome = new(false, false, "The bound worker still reports active after close");
            } else if (entry.Feature.HasPendingRestore) {
                outcome = outcome with { Restored = false };
            }
        } catch (Exception exception) {
            outcome = new(false, false, exception.Message);
        }
        lock (entriesLock) {
            // Keep failed identities and adapters available for another close attempt
            entry.UnconfirmedStop = !outcome.Stopped;
            entry.CleanupRequired = !outcome.Restored;
            entry.Known = outcome.SafeToRelease;
            entry.Failure = outcome.FailureReason;
        }
        return outcome;
    }
}
