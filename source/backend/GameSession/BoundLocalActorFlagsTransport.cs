using syndishanx_iw_tool.Features;
using syndishanx_iw_tool.Players.LocalBinding;
using syndishanx_iw_tool.Players.LocalObservation;

namespace syndishanx_iw_tool.GameSession;

// A complete local account and actor binding owns this adapter for its whole lifetime
public sealed class BoundLocalActorFlagsTransport : ITeammateFlagsTransport {
    private readonly LocalAccountSessionBinding binding;
    private readonly Func<bool> connected;
    private readonly Func<LocalOwnedActorFlagsSnapshot> read;
    private readonly Func<long, uint, uint, bool> write;
    private readonly object accessLock = new();
    private int lastCommandTime;
    // The first failure stays with the original binding after retirement
    private Exception? retirementCause;

    public BoundLocalActorFlagsTransport(LocalActorFlagsSnapshot initial, Func<bool> connected,
        Func<LocalOwnedActorFlagsSnapshot> read, Func<long, uint, uint, bool> write) {
        ArgumentNullException.ThrowIfNull(initial);
        ArgumentNullException.ThrowIfNull(connected);
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(write);
        ValidateBindingShape(initial.Binding);
        binding = initial.Binding;
        lastCommandTime = initial.CommandTime;
        this.connected = connected;
        this.read = read;
        this.write = write;
    }

    // Actor invalidation must never pretend that the original process has ended
    public bool Connected => connected();

    public uint ReadFlags() {
        lock (accessLock) return ReadValidated().Flags;
    }

    public bool WriteFlags(uint expected, uint desired) {
        lock (accessLock) {
            // Unrelated bits and an observably changed baseline cannot reach the native writer
            if ((expected ^ desired) != 1U) return false;
            var current = ReadValidated();
            if (current.Flags != expected) return false;
            // Strict life admission gates a new ON without blocking restoration of an owned OFF
            if ((desired & 1U) != 0 && !current.IsEnableEligible) return false;
            // The callback must revalidate the original actor and exact bytes under its own native lock
            try { return write(binding.Identity.Entity + 0x1C8, expected, desired); }
            catch (Exception cause) {
                // A failed writer-side actor check cannot recover into an old binding later
                retirementCause ??= cause;
                throw;
            }
        }
    }

    private LocalOwnedActorFlagsSnapshot ReadValidated() {
        if (retirementCause is { } cause) {
            throw new IOException($"The original local actor binding is no longer usable; first failure: {cause.Message}", cause);
        }
        try {
            if (!Connected) throw new IOException("The original game process has closed");
            var current = read() ?? throw new IOException("The current local actor flags are unavailable");
            if (!Connected) throw new IOException("The original game process has closed");
            if (current.Binding != binding) {
                throw new IOException("The local account, actor or match binding changed");
            }
            if (current.CommandTime < lastCommandTime) {
                throw new IOException("The local actor command time moved backwards");
            }
            // A failed read or time rollback permanently retires this writer without retargeting it
            lastCommandTime = current.CommandTime;
            return current;
        } catch (Exception error) {
            retirementCause ??= error;
            throw;
        }
    }

    private static void ValidateBindingShape(LocalAccountSessionBinding binding) {
        const long maximumAddress = 0x7FFFFFFF0000;
        static bool Pointer(long address) => address is >= 0x10000 and <= maximumAddress && address % 8 == 0;
        var identity = binding.Identity;
        long recordOffset = (long)binding.MemberIndex * 715416;
        // These bounded shape checks supplement the source's full account and backlink proof
        if (binding.Controller != 0 || binding.AccountId == 0 || binding.MemberIndex is < 0 or > 3
            || identity is null || identity.Slot != binding.MemberIndex || identity.ServerCount != 4
            || !Pointer(binding.SelectorPointer) || !Pointer(binding.Session)
            || !Pointer(identity.Entity) || identity.Entity > maximumAddress - 0x1CB
            || !Pointer(identity.Client) || identity.Client > maximumAddress - 0x4A97
            || !Pointer(identity.ServerClients) || !Pointer(identity.ServerClient)
            || recordOffset > maximumAddress - identity.ServerClients
            || identity.ServerClient != identity.ServerClients + recordOffset) {
            throw new ArgumentException("The local actor binding has an unsupported shape", nameof(binding));
        }
        // Name and GUID validation here is shape validation, never local account authentication
        _ = new LocalPlayerObservation(1, identity, LocalPlayerLife.Unknown, null, null, "Binding shape checked");
    }
}
