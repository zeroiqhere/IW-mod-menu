namespace syndishanx_iw_tool.Features;

// Bind one transport to one player identity for its entire lifetime
public interface ITeammateFlagsTransport {
    // False means the original process is gone, not that a player check failed
    bool Connected { get; }

    // Reads must validate identity and report failed reads through exceptions
    uint ReadFlags();

    // Preserve all other bits and refuse a changed value or player identity
    // Separate process reads and writes cannot form an atomic comparison
    bool WriteFlags(uint expected, uint desired);
}
