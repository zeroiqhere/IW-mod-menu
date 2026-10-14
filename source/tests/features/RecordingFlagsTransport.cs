using syndishanx_iw_tool.Features;

namespace Features.Tests;

// Store synthetic flags and enforce the same expected-value check as the adapter
internal sealed class RecordingFlagsTransport : ITeammateFlagsTransport {
    private int flags = 0x200;
    private int reads;
    private int writes;
    private volatile bool connected = true;

    public bool Connected { get => connected; set => connected = value; }
    public uint Flags { get => unchecked((uint)Volatile.Read(ref flags)); set => Volatile.Write(ref flags, unchecked((int)value)); }
    public int Reads => Volatile.Read(ref reads);
    public int Writes => Volatile.Read(ref writes);
    public Func<int, uint>? OnRead { get; set; }
    public Func<int, uint, uint, bool>? OnWrite { get; set; }

    public uint ReadFlags() {
        int count = Interlocked.Increment(ref reads);
        return OnRead is null ? Flags : OnRead(count);
    }

    public bool WriteFlags(uint expected, uint desired) {
        int count = Interlocked.Increment(ref writes);
        // The callback can model a backend failure without touching a process
        if (OnWrite is not null && !OnWrite(count, expected, desired)) {
            return false;
        }
        return Interlocked.CompareExchange(ref flags, unchecked((int)desired), unchecked((int)expected)) == unchecked((int)expected);
    }
}
