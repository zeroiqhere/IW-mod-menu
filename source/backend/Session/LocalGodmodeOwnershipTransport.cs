using syndishanx_iw_tool.Features;

namespace Apparition.Backend.Session;

// A proven actor retirement reaches the retained writer even if later scalars recover
internal sealed class LocalGodmodeOwnershipTransport : ITeammateFlagsTransport {
    private readonly ITeammateFlagsTransport inner;
    private readonly Func<bool> retired;
    private readonly Func<bool> enableAllowed;

    public LocalGodmodeOwnershipTransport(ITeammateFlagsTransport inner, Func<bool> retired, Func<bool> enableAllowed) {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        this.retired = retired ?? throw new ArgumentNullException(nameof(retired));
        this.enableAllowed = enableAllowed ?? throw new ArgumentNullException(nameof(enableAllowed));
    }

    // Actor replacement is never reported as the end of the original process
    public bool Connected => inner.Connected;

    public uint ReadFlags() {
        RequireOriginalActor();
        return inner.ReadFlags();
    }

    public bool WriteFlags(uint expected, uint desired) {
        RequireOriginalActor();
        // Closing denies a new ON write entry without blocking reads or owned OFF restoration
        if ((desired & 1U) != 0 && !enableAllowed()) return false;
        return inner.WriteFlags(expected, desired);
    }

    private void RequireOriginalActor() {
        if (retired()) throw new IOException("The original local actor binding has been retired");
    }
}
