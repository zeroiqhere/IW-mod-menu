using System.IO;
using syndishanx_iw_tool.Features;
using syndishanx_iw_tool.Players;

namespace syndishanx_iw_tool.GameSession;

// One selected identity is retained for the entire feature lifetime
public sealed class BoundPlayerFlagsTransport : ITeammateFlagsTransport {
    private readonly PlayerIdentity identity;
    private readonly Func<bool> connected;
    private readonly Func<OwnedPlayerFlagsState> read;
    private readonly Func<long, uint, uint, bool> write;
    private readonly object accessLock = new();
    private int lastCommandTime;
    private bool invalidated;

    // Legacy callers retain their existing strict-reader contract
    public BoundPlayerFlagsTransport(PlayerIdentity selectedIdentity, PlayerState initial, Func<bool> connected,
        Func<PlayerState> read, Func<long, uint, uint, bool> write)
        : this(selectedIdentity, initial, connected, LegacyReader(read), write) { }

    public BoundPlayerFlagsTransport(PlayerIdentity selectedIdentity, PlayerState initial, Func<bool> connected,
        Func<OwnedPlayerFlagsState> read, Func<long, uint, uint, bool> write) {
        ArgumentNullException.ThrowIfNull(selectedIdentity);
        ArgumentNullException.ThrowIfNull(initial);
        if (selectedIdentity != initial.Identity) {
            throw new IOException("The selected teammate changed before binding");
        }
        this.connected = connected ?? throw new ArgumentNullException(nameof(connected));
        this.read = read ?? throw new ArgumentNullException(nameof(read));
        this.write = write ?? throw new ArgumentNullException(nameof(write));
        identity = initial.Identity;
        lastCommandTime = initial.CommandTime;
    }

    private static Func<OwnedPlayerFlagsState> LegacyReader(Func<PlayerState> read) {
        ArgumentNullException.ThrowIfNull(read);
        return () => {
            var current = read();
            return new(current.Identity, current.CommandTime, current.Flags, true);
        };
    }

    // False means the original process is gone, rather than a changed player identity
    public bool Connected => connected();

    public uint ReadFlags() {
        lock (accessLock) {
            return ReadValidated().Flags;
        }
    }

    public bool WriteFlags(uint expected, uint desired) {
        lock (accessLock) {
            if ((expected ^ desired) != 1) return false;
            var current = ReadValidated();
            if (current.Flags != expected) return false;
            // Life changes block a new ON without retiring the retained restoration owner
            if ((desired & 1U) != 0 && !current.IsEnableEligible) return false;
            try {
                // The native callback rechecks identity, time, eligibility, and bytes under its lock
                return write(identity.Entity + 0x1C8, expected, desired);
            } catch {
                invalidated = true;
                throw;
            }
        }
    }

    private OwnedPlayerFlagsState ReadValidated() {
        if (invalidated) {
            throw new IOException("The selected player changed; reconnect in a fresh match");
        }
        try {
            if (!Connected) {
                throw new IOException("The original game process has closed");
            }
            OwnedPlayerFlagsState current = read() ?? throw new IOException("The original flags source is unavailable");
            if (!Connected || current.Identity != identity || current.CommandTime < 0 || current.CommandTime < lastCommandTime) {
                throw new IOException("The selected player or match identity changed");
            }
            // A time reset or failed state check ends this binding permanently
            lastCommandTime = current.CommandTime;
            return current;
        } catch {
            invalidated = true;
            throw;
        }
    }
}
