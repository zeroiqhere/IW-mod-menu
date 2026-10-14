using syndishanx_iw_tool.Features;

namespace Apparition.Backend.Session;

// New flag changes prove the retained process without removing its original restore route
internal sealed class OriginalConnectionFlagsTransport(
    ITeammateFlagsTransport inner, Func<bool> admitEnable) : ITeammateFlagsTransport {
    public bool Connected => inner.Connected;

    // The bound adapter keeps its full actor and command-time checks
    public uint ReadFlags() => inner.ReadFlags();

    public bool WriteFlags(uint expected, uint desired) {
        // An original OFF restore remains possible after cached liveness has retired
        if ((desired & 1U) != 0 && !admitEnable()) return false;
        return inner.WriteFlags(expected, desired);
    }
}
