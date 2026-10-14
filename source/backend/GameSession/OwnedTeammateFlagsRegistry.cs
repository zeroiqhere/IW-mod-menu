using System.IO;
using syndishanx_iw_tool.Players;

namespace syndishanx_iw_tool.GameSession;

// At most four read-access entries share the native transport lock; none owns a feature reservation
internal sealed class OwnedTeammateFlagsRegistry {
    private readonly object gate;
    private readonly Func<bool> connected;
    private readonly Func<PlayerIdentity, OwnedPlayerFlagsState> read;
    private readonly Func<long, uint, uint, bool> write;
    private readonly Dictionary<int, OwnedTeammateFlagsAccess> entries = new();

    public OwnedTeammateFlagsRegistry(object gate, Func<bool> connected,
        Func<PlayerIdentity, OwnedPlayerFlagsState> read, Func<long, uint, uint, bool> write) {
        this.gate = gate ?? throw new ArgumentNullException(nameof(gate));
        this.connected = connected ?? throw new ArgumentNullException(nameof(connected));
        this.read = read ?? throw new ArgumentNullException(nameof(read));
        this.write = write ?? throw new ArgumentNullException(nameof(write));
    }

    public OwnedPlayerFlagsState Read(PlayerIdentity identity) {
        lock (gate) return Access(identity).Read();
    }

    public OwnedTeammateFlagsAccess Bind(PlayerState initial) {
        ArgumentNullException.ThrowIfNull(initial);
        lock (gate) {
            // This already verified strict baseline cannot precede an owned observation watermark
            var current = new OwnedPlayerFlagsState(initial.Identity, initial.CommandTime, initial.Flags, true);
            var access = Access(initial.Identity, current);
            access.Observe(current);
            return access;
        }
    }

    public void ObserveExisting(PlayerState current) {
        lock (gate) {
            if (!entries.ContainsKey(current.Identity.Slot)) return;
            var sample = new OwnedPlayerFlagsState(current.Identity, current.CommandTime, current.Flags, true);
            Access(current.Identity, sample).Observe(sample);
        }
    }

    private OwnedTeammateFlagsAccess Access(PlayerIdentity identity, OwnedPlayerFlagsState? verified = null) {
        if (identity is null || identity.Slot is < 0 or > 3) throw new IOException("The owned player slot is unsupported");
        if (entries.TryGetValue(identity.Slot, out var existing) && existing.Identity == identity) return existing;
        // A new full identity must first pass the original reader or an actual strict capture
        var sample = verified ?? read(identity);
        if (sample is null || sample.Identity != identity || sample.CommandTime < 0) {
            throw new IOException("The requested owned player was not verified");
        }
        var replacement = new OwnedTeammateFlagsAccess(sample, gate, connected, () => read(identity), write);
        // Old bound references stay retired; replacing this read cache never transfers a writer token
        existing?.Retire();
        entries[identity.Slot] = replacement;
        return replacement;
    }
}
