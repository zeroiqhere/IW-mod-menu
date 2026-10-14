using syndishanx_iw_tool.GameSession;
using System.Buffers.Binary;

namespace Backend.Tests;

internal static class LocalExecutableHeaderCacheTests {
    private static byte[] Header() {
        var result = new byte[4096];
        result[0] = (byte)'M'; result[1] = (byte)'Z';
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(60, 4), 128);
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(128, 4), 0x4550);
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(212, 4), 1536);
        return result;
    }

    public static void NativeReadRetryReusesTheVerifiedFileWithoutRehashingOrAliasingBuffers() {
        var cache = new LocalExecutableHeaderCache();
        int loads = 0;
        byte[] callbackBuffer = Header();
        var first = cache.GetOrRead(0x140000000, () => { loads++; return callbackBuffer; });
        first[0] = 0;
        callbackBuffer[1] = 0;
        // A failed later native read must not require reopening and rehashing the same file
        var retry = cache.GetOrRead(0x140000000, () => { loads++; throw new IOException("File must not be read again"); });
        TestAssert.Equal(1, loads);
        TestAssert.Equal((byte)'M', retry[0]);
        TestAssert.Equal((byte)'Z', retry[1]);
        retry[0] = 0;
        TestAssert.Equal((byte)'M', cache.GetOrRead(0x140000000, Header)[0]);
        cache.Clear();
        TestAssert.Equal((byte)'Z', cache.GetOrRead(0x150000000, () => { loads++; return Header(); })[1]);
        TestAssert.Equal(2, loads);
    }

    public static void InvalidOrChangedModuleRejectsBeforeReadingOrBorrowingTheFile() {
        foreach (long module in new[] { 0L, 0x10001L, long.MaxValue, 0x7FFFFFFF0000L }) {
            int loads = 0;
            TestAssert.Throws<InvalidOperationException>(() => new LocalExecutableHeaderCache().GetOrRead(module, () => { loads++; return Header(); }));
            TestAssert.Equal(0, loads);
        }
        var cache = new LocalExecutableHeaderCache();
        cache.GetOrRead(0x140000000, Header);
        int changedLoads = 0;
        TestAssert.Throws<InvalidOperationException>(() => cache.GetOrRead(0x150000000, () => { changedLoads++; return Header(); }));
        TestAssert.Equal(0, changedLoads);
    }

    public static void FailedOrMalformedFileHeaderCanRetryWithoutCachingAnInvalidResult() {
        var cache = new LocalExecutableHeaderCache();
        TestAssert.Throws<IOException>(() => cache.GetOrRead(0x140000000, () => throw new IOException("Transient file failure")));
        TestAssert.Throws<IOException>(() => cache.GetOrRead(0x140000000, () => new byte[60]));
        TestAssert.Throws<ArgumentNullException>(() => cache.GetOrRead(0x140000000, null!));
        var result = cache.GetOrRead(0x140000000, Header);
        TestAssert.Equal((byte)'M', result[0]);
        // A replacement connection owns a new cache and reads its own module independently
        TestAssert.Equal((byte)'Z', new LocalExecutableHeaderCache().GetOrRead(0x150000000, Header)[1]);
    }
}
