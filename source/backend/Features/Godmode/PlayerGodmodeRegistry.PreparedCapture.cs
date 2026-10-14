using syndishanx_iw_tool.Players;

namespace syndishanx_iw_tool.Features.Godmode;

public sealed partial class PlayerGodmodeRegistry {
    // Preparation checks the retained local owner only after shared action admission
    internal async Task<bool> CapturePreparedAsync(IReadOnlyList<PlayerIdentity> capturedIdentities,
        Func<Task<bool>> prepare, bool waitForAvailable, CancellationToken cancellationToken) {
        PlayerIdentity[] captured = CapturedGodmodeRoster.Copy(capturedIdentities);
        if (!CapturedGodmodeRoster.Valid(captured)) return false;
        bool entered = false;
        try {
            if (waitForAvailable) {
                // Explicit refresh waits for a current proof instead of queuing an earlier local receipt
                await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                entered = true;
            } else {
                entered = await gate.WaitAsync(0, cancellationToken).ConfigureAwait(false);
            }
            // A busy rejection has no source reads, snapshot or queued recovery receipt
            if (!entered || IsClosed()) return false;
            BeginMetadataOperation(captured);
            if (!await prepare().ConfigureAwait(false)) return false;
            // A cancelled preparation must not start the next native validation phase
            cancellationToken.ThrowIfCancellationRequested();
            return CaptureLocked(captured, cancellationToken).Succeeded;
        } catch (Exception) {
            // Cancellation and failed proof keep the original local cleanup owner
            return false;
        } finally {
            if (entered) {
                Volatile.Write(ref operating, Array.Empty<PlayerIdentity>());
                gate.Release();
            }
        }
    }
}
