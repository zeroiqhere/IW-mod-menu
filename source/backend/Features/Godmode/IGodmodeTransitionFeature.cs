namespace syndishanx_iw_tool.Features.Godmode;

// Rollback may restore ON only when this operation proved its own flag transition
public interface IGodmodeTransitionFeature : IPlayerGodmodeFeature {
    Task<GodmodeTransitionResult> SetWithTransitionAsync(bool desired,
        CancellationToken cancellationToken = default);
}

public sealed record GodmodeTransitionResult(bool Succeeded, bool ConfirmedOwnedWrite);
