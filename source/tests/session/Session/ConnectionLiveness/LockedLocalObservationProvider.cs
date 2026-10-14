using syndishanx_iw_tool.Players.LocalObservation;

namespace Session.Tests;

// Owned fixture permission facts accompany the same locked actor read as the private binding
internal sealed class LockedLocalObservationProvider(LockedRetailTransport transport) : ILocalPlayerObservationReceiptProvider {
    public Task<LocalPlayerObservationReceipt?> ReadReceiptAsync(CancellationToken token) {
        var actor = transport.ReadLocalActorFlagsSnapshot(token);
        var observed = new LocalPlayerObservation(4242, actor.Binding.Identity, LocalPlayerLife.Alive,
            true, false, "Owned test actor is permitted");
        return Task.FromResult<LocalPlayerObservationReceipt?>(new(actor.Binding, observed));
    }

    public async Task<LocalPlayerObservation?> ReadAsync(CancellationToken token) =>
        (await ReadReceiptAsync(token))?.Observation;
}
