using syndishanx_iw_tool.Features.Godmode;

namespace Features.Tests.Session;

// Delegate real flag behavior while a cached adapter property can fail
internal sealed class FailingMetadataFeature(IPlayerGodmodeFeature inner) : IPlayerGodmodeFeature {
    public bool FailActive { get; set; }
    public bool Connected => inner.Connected;
    public bool Active => FailActive ? throw new IOException("The cached adapter state is unavailable") : inner.Active;
    public bool HasPendingRestore => inner.HasPendingRestore;
    public string? FailureReason => inner.FailureReason;
    public GodmodePreflightResult Preflight(bool desired) => inner.Preflight(desired);
    public Task<bool> SetAsync(bool desired, CancellationToken cancellationToken = default) =>
        inner.SetAsync(desired, cancellationToken);
    public Task<GodmodeCloseResult> CloseAsync() => inner.CloseAsync();
}
