using syndishanx_iw_tool.Features.Godmode;

namespace Apparition.Backend.Session;

// Shared personal and peer owners release only after every worker and flag is confirmed
internal sealed class ConnectionGodmodeCleanup(RetailConnection? owner) {
    private GodmodeRegistryCloseResult? outcome;

    public bool RestoreConfirmed => owner is null || outcome?.Restored == true;

    public async Task StopAsync() {
        if (owner is null) return;
        outcome = await owner.CloseGodmodeOwnersAsync().ConfigureAwait(false);
        if (!outcome.SafeToRelease)
            throw new IOException("Godmode cleanup is unconfirmed; the original transport is retained");
    }
}
