using System.Buffers.Binary;
using syndishanx_iw_tool.GameSession;

namespace Backend.Tests;

// File section bytes and mapped padding must not be confused with declared PE headers
internal static class LocalExecutableHeaderTests {
    private static byte[] Header() {
        var bytes = new byte[4096];
        bytes[0] = (byte)'M'; bytes[1] = (byte)'Z';
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(60, 4), 128);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(128, 4), 0x4550);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(212, 4), 1536);
        return bytes;
    }

    public static void MatchingDeclaredHeadersIgnoreDifferentRawSectionTails() {
        byte[] installed = Header();
        byte[] loaded = (byte[])installed.Clone();
        installed[1536] = 0x90;
        loaded[4095] = 0xCC;
        LocalExecutableHeader.Verify(loaded, installed);
        // A shorter capture that still contains every declared header byte is sufficient
        LocalExecutableHeader.Verify(loaded[..1536], installed);
    }

    public static void HeaderDifferencesAndInvalidSignaturesNeverValidateTheLoadedImage() {
        byte[] installed = Header();
        foreach (int offset in new[] { 0, 1, 100, 128, 129, 212, 1535 }) {
            byte[] loaded = (byte[])installed.Clone();
            loaded[offset] ^= 1;
            TestAssert.Throws<IOException>(() => LocalExecutableHeader.Verify(loaded, installed));
        }
        foreach (int offset in new[] { 0, 1, 128, 129 }) {
            byte[] malformed = (byte[])installed.Clone();
            malformed[offset] = 0;
            TestAssert.Throws<IOException>(() => LocalExecutableHeader.Verify(malformed, malformed));
        }
    }

    public static void TruncatedHeadersInvalidOffsetsAndSizesRejectBeforeSlicing() {
        foreach (int length in new[] { 0, 60, 64, 128, 215, 1535 }) {
            byte[] truncated = Header()[..length];
            TestAssert.Throws<IOException>(() => LocalExecutableHeader.Verify(truncated, Header()));
            TestAssert.Throws<IOException>(() => LocalExecutableHeader.Verify(Header(), truncated));
        }
        foreach (int offset in new[] { int.MinValue, -1, 0, 63, 4009, int.MaxValue }) {
            byte[] malformed = Header();
            BinaryPrimitives.WriteInt32LittleEndian(malformed.AsSpan(60, 4), offset);
            TestAssert.Throws<IOException>(() => LocalExecutableHeader.Verify(malformed, malformed));
        }
        foreach (int size in new[] { int.MinValue, -1, 0, 215, 4097, int.MaxValue }) {
            byte[] malformed = Header();
            BinaryPrimitives.WriteInt32LittleEndian(malformed.AsSpan(212, 4), size);
            TestAssert.Throws<IOException>(() => LocalExecutableHeader.Verify(malformed, malformed));
        }
    }

    public static void MissingBuffersCannotValidateAnExecutable() {
        TestAssert.Throws<ArgumentNullException>(() => LocalExecutableHeader.Verify(null!, Header()));
        TestAssert.Throws<ArgumentNullException>(() => LocalExecutableHeader.Verify(Header(), null!));
    }
}
