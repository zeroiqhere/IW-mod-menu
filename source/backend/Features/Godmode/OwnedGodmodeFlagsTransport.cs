namespace syndishanx_iw_tool.Features.Godmode;

internal sealed record GodmodeFlagsWrite(uint Expected, uint Desired, bool Accepted);

// Forward the primitive's one write while observing its exact owned baseline
internal sealed class OwnedGodmodeFlagsTransport(ITeammateFlagsTransport transport) : ITeammateFlagsTransport {
    private long capturedOriginal = -1;
    private GodmodeFlagsWrite? lastWrite;

    public bool Connected => transport.Connected;
    public GodmodeFlagsWrite? LastWrite => Volatile.Read(ref lastWrite);

    public uint? OriginalFlags {
        get {
            long captured = Volatile.Read(ref capturedOriginal);
            return captured < 0 ? null : (uint)captured;
        }
    }

    public uint ReadFlags() => transport.ReadFlags();

    public bool WriteFlags(uint expected, uint desired) {
        if ((expected ^ desired) != 1U) {
            return false;
        }
        if ((expected & 1U) == 0) {
            // Record before forwarding because rejection can still apply the change
            Volatile.Write(ref capturedOriginal, expected);
        }
        // Publish an unconfirmed receipt first if the transport later throws
        Volatile.Write(ref lastWrite, new(expected, desired, false));
        bool accepted = transport.WriteFlags(expected, desired);
        Volatile.Write(ref lastWrite, new(expected, desired, accepted));
        return accepted;
    }
}
