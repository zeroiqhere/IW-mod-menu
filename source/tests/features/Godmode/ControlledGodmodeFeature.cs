using syndishanx_iw_tool.Features.Godmode;

namespace Features.Tests.Godmode;

// Only lifecycle failures need a controllable adapter instead of real flag bytes
internal sealed class ControlledGodmodeFeature : IPlayerGodmodeFeature {
    public bool Connected { get; set; } = true;
    public bool Active { get; set; }
    public bool HasPendingRestore { get; set; }
    public string? FailureReason { get; set; }
    public int Sets { get; private set; }
    public int Closes { get; private set; }
    public Func<bool, CancellationToken, Task<bool>>? OnSet { get; set; }
    public Func<Task<GodmodeCloseResult>>? OnClose { get; set; }

    public GodmodePreflightResult Preflight(bool desired) => new(true,
        Active ? GodmodeValue.On : HasPendingRestore ? GodmodeValue.Unknown : GodmodeValue.Off);

    public async Task<bool> SetAsync(bool desired, CancellationToken cancellationToken = default) {
        Sets++;
        if (OnSet is not null) {
            return await OnSet(desired, cancellationToken);
        }
        Active = desired;
        HasPendingRestore = desired;
        return true;
    }

    public async Task<GodmodeCloseResult> CloseAsync() {
        Closes++;
        if (OnClose is not null) {
            return await OnClose();
        }
        Active = false;
        HasPendingRestore = false;
        return new(true, true);
    }
}
