using System.Buffers.Binary;

namespace Backend.Tests;

// Owned exact fixture bytes exercise the reader without a process or game
internal sealed class SessionHostMemoryFixture {
    internal const long ModuleBase = 0x140000000;
    internal const long Session = 0x2400000;
    internal const long FirstParty = ModuleBase + 0x524C4A0;
    internal const long SecondParty = ModuleBase + 0x5254DA0;
    internal const int ActivityOffset = 0x2D10;
    internal const int HostOffset = 0x2C50;
    private readonly Dictionary<long, byte[]> memory = new();
    internal readonly List<(long Address, int Width)> Reads = new();
    internal Action<long, int, int>? BeforeRead;

    internal SessionHostMemoryFixture(bool second = false, int member = 3) {
        Int64(FirstParty, second ? Session + 8 : Session);
        Int64(SecondParty, second ? Session : Session + 8);
        Int32(FirstParty + ActivityOffset, 1);
        Int32(SecondParty + ActivityOffset, 1);
        Int32(FirstParty + HostOffset, member);
        Int32(SecondParty + HostOffset, member);
    }

    internal void Int64(long address, long value) {
        var bytes = new byte[sizeof(long)];
        BinaryPrimitives.WriteInt64LittleEndian(bytes, value);
        memory[address] = bytes;
    }

    internal void Int32(long address, int value) {
        var bytes = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        memory[address] = bytes;
    }

    internal byte[]? Read(long address, int width) {
        Reads.Add((address, width));
        // A transition can occur before a later read of the same observed field
        BeforeRead?.Invoke(address, width, Reads.Count(item => item.Address == address));
        return memory.TryGetValue(address, out byte[]? bytes) && bytes.Length == width
            ? (byte[])bytes.Clone() : null;
    }
}
