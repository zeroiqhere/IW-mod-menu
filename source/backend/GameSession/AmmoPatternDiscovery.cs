using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using syndishanx_iw_tool.Features;

namespace syndishanx_iw_tool.GameSession;

// Discovery only reads state and never adopts an existing patch
internal static class AmmoPatternDiscovery {
    private const string OriginalPattern = "01 7C 90 14 48 8B 4C 24 60";
    private const string PatchedPattern = "90 90 90 90 48 8B 4C 24 60";

    public static async Task<IReadOnlyList<long>> FindOriginalAsync(
        Func<string, Task<IEnumerable<long>>> scan, CancellationToken token) {
        ArgumentNullException.ThrowIfNull(scan);
        token.ThrowIfCancellationRequested();

        // Keep ordinary discovery unchanged when the original instructions are present
        long[] original = (await scan(OriginalPattern).ConfigureAwait(false)).ToArray();
        token.ThrowIfCancellationRequested();
        if (original.Length != 0) {
            return original;
        }

        // The full known pattern can explain a failure without authorizing restoration
        long[] patched = (await scan(PatchedPattern).ConfigureAwait(false)).ToArray();
        token.ThrowIfCancellationRequested();
        if (patched.Length != 0) {
            throw new ExistingAmmoPatchException();
        }

        // Unknown and unsupported states remain subject to normal candidate validation
        return Array.Empty<long>();
    }
}
