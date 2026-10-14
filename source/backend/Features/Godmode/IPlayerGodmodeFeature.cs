namespace syndishanx_iw_tool.Features.Godmode;

// A bound adapter owns the only writer and worker for one full player identity
public interface IPlayerGodmodeFeature {
    bool Connected { get; }
    bool Active { get; }
    bool HasPendingRestore { get; }
    string? FailureReason { get; }
    GodmodePreflightResult Preflight(bool desired);
    Task<bool> SetAsync(bool desired, CancellationToken cancellationToken = default);
    Task<GodmodeCloseResult> CloseAsync();
}

// Preflight reads eligibility without starting a worker or changing player flags
public sealed record GodmodePreflightResult(bool Succeeded, GodmodeValue Value, string? FailureReason = null);

// Worker completion and confirmed restoration are separate cleanup facts
public sealed record GodmodeCloseResult(bool Stopped, bool Restored, string? FailureReason = null) {
    public bool SafeToRelease => Stopped && Restored;
}
