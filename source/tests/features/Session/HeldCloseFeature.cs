using syndishanx_iw_tool.Features.Godmode;

namespace Features.Tests.Session;

// Hold only the cleanup boundary while the real feature supplies flags and ownership
internal sealed class HeldCloseFeature(IPlayerGodmodeFeature inner) : IPlayerGodmodeFeature {
    public TaskCompletionSource Entered { get; } = TestAssert.Barrier();
    public TaskCompletionSource Release { get; } = TestAssert.Barrier();
    public bool Connected => inner.Connected;
    public bool Active => inner.Active;
    public bool HasPendingRestore => inner.HasPendingRestore;
    public string? FailureReason => inner.FailureReason;
    public GodmodePreflightResult Preflight(bool desired) => inner.Preflight(desired);
    public Task<bool> SetAsync(bool desired, CancellationToken cancellationToken = default) =>
        inner.SetAsync(desired, cancellationToken);
    public async Task<GodmodeCloseResult> CloseAsync() {
        // No synthetic delay changes the real flag or claims that a worker has already stopped
        Entered.TrySetResult();
        await Release.Task.ConfigureAwait(false);
        return await inner.CloseAsync().ConfigureAwait(false);
    }
}
