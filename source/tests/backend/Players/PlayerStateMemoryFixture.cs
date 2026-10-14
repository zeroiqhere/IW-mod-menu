using System.Buffers.Binary;
using System.Text;
using syndishanx_iw_tool.Players;

namespace Backend.Tests;

// Sparse owned bytes reject any read outside the fields prepared by the fixture
internal sealed class PlayerStateMemoryFixture {
    internal const long ModuleBase = 0x140000000;
    internal const long Table = ModuleBase + 0x6B22950;
    internal const long CountAddress = ModuleBase + 0x6B229E0;
    internal const long Anchor = 0x210000000;
    internal const int LegacyStride = 715416;
    private readonly Dictionary<long, byte> bytes = new();
    private readonly Dictionary<long, int> readCounts = new();
    internal List<(long Address, int Width)> Reads { get; } = new();
    internal Action<long, int, int>? BeforeRead { get; set; }

    internal PlayerStateMemoryFixture() {
        Int32(ModuleBase + 0x2246C30 + 8, 9);
        Bytes(ModuleBase + 0x2246C30 + 32, new byte[] { 0, 1 });
        foreach (long address in new[] { ModuleBase + 0x6B2C9D4, ModuleBase + 0x6B2C9D8, ModuleBase + 0x6B2C9DC }) {
            Int32(address, 0);
        }
        Int64(Table, Anchor);
        Int32(CountAddress, 4);
        for (int slot = 0; slot <= 3; slot++) {
            Int64(Table + slot * 8, Server(slot));
            Int16(Entity(slot), (short)slot);
            Int32(Entity(slot) + 0x9C, slot);
            Int64(Entity(slot) + 0x170, Client(slot));
            Int32(Entity(slot) + 0x1E0, 100);
            Int32(Entity(slot) + 0x1C8, 0x200);
            Int32(Client(slot), 100);
            Int32(Client(slot) + 4, 0);
            Int32(Client(slot) + 0x4A90, 2);
            Int32(Client(slot) + 0x4A94, 0);
            Int32(Client(slot) + 0x4BDC, slot);
            Text(Client(slot) + 0x4BB0, Name(slot), 32);
            Text(Client(slot) + 0x4C50, Name(slot), 32);
            ServerRecord(Server(slot), slot);
        }
    }

    internal static long Entity(int slot) => ModuleBase + 0x3D22610 + slot * 0x3F8;
    internal static long Client(int slot) => 0x260000000 + slot * 0x10000;
    // The selected zero record is the actual first pointer, not a stride lookalike
    internal static long Server(int slot) => slot == 0 ? Anchor : 0x300000000 + slot * 0x2000000;
    internal static long LegacyServer(int slot) => Anchor + slot * LegacyStride;
    internal static string Name(int slot) => "Player" + slot;
    internal static string Guid(int slot) => slot.ToString("X16");

    internal PlayerState Read(int slot = 1) => PlayerStateReader.Read(ModuleBase, slot, ReadBytes);

    internal byte[]? ReadBytes(long address, int width) {
        Reads.Add((address, width));
        int count = readCounts.TryGetValue(address, out int prior) ? prior + 1 : 1;
        readCounts[address] = count;
        // Hooks simulate a field changing between the reader's two checked snapshots
        BeforeRead?.Invoke(address, width, count);
        var result = new byte[width];
        for (int index = 0; index < width; index++) {
            if (!bytes.TryGetValue(address + index, out result[index])) return null;
        }
        return result;
    }

    internal void ServerRecord(long address, int slot) {
        Int32(address + 8, 5);
        Int64(address + 136, Entity(slot));
        Text(address + 1188, Name(slot), 32);
        Text(address + 0x9F340, Guid(slot), 21);
    }

    internal void AddLegacyLookalike(int slot = 1) => ServerRecord(LegacyServer(slot), slot);

    internal void Int16(long address, short value) {
        var data = new byte[sizeof(short)];
        BinaryPrimitives.WriteInt16LittleEndian(data, value);
        Bytes(address, data);
    }

    internal void Int32(long address, int value) {
        var data = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(data, value);
        Bytes(address, data);
    }

    internal void Int64(long address, long value) {
        var data = new byte[sizeof(long)];
        BinaryPrimitives.WriteInt64LittleEndian(data, value);
        Bytes(address, data);
    }

    internal void Text(long address, string value, int width) {
        byte[] data = new byte[width];
        Encoding.UTF8.GetBytes(value).CopyTo(data, 0);
        Bytes(address, data);
    }

    internal void Bytes(long address, byte[] data) {
        for (int index = 0; index < data.Length; index++) bytes[address + index] = data[index];
    }
}
