namespace syndishanx_iw_tool.Players.LocalObservation;

// This adapter borrows an existing connection and never opens or releases a process
public sealed class OwnedLocalPlayerObservationProvider : ILocalPlayerObservationReceiptProvider {
    private readonly int processId;
    private readonly ILocalActorSnapshotSource source;
    private readonly LocalMenuAccessState access = new();

    public OwnedLocalPlayerObservationProvider(int processId, ILocalActorSnapshotSource source) {
        if (processId <= 0) throw new ArgumentOutOfRangeException(nameof(processId));
        ArgumentNullException.ThrowIfNull(source);
        this.processId = processId;
        this.source = source;
    }

    public Task<LocalPlayerObservationReceipt?> ReadReceiptAsync(CancellationToken cancellationToken) =>
        Task.FromResult(ReadCopiedActor(cancellationToken).Receipt);

    public Task<LocalPlayerObservation?> ReadAsync(CancellationToken cancellationToken) =>
        Task.FromResult<LocalPlayerObservation?>(ReadCopiedActor(cancellationToken).Observation);

    private (LocalPlayerObservation Observation, LocalPlayerObservationReceipt? Receipt) ReadCopiedActor(CancellationToken token) {
        // Both APIs consume one copied actor read, never separately sampled account and player views
        token.ThrowIfCancellationRequested();
        try {
            if (!MatchesConnection()) return Unavailable("The original connection is unavailable");
            token.ThrowIfCancellationRequested();
            var snapshot = source.ReadLocalActorSnapshot(token);
            token.ThrowIfCancellationRequested();
            if (!MatchesConnection()) return Unavailable("The original connection changed during the read");
            if (!LocalObservationBindingShape.IsSupported(snapshot.Binding)) {
                return Unavailable("The local actor binding has an unsupported shape");
            }
            // Permission remains owned by this provider and resets on complete binding replacement
            var observed = new LocalPlayerObservation(processId, snapshot.Binding.Identity, snapshot.Life, null, null,
                snapshot.Life == LocalPlayerLife.Unknown ? "The local actor life state is unsupported" : "The local actor is verified",
                snapshot.IsSessionHost);
            token.ThrowIfCancellationRequested();
            observed = observed with { HasMenu = access.Observe(snapshot) };
            return (observed, new(snapshot.Binding, observed));
        } catch (Exception exception) when (exception is IOException or InvalidOperationException
            or ArgumentException or System.ComponentModel.Win32Exception) {
            // An incomplete read cannot preserve an earlier successful receipt
            return Unavailable("The local actor could not be confirmed");
        }
    }

    private static (LocalPlayerObservation, LocalPlayerObservationReceipt?) Unavailable(string status) =>
        (LocalPlayerObservation.Unknown(status), null);

    private bool MatchesConnection() => source.Connected && source.ProcessId == processId;
}
