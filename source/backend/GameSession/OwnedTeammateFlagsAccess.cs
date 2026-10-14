using System.IO;
using syndishanx_iw_tool.Players;

namespace syndishanx_iw_tool.GameSession;

// Both native reads and the final writer retain the most recent command time under one lock
internal sealed class OwnedTeammateFlagsAccess {
    private readonly PlayerIdentity identity;
    private readonly object gate;
    private readonly Func<bool> connected;
    private readonly Func<OwnedPlayerFlagsState> read;
    private readonly Func<long, uint, uint, bool> write;
    private int lastCommandTime;
    private bool retired;

    public OwnedTeammateFlagsAccess(PlayerState initial, object gate, Func<bool> connected,
        Func<OwnedPlayerFlagsState> read, Func<long, uint, uint, bool> write)
        : this(StrictSample(initial), gate, connected, read, write) { }

    public OwnedTeammateFlagsAccess(OwnedPlayerFlagsState initial, object gate, Func<bool> connected,
        Func<OwnedPlayerFlagsState> read, Func<long, uint, uint, bool> write) {
        ArgumentNullException.ThrowIfNull(initial);
        identity = initial.Identity ?? throw new ArgumentException("An original identity is required", nameof(initial));
        this.gate = gate ?? throw new ArgumentNullException(nameof(gate));
        this.connected = connected ?? throw new ArgumentNullException(nameof(connected));
        this.read = read ?? throw new ArgumentNullException(nameof(read));
        this.write = write ?? throw new ArgumentNullException(nameof(write));
        if (initial.CommandTime < 0) throw new ArgumentOutOfRangeException(nameof(initial));
        lastCommandTime = initial.CommandTime;
    }

    public PlayerIdentity Identity => identity;

    // A proven replacement retires old references even when a new read-only cache is allowed
    public void Retire() { lock (gate) retired = true; }

    public void Observe(OwnedPlayerFlagsState current) {
        lock (gate) {
            if (retired) throw new IOException("The original owned teammate access is retired");
            try { AcceptSample(current); }
            catch { retired = true; throw; }
        }
    }

    public OwnedPlayerFlagsState Read() { lock (gate) return ReadValidated(); }

    public bool Write(long address, uint expected, uint desired) {
        lock (gate) {
            if (address != identity.Entity + 0x1C8 || (expected ^ desired) != 1U) return false;
            var current = ReadValidated();
            if (current.Flags != expected || (desired & 1U) != 0 && !current.IsEnableEligible) return false;
            try { return write(address, expected, desired); }
            catch {
                // A failed final actor check cannot later resume against recovered scalar values
                retired = true;
                throw;
            }
        }
    }

    private OwnedPlayerFlagsState ReadValidated() {
        if (retired) throw new IOException("The original owned teammate access is retired");
        try {
            if (!connected()) throw new IOException("The original game process has closed");
            var current = read() ?? throw new IOException("The owned teammate source is unavailable");
            AcceptSample(current);
            return current;
        } catch {
            retired = true;
            throw;
        }
    }
    // Strict callers already checked living eligibility before handing over this baseline
    private static OwnedPlayerFlagsState StrictSample(PlayerState initial) {
        ArgumentNullException.ThrowIfNull(initial);
        return new(initial.Identity, initial.CommandTime, initial.Flags, true);
    }

    private void AcceptSample(OwnedPlayerFlagsState current) {
        if (!connected() || current is null || current.Identity != identity || current.CommandTime < 0 || current.CommandTime < lastCommandTime) {
            throw new IOException("The original teammate identity or command time changed");
        }
        // This watermark also includes successful session proofs and strict capture samples
        lastCommandTime = current.CommandTime;
    }
}
