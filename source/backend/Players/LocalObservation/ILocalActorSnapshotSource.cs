namespace syndishanx_iw_tool.Players.LocalObservation;

// The connection owns this source and retains it until its observation worker stops
public interface ILocalActorSnapshotSource {
    bool Connected { get; }
    int ProcessId { get; }
    LocalActorSnapshot ReadLocalActorSnapshot(CancellationToken cancellationToken);
}
