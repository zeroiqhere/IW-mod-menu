using System.Buffers.Binary;
using System.Text;
using syndishanx_iw_tool.Players.LocalBinding;

namespace Backend.Tests;

// All reads use sparse owned fields and fail when any requested byte is absent
internal sealed class LocalBindingMemoryFixture {
    internal const long ModuleBase = 0x140000000;
    internal const long ControllerAddress = ModuleBase + 0x2204464;
    internal const long AccountAddress = ModuleBase + 0x779C7A8;
    internal const long SelectorAddress = ModuleBase + 0x6005468;
    internal const long Selector = 0x210000000;
    internal const long FalseSession = ModuleBase + 0x6B4E080;
    internal const long TrueSession = ModuleBase + 0x6B50B30;
    internal const long Table = ModuleBase + 0x6B22950;
    internal const long CountAddress = ModuleBase + 0x6B229E0;
    internal const long Anchor = 0x300000000;
    internal const int ServerStride = 715416;
    internal const ulong Account = 0x0123456789ABCDEF;
    private readonly Dictionary<long, byte> bytes = new();
    private readonly Dictionary<long, int> counts = new();
    internal List<(long Address, int Width)> Reads { get; } = new();
    internal Action<long, int, int>? BeforeRead { get; set; }
    internal int Member { get; }
    internal long Session { get; }

    internal LocalBindingMemoryFixture(int member = 0, byte selectorByte = 0) {
        Member = member;
        Session = selectorByte == 0 ? FalseSession : TrueSession;
        Int32(ControllerAddress, 0);
        UInt64(AccountAddress, Account);
        Int64(SelectorAddress, Selector);
        Bytes(Selector + 0x10, new[] { selectorByte });
        InitializeSession(FalseSession);
        InitializeSession(TrueSession);
        Registered(Session, member, Account);
        Int32(ModuleBase + 0x2246C30 + 8, 9);
        Bytes(ModuleBase + 0x2246C30 + 32, new byte[] { 0, 1 });
        foreach (long address in new[] { ModuleBase + 0x6B2C9D4, ModuleBase + 0x6B2C9D8, ModuleBase + 0x6B2C9DC }) {
            Int32(address, 0);
        }
        Int32(CountAddress, 4);
        for (int slot = 0; slot <= 3; slot++) {
            Int64(Table + slot * 8, Server(slot));
            Identity(slot);
        }
    }

    internal static long Entity(int slot) => ModuleBase + 0x3D22610 + slot * 0x3F8;
    internal static long Client(int slot) => 0x400000000 + slot * 0x10000;
    internal static long Server(int slot) => Anchor + slot * ServerStride;
    internal static string Name(int slot) => "Player" + slot;
    internal static string Guid(int slot) => slot.ToString("X16");

    internal LocalAccountSessionBinding Read() => LocalAccountSessionReader.Read(ModuleBase, ReadBytes);

    internal byte[]? ReadBytes(long address, int width) {
        Reads.Add((address, width));
        int count = counts.TryGetValue(address, out int prior) ? prior + 1 : 1;
        counts[address] = count;
        BeforeRead?.Invoke(address, width, count);
        var result = new byte[width];
        for (int index = 0; index < width; index++) {
            if (!bytes.TryGetValue(address + index, out result[index])) return null;
        }
        return result;
    }

    internal void InitializeSession(long session) {
        Bytes(session + 0x10, new byte[] { 1 });
        for (int member = 0; member < 18; member++) {
            Bytes(session + member * 0x38 + 0x60, new byte[] { 0 });
            UInt64(session + member * 0x38 + 0x68, 0);
        }
    }

    internal void Registered(long session, int member, ulong account, byte flag = 1) {
        Bytes(session + member * 0x38 + 0x60, new[] { flag });
        UInt64(session + member * 0x38 + 0x68, account);
    }

    internal void Identity(int slot) {
        Int16(Entity(slot), (short)slot);
        Int32(Entity(slot) + 0x9C, slot);
        Int64(Entity(slot) + 0x170, Client(slot));
        Int32(Client(slot) + 0x4A90, 2);
        Int32(Client(slot) + 0x4BDC, slot);
        Text(Client(slot) + 0x4BB0, Name(slot), 32);
        Text(Client(slot) + 0x4C50, Name(slot), 32);
        Int32(Server(slot) + 8, 5);
        Int64(Server(slot) + 136, Entity(slot));
        Text(Server(slot) + 1188, Name(slot), 32);
        Text(Server(slot) + 0x9F340, Guid(slot), 21);
        // Health, movement type and playing state are intentionally absent from this identity fixture
    }

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

    internal void UInt64(long address, ulong value) {
        var data = new byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64LittleEndian(data, value);
        Bytes(address, data);
    }

    internal void Text(long address, string value, int width) {
        var data = new byte[width];
        Encoding.UTF8.GetBytes(value).CopyTo(data, 0);
        Bytes(address, data);
    }

    internal void Bytes(long address, byte[] data) {
        for (int index = 0; index < data.Length; index++) bytes[address + index] = data[index];
    }
}
