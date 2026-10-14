using syndishanx_iw_tool.Features;

namespace Apparition.Backend.Session;

// The existing flag primitive remains the only owner of writes and restoration
internal sealed class TeammateGodmodeOwnershipTransport(ITeammateFlagsTransport inner,
    Func<bool, bool> validate) : ITeammateFlagsTransport {
    // Retired actor proof never pretends that the original process has ended
    public bool Connected => inner.Connected;

    public uint ReadFlags() {
        if (!validate(false)) throw new IOException("The original teammate ownership proof was retired");
        return inner.ReadFlags();
    }

    public bool WriteFlags(uint expected, uint desired) {
        bool enabling = (desired & 1U) != 0;
        // Closing blocks new ON while exact retained OFF remains available to cleanup
        if (!validate(enabling)) return false;
        return inner.WriteFlags(expected, desired);
    }
}
