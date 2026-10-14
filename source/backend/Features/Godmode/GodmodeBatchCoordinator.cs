using syndishanx_iw_tool.Players;

namespace syndishanx_iw_tool.Features.Godmode;

internal sealed record GodmodeBatchMember(PlayerIdentity Identity, IPlayerGodmodeFeature Feature,
    GodmodeValue Previous);

internal sealed record GodmodeBatchOutcome(bool Succeeded, bool Cancelled, string? FailureReason,
    GodmodeRegistrySnapshot? Snapshot = null);

// A captured batch changes actual member state and owns only its own rollback
internal static class GodmodeBatchCoordinator {
    internal static async Task<GodmodeBatchOutcome> ApplyAsync(IReadOnlyList<GodmodeBatchMember> plan,
        bool desired, Action validateRoster, Func<PlayerIdentity, bool> validateIdentity,
        Action<GodmodeBatchMember, bool, string?> update, Func<GodmodeRegistrySnapshot> complete,
        CancellationToken cancellationToken, Func<bool>? admitRollbackEnable = null) {
        var touched = new List<(GodmodeBatchMember Member, bool OwnedWrite)>();
        string? failure;
        try {
            foreach (GodmodeBatchMember member in plan) {
                cancellationToken.ThrowIfCancellationRequested();
                validateRoster();
                bool alreadyDesired = desired ? member.Feature.Active
                    : !member.Feature.Active && !member.Feature.HasPendingRestore;
                if (alreadyDesired) {
                    continue;
                }
                // Include a failed-but-applied target in the rollback set
                int touchedIndex = touched.Count;
                touched.Add((member, false));
                GodmodeTransitionResult transition = await SetWithEvidenceAsync(member.Feature, desired,
                    cancellationToken).ConfigureAwait(false);
                touched[touchedIndex] = (member, transition.ConfirmedOwnedWrite);
                bool changed = transition.Succeeded;
                bool confirmed = desired ? member.Feature.Active && member.Feature.Connected
                    : !member.Feature.Active && !member.Feature.HasPendingRestore;
                update(member, confirmed, changed ? null : member.Feature.FailureReason);
                if (!changed || !confirmed) {
                    throw new InvalidOperationException(member.Feature.FailureReason ?? "The player change was not confirmed");
                }
                // Cancellation after the final applied write still needs rollback
                cancellationToken.ThrowIfCancellationRequested();
            }
            // The final await may change any captured identity, including an earlier member
            validateRoster();
            GodmodeRegistrySnapshot snapshot = complete();
            // Completion reads may fail, cancel or observe replacement while rollback still owns its writes
            cancellationToken.ThrowIfCancellationRequested();
            validateRoster();
            return new(true, false, null, snapshot);
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            failure = "The operation was cancelled";
        } catch (Exception exception) {
            failure = exception.Message;
        }
        await RollbackAsync(touched, validateIdentity, update, admitRollbackEnable).ConfigureAwait(false);
        return new(false, cancellationToken.IsCancellationRequested, failure);
    }

    private static async Task RollbackAsync(List<(GodmodeBatchMember Member, bool OwnedWrite)> touched,
        Func<PlayerIdentity, bool> validateIdentity, Action<GodmodeBatchMember, bool, string?> update,
        Func<bool>? admitRollbackEnable) {
        for (int index = touched.Count - 1; index >= 0; index--) {
            GodmodeBatchMember member = touched[index].Member;
            bool ownsOffChange = touched[index].OwnedWrite;
            try {
                if (member.Previous == GodmodeValue.Off) {
                    await member.Feature.SetAsync(false, CancellationToken.None).ConfigureAwait(false);
                } else if (member.Previous == GodmodeValue.On && !member.Feature.Active) {
                    if (member.Feature.HasPendingRestore) {
                        GodmodeTransitionResult cleanup = await SetWithEvidenceAsync(member.Feature, false,
                            CancellationToken.None).ConfigureAwait(false);
                        ownsOffChange |= cleanup.ConfirmedOwnedWrite;
                    }
                    // Only a confirmed owned OFF may be reversed into ON
                    if (ownsOffChange && !member.Feature.HasPendingRestore && validateIdentity(member.Identity)
                        && (admitRollbackEnable?.Invoke() ?? true)) {
                        GodmodePreflightResult preflight = member.Feature.Preflight(true);
                        // A fresh host check also follows the native preflight before a new ON
                        if (preflight.Succeeded && (admitRollbackEnable?.Invoke() ?? true)) {
                            await member.Feature.SetAsync(true, CancellationToken.None).ConfigureAwait(false);
                        }
                    }
                } else if (member.Previous == GodmodeValue.Unknown
                    && (member.Feature.Active || member.Feature.HasPendingRestore)) {
                    // An earlier failed change has no confirmed ON state to preserve
                    await member.Feature.SetAsync(false, CancellationToken.None).ConfigureAwait(false);
                }
                GodmodePreflightResult observed = member.Feature.Preflight(false);
                bool confirmed = observed.Succeeded && (member.Feature.Active && observed.Value == GodmodeValue.On
                    || !member.Feature.Active && !member.Feature.HasPendingRestore && observed.Value == GodmodeValue.Off);
                // Confirm current OFF when the game cleared it instead of reapplying a stale ON
                update(member, confirmed, confirmed ? null : "The previous state could not be confirmed after rollback");
            } catch (Exception exception) {
                // Continue restoring the remaining members after any adapter failure
                update(member, false, exception.Message);
            }
        }
    }

    private static async Task<GodmodeTransitionResult> SetWithEvidenceAsync(IPlayerGodmodeFeature feature,
        bool desired, CancellationToken cancellationToken) => feature is IGodmodeTransitionFeature tracked
        ? await tracked.SetWithTransitionAsync(desired, cancellationToken).ConfigureAwait(false)
        : new(await feature.SetAsync(desired, cancellationToken).ConfigureAwait(false), false);
}
