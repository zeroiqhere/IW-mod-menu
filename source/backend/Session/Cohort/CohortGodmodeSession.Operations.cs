using syndishanx_iw_tool.Features.Godmode;
using syndishanx_iw_tool.Overlay.Players;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Apparition.Backend.Session;

// The service admits a private target while the registry remains the only flag-change owner
public sealed partial class CohortGodmodeSession {
    private async Task<GodmodeOperationResult> ExecuteAsync(bool toggle, CohortGodmodeTarget? expected,
        CancellationToken token) {
        // An unrelated caller cannot retire the currently valid source receipt
        if (toggle && (expected is null || !ReferenceEquals(expected, target)
            || !ReferenceEquals(expected.OwnerToken, ownerToken)))
            return Rejected("The admitted cohort menu target is stale");
        try {
            token.ThrowIfCancellationRequested();
            Publication cached = Volatile.Read(ref publication);
            bool currentStamp = target is not null && cached.Metadata is not null
                && cached.Metadata.Matches(registry.ReadMetadataStamp(target.Capture.Identities));
            if (toggle && !currentStamp)
                throw new IOException("The admitted cohort menu target is stale");
            CohortGodmodeTarget? continued = target;
            Proof proof = ReadProof(continued?.Binding, continued, token);
            bool requireAlive = toggle && cached.View.Value != GodmodeValue.On;
            if (requireAlive && proof.Actor.Life != LocalPlayerLife.Alive)
                throw new IOException("The authenticated local actor cannot admit a new ON");
            if (continued is null || !currentStamp) {
                // A shared owner operation creates a new intent, not a graft onto an old display receipt
                target = new(ownerToken, proof.Capture, proof.Actor.Binding);
            }
            CohortGodmodeTarget admitted = target!;
            GodmodeOperationResult result;
            if (toggle) {
                result = await registry.ToggleCohortAsync(admitted.Capture,
                    () => {
                        Proof current = ReadProof(admitted.Binding, admitted, token);
                        if (requireAlive && current.Actor.Life != LocalPlayerLife.Alive)
                            throw new IOException("The authenticated local actor cannot admit a new ON");
                        return current.Observation;
                    }, token,
                    () => AdmitRollbackEnable(admitted.Binding)).ConfigureAwait(false);
            } else {
                result = await registry.CaptureAsync(proof.Capture.Identities, token).ConfigureAwait(false);
            }
            // Completion may have awaited another adapter while host or membership changed
            Proof completed = ReadProof(admitted.Binding, admitted, token);
            token.ThrowIfCancellationRequested();
            if (!result.Succeeded || result.OwnerMetadata is null
                || !result.OwnerMetadata.Matches(registry.ReadMetadataStamp(admitted.Capture.Identities)))
                throw new IOException(result.FailureReason ?? "The shared owner receipt changed before publication");
            if (!ReceiptAgreesWithSnapshot(result) || result.Snapshot.Closed || result.Snapshot.Members.Count != admitted.Capture.Identities.Count
                || !result.Snapshot.Members.Select(member => member.Identity).SequenceEqual(admitted.Capture.Identities)
                || result.Snapshot.Value is not (GodmodeValue.On or GodmodeValue.Off or GodmodeValue.Mixed))
                throw new IOException("The current cohort flags could not be confirmed");
            bool allOn = result.Snapshot.Value == GodmodeValue.On;
            bool eligible = completed.Observation.Slots.All(slot => slot.Player is null || slot.Player.IsEnableEligible);
            bool canToggle = !result.Snapshot.HasPendingCleanup
                && (allOn || completed.Actor.Life == LocalPlayerLife.Alive && eligible);
            lock (workGate) {
                if (Volatile.Read(ref closing) != 0 || token.IsCancellationRequested)
                    throw new OperationCanceledException(token);
                Volatile.Write(ref publication, new(new(result.Snapshot.Value, canToggle,
                    canToggle ? "The current host and complete lobby are verified"
                        : "The retained cohort can be observed but cannot enable God Mode", admitted), result.OwnerMetadata));
            }
            return result;
        } catch (Exception error) {
            // Proof failure retires the private menu intent, not the shared restoration owners
            target = null;
            Volatile.Write(ref publication, new(Unavailable(error.Message), null));
            return Rejected(error.Message, token.IsCancellationRequested);
        }
    }
}
