using syndishanx_iw_tool.Features.Godmode;

namespace Session.Tests.Session.Cohort;

// Only the cached monitor field is controlled; the real feature keeps flag and cleanup ownership
internal sealed class CachedStateGodmodeFeature(FlagsPlayerGodmodeFeature feature) : IGodmodeTransitionFeature {
    internal bool ForceInactive;
    public bool Connected => feature.Connected;
    public bool Active => !ForceInactive && feature.Active;
    public bool HasPendingRestore => feature.HasPendingRestore;
    public string? FailureReason => feature.FailureReason;
    public GodmodePreflightResult Preflight(bool desired) => feature.Preflight(desired);
    public Task<bool> SetAsync(bool desired, CancellationToken token = default) => feature.SetAsync(desired, token);
    public Task<GodmodeTransitionResult> SetWithTransitionAsync(bool desired, CancellationToken token = default) =>
        feature.SetWithTransitionAsync(desired, token);
    public Task<GodmodeCloseResult> CloseAsync() => feature.CloseAsync();
}
