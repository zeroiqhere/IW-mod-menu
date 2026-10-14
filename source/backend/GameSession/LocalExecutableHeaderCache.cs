using System.IO;

namespace syndishanx_iw_tool.GameSession;

// A file already fingerprinted by the owner is loaded once for one immutable module
internal sealed class LocalExecutableHeaderCache {
    private long retainedModule;
    private byte[]? header;

    internal void Clear() {
        // Retiring the connection also retires its file fingerprint and copied header
        header = null;
        retainedModule = 0;
    }

    internal byte[] GetOrRead(long moduleBase, Func<byte[]> readVerifiedHeader) {
        ArgumentNullException.ThrowIfNull(readVerifiedHeader);
        const long maximumAddress = 0x7FFFFFFF0000;
        if (moduleBase < 0x10000 || moduleBase > maximumAddress - 4095 || moduleBase % 8 != 0) {
            throw new InvalidOperationException("The local observation module span is unsupported");
        }
        if (header is not null) {
            if (moduleBase != retainedModule) throw new InvalidOperationException("The original game module changed");
            // Every caller owns its copy, including retries after a failed native handle read
            return (byte[])header.Clone();
        }
        byte[] supplied = readVerifiedHeader();
        ArgumentNullException.ThrowIfNull(supplied);
        var copied = (byte[])supplied.Clone();
        // No failed or incomplete header becomes a cached successful file validation
        LocalExecutableHeader.Verify(copied, copied);
        retainedModule = moduleBase;
        header = copied;
        return (byte[])copied.Clone();
    }
}
