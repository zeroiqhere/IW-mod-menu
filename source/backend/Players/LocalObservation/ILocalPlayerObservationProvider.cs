namespace syndishanx_iw_tool.Players.LocalObservation;

// The session retains the source until the returned read task and cancellation work have ended
// Providers borrow that source and must not start detached work or release the transport
public interface ILocalPlayerObservationProvider {
    Task<LocalPlayerObservation?> ReadAsync(CancellationToken cancellationToken);
}
