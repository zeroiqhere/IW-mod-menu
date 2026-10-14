using syndishanx_iw_tool.Players;

namespace syndishanx_iw_tool.Features.Godmode;

public sealed partial class PlayerGodmodeRegistry {
    // Source toggle means OFF only when every current participant is already owned ON
    public Task<GodmodeOperationResult> ToggleCohortAsync(PlayerCohortCapture captured,
        Func<PlayerCohortObservation> readCurrent, CancellationToken cancellationToken = default,
        Func<bool>? admitRollbackEnable = null) =>
        ApplyCohortAsync(captured, null, readCurrent, cancellationToken, admitRollbackEnable);

    public Task<GodmodeOperationResult> SetCohortAsync(PlayerCohortCapture captured, bool desired,
        Func<PlayerCohortObservation> readCurrent, CancellationToken cancellationToken = default,
        Func<bool>? admitRollbackEnable = null) =>
        ApplyCohortAsync(captured, desired, readCurrent, cancellationToken, admitRollbackEnable);

    private async Task<bool> DetermineCohortDesiredAsync(PlayerIdentity[] captured, Action validate,
        CancellationToken cancellationToken) {
        bool allOn = true;
        foreach (PlayerIdentity identity in captured) {
            cancellationToken.ThrowIfCancellationRequested();
            validate();
            Entry member = await BindAsync(identity).ConfigureAwait(false);
            // Monitor state can lag a cleared flag, so source all-ON needs a fresh owned sample
            GodmodePreflightResult sampled = member.Feature.Preflight(false);
            bool confirmedOn = member.Feature.Active && sampled.Value == GodmodeValue.On;
            bool confirmedOff = !member.Feature.Active && !member.Feature.HasPendingRestore
                && sampled.Value == GodmodeValue.Off;
            if (!sampled.Succeeded || !confirmedOn && !confirmedOff)
                throw new InvalidOperationException(sampled.FailureReason
                    ?? "The current cohort Godmode state is unconfirmed; restore original owned flags first");
            allOn &= confirmedOn;
        }
        return !allOn;
    }

    private Task<GodmodeOperationResult> ApplyCohortAsync(PlayerCohortCapture captured, bool? desired,
        Func<PlayerCohortObservation> readCurrent, CancellationToken cancellationToken,
        Func<bool>? admitRollbackEnable) {
        ArgumentNullException.ThrowIfNull(captured);
        ArgumentNullException.ThrowIfNull(readCurrent);
        // This contract requires an owned membership source, never display-name discovery
        return ApplyCapturedAsync(captured.Identities, desired, cancellationToken, null,
            new GodmodeCohortGuard(captured, readCurrent), admitRollbackEnable);
    }
}
