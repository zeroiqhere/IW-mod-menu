using syndishanx_iw_tool.Players.LocalBinding;

namespace Apparition.Backend.Session;

internal sealed partial class RetailConnection {
    private long? personalRecoveryAttemptTimestamp;
    // Failed captures retry at most four times per second on a normally advancing clock
    private static readonly TimeSpan PersonalRecoveryRetry = TimeSpan.FromMilliseconds(250);

    internal Task<bool> RecoverPersonalGodmodeAsync(LocalAccountSessionBinding observedBinding,
        TimeProvider clock, CancellationToken cancellationToken) {
        var personal = LocalGodmode;
        if (personal is null || cancellationToken.IsCancellationRequested) return Task.FromResult(false);
        long current = clock.GetTimestamp();
        if (personalRecoveryAttemptTimestamp is { } previous && current >= previous
            && clock.GetElapsedTime(previous, current) < PersonalRecoveryRetry) return Task.FromResult(false);
        return GodmodeOwners.CapturePreparedAsync(new[] { observedBinding.Identity }, () => {
            // Only an admitted attempt consumes the retained connection's read-only retry deadline
            // A clock rollback replaces the old deadline instead of waiting for an obsolete future time
            personalRecoveryAttemptTimestamp = current;
            return personal.CaptureAsync(cancellationToken, observedBinding);
        }, waitForAvailable: false, cancellationToken);
    }
}
