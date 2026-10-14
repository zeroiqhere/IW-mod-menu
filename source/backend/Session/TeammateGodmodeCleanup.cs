using syndishanx_iw_tool.Features.Godmode;

namespace Apparition.Backend.Session;

// The session resource stays available until every original player owner is safe
public sealed class TeammateGodmodeCleanup(TeammateGodmodeSession? session) {
    private GodmodeRegistryCloseResult? outcome;

    public bool RestoreConfirmed => session is null || outcome?.Restored == true;

    public async Task StopAsync() {
        if (session is null) return;
        outcome = await session.CloseAsync().ConfigureAwait(false);
        // A failed restoration keeps the original binding available for another attempt
        if (!outcome.SafeToRelease) {
            throw new InvalidOperationException("Teammate cleanup is unconfirmed; the original transport is retained");
        }
    }
}
