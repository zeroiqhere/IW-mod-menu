using System.Buffers.Binary;
using System.IO;

namespace syndishanx_iw_tool.GameSession;

// File section bytes after SizeOfHeaders do not belong to the loaded header
internal static class LocalExecutableHeader {
    private static int HeaderSize(byte[] bytes) {
        if (bytes.Length < 64 || bytes[0] != 'M' || bytes[1] != 'Z')
            throw new IOException("A complete DOS header is required");
        int pe = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(60, 4));
        if (pe < 64 || pe > bytes.Length - 88 || BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(pe, 4)) != 0x4550)
            throw new IOException("A complete PE header is required");
        int size = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(pe + 84, 4));
        if (size < pe + 88 || size > bytes.Length)
            throw new IOException("Declared header size is outside the captured span");
        return size;
    }

    public static void Verify(byte[] loaded, byte[] installed) {
        ArgumentNullException.ThrowIfNull(loaded);
        ArgumentNullException.ThrowIfNull(installed);
        int size = HeaderSize(installed);
        if (HeaderSize(loaded) != size || !loaded.AsSpan(0, size).SequenceEqual(installed.AsSpan(0, size)))
            throw new IOException("Loaded executable header differs");
    }
}
