using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace syndishanx_iw_tool.Players.LocalBinding;

// Small exact reads retain copied bytes for one bounded consistency pass
internal sealed class ObservedBindingReads {
    private const long MinimumAddress = 0x10000;
    private const long MaximumAddress = 0x7FFFFFFF0000;
    private const int MaximumFields = 128;
    private const int MaximumReadWidth = 32;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private readonly Func<long, int, byte[]?> read;
    private readonly Dictionary<(long Address, int Width), byte[]> observed = new();

    internal ObservedBindingReads(Func<long, int, byte[]?> read) {
        this.read = read ?? throw new ArgumentNullException(nameof(read));
    }

    internal static void Pointer(long pointer) {
        if (pointer < MinimumAddress || pointer > MaximumAddress || pointer % 8 != 0) {
            throw new InvalidOperationException("A local binding pointer is outside supported aligned memory");
        }
    }

    internal static long Address(long origin, long offset, int width) {
        // Subtraction bounds avoid overflow before any callback receives an address
        if (origin < MinimumAddress || origin > MaximumAddress || offset < 0 || width <= 0
            || offset > MaximumAddress - origin || width - 1L > MaximumAddress - origin - offset) {
            throw new InvalidOperationException("A local binding span is outside supported memory");
        }
        return origin + offset;
    }

    internal int Int32(long address) => BinaryPrimitives.ReadInt32LittleEndian(Bytes(address, sizeof(int)));
    internal short Int16(long address) => BinaryPrimitives.ReadInt16LittleEndian(Bytes(address, sizeof(short)));
    internal long Int64(long address) => BinaryPrimitives.ReadInt64LittleEndian(Bytes(address, sizeof(long)));
    internal ulong UInt64(long address) => BinaryPrimitives.ReadUInt64LittleEndian(Bytes(address, sizeof(ulong)));
    internal byte Byte(long address) => Bytes(address, 1)[0];

    internal string Name(long address) {
        byte[] bytes = Bytes(address, 32);
        int end = Array.IndexOf(bytes, (byte)0);
        if (end <= 0) throw new InvalidOperationException("A local player name lacks a bounded terminator");
        string name;
        try { name = StrictUtf8.GetString(bytes, 0, end); }
        catch (DecoderFallbackException exception) {
            throw new InvalidOperationException("A local player name is not valid UTF-8", exception);
        }
        if (string.IsNullOrWhiteSpace(name) || name.Any(char.IsControl)) {
            throw new InvalidOperationException("A local player name contains unsupported text");
        }
        return name;
    }

    internal string Guid(long address) {
        byte[] bytes = Bytes(address, 21);
        if (Array.IndexOf(bytes, (byte)0) != 16) throw new InvalidOperationException("A local player GUID lacks sixteen bounded digits");
        for (int index = 0; index < 16; index++) {
            byte value = bytes[index];
            if (!(value is >= (byte)'0' and <= (byte)'9' or >= (byte)'A' and <= (byte)'F'
                or >= (byte)'a' and <= (byte)'f')) {
                throw new InvalidOperationException("A local player GUID contains a non-hexadecimal digit");
            }
        }
        return Encoding.ASCII.GetString(bytes, 0, 16);
    }

    private byte[] Bytes(long address, int width) {
        var key = (address, width);
        bool retained = observed.TryGetValue(key, out byte[]? prior);
        if (!retained && observed.Count >= MaximumFields) {
            throw new InvalidOperationException("The local binding read exceeds its field bound");
        }
        byte[] bytes = ReadExact(address, width);
        if (retained) {
            if (!prior!.AsSpan().SequenceEqual(bytes)) throw new InvalidOperationException("The local binding fields changed during the read");
        } else {
            observed.Add(key, bytes);
        }
        return bytes;
    }

    internal void VerifyUnchanged() => VerifyUnchangedFields(null);

    // The captured command clock may advance while every other copied field stays exact
    internal int VerifyUnchangedCommandTime(long address) {
        var key = (address, sizeof(int));
        if (!observed.TryGetValue(key, out byte[]? firstCommandTime)) {
            throw new InvalidOperationException("The command-time field was not observed during the read");
        }
        VerifyUnchangedFields(key);
        // Sample the clock after the other copied fields have passed their final read
        int first = BinaryPrimitives.ReadInt32LittleEndian(firstCommandTime);
        int current = BinaryPrimitives.ReadInt32LittleEndian(ReadExact(address, sizeof(int)));
        if (first < 0 || current < first) {
            throw new InvalidOperationException("The local command time rolled back during the read");
        }
        return current;
    }

    private void VerifyUnchangedFields((long Address, int Width)? exceptField) {
        // Every other copied field must still match its first sample exactly
        foreach (var field in observed) {
            if (exceptField == field.Key) continue;
            byte[] current = ReadExact(field.Key.Address, field.Key.Width);
            if (!field.Value.AsSpan().SequenceEqual(current)) {
                throw new InvalidOperationException("The local binding fields changed during the read");
            }
        }
    }

    private byte[] ReadExact(long address, int width) {
        Address(address, 0, width);
        if (width > MaximumReadWidth) throw new InvalidOperationException("The local binding read exceeds its width bound");
        byte[]? bytes;
        try { bytes = read(address, width); }
        catch (Exception exception) when (exception is IOException or InvalidOperationException
            or System.ComponentModel.Win32Exception) {
            throw new IOException("The local binding read failed", exception);
        }
        if (bytes is null || bytes.Length != width) throw new IOException("The local binding read returned an incomplete field");
        // The callback may reuse its storage while later fields are being read
        return (byte[])bytes.Clone();
    }
}
