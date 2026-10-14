using syndishanx_iw_tool.Features.Godmode;

namespace Apparition.Backend.Session;

internal sealed partial class RetailConnection {
    private readonly SemaphoreSlim godmodeCloseGate = new(1, 1);

    internal async Task<GodmodeRegistryCloseResult> CloseGodmodeOwnersAsync() {
        // Disarm both routes before waiting for selection, registry, or local factory work
        RequestNewWriteClose();
        LocalGodmode?.RequestClose();
        Teammates.RequestClose();
        CohortGodmode.RequestClose();
        await godmodeCloseGate.WaitAsync().ConfigureAwait(false);
        try {
            // Source proofs can still hold the transport before entering the shared writer gate
            await CohortGodmode.StopAsync().ConfigureAwait(false);
            await Teammates.CloseAsync().ConfigureAwait(false);
            var closed = await GodmodeOwners.CloseAsync().ConfigureAwait(false);
            if (LocalGodmode is not null && !GodmodeOwners.OwnsFeature(LocalGodmode)) {
                // Failed read-only registration can still retain a local adapter outside the registry
                var personal = await LocalGodmode.CloseAsync().ConfigureAwait(false);
                closed = closed with {
                    Stopped = closed.Stopped && personal.Stopped,
                    Restored = closed.Restored && personal.Restored
                };
            }
            return closed;
        } finally { godmodeCloseGate.Release(); }
    }
}
