namespace syndishanx_iw_tool.Players.LocalObservation;

// Legacy observations remain useful for display but cannot supply private account admission proof
public interface ILocalPlayerObservationReceiptProvider : ILocalPlayerObservationProvider {
    Task<LocalPlayerObservationReceipt?> ReadReceiptAsync(CancellationToken cancellationToken);
}
