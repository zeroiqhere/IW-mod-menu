using syndishanx_iw_tool.Players.LocalObservation;

namespace Apparition.Backend.Session;

internal sealed partial class RetailBackendSession {
    // The private binding stays inside the session; only an opaque owner token reaches the controller
    private sealed record LocalAdmissionState(RetailConnection Owner,
        LocalPlayerObservationReceipt Receipt, LocalMenuAdmissionSnapshot Snapshot,
        LocalMenuPreferenceScope PreferenceScope);
    private LocalAdmissionState? localMenuAdmission;
    private ulong localMenuAdmissionRevision;

    public LocalMenuAdmissionSnapshot? ReadLocalMenuAdmission() {
        var state = Volatile.Read(ref localMenuAdmission);
        // A cached read never asks a transport whether its process or actor still exists
        return state is not null && CanCommit(state.Owner) && state.Owner.HasConfirmedConnection
            && ReferenceEquals(state, Volatile.Read(ref localMenuAdmission)) ? state.Snapshot : null;
    }

    private void PublishLocalMenuAdmission(RetailConnection owner, LocalPlayerObservationReceipt? receipt) {
        var observed = receipt?.Observation;
        if (!owner.HasConfirmedConnection || observed is null || !observed.IsSupported() || observed.ProcessId != processId
            || observed.Identity is null || observed.Life == LocalPlayerLife.Unknown || observed.HasMenu is null) {
            Volatile.Write(ref localMenuAdmission, null);
            return;
        }
        var previous = Volatile.Read(ref localMenuAdmission);
        bool sameActor = previous is not null && ReferenceEquals(previous.Owner, owner)
            && previous.Receipt.Binding == receipt!.Binding;
        bool sameScope = sameActor && previous!.Snapshot.Observation.Life == observed.Life
            && previous.Snapshot.Observation.HasMenu == observed.HasMenu;
        ulong revision;
        if (sameScope) revision = previous!.Snapshot.Revision;
        else {
            // Exhaustion revokes proof instead of recycling an earlier command epoch
            if (localMenuAdmissionRevision == ulong.MaxValue) {
                Volatile.Write(ref localMenuAdmission, null);
                return;
            }
            revision = ++localMenuAdmissionRevision;
        }
        object token = sameActor ? previous!.Snapshot.OwnerToken : new object();
        // Life and permission epochs do not replace the account owner for the same complete binding
        var preferenceScope = sameActor ? previous!.PreferenceScope
            : new LocalMenuPreferenceScope(receipt!.Binding.AccountId, token);
        Volatile.Write(ref localMenuAdmission, new(owner, receipt!, new(revision, observed, token), preferenceScope));
    }

    public Task<LocalMenuAdmissionSnapshot?> ValidateLocalMenuOpeningAsync(ulong expectedRevision,
        CancellationToken cancellationToken = default) {
        var expected = CaptureLocalMenuAdmission(expectedRevision, cancellationToken);
        long revision = Volatile.Read(ref admissionRevision);
        return expected is null ? Task.FromResult<LocalMenuAdmissionSnapshot?>(null)
            : Task.Run(() => ValidateLocalMenuOpeningOnWorkerAsync(expected, revision, cancellationToken));
    }

    // Owned scheduling tests retain the actual outer continuation without replacing source reads
    internal Task<LocalMenuAdmissionSnapshot?> ValidateLocalMenuOpeningOnWorkerAsync(ulong expectedRevision,
        CancellationToken cancellationToken = default) {
        var expected = CaptureLocalMenuAdmission(expectedRevision, cancellationToken);
        return expected is null ? Task.FromResult<LocalMenuAdmissionSnapshot?>(null)
            : ValidateLocalMenuOpeningOnWorkerAsync(expected, Volatile.Read(ref admissionRevision), cancellationToken);
    }

    private LocalAdmissionState? CaptureLocalMenuAdmission(ulong revision, CancellationToken token) {
        var state = Volatile.Read(ref localMenuAdmission);
        return !token.IsCancellationRequested && revision != 0 && state?.Snapshot.Revision == revision
            && CanCommit(state.Owner) && state.Owner.HasConfirmedConnection ? state : null;
    }

    private bool MatchesLocalMenuAdmission(LocalAdmissionState expected, long revision) {
        var current = Volatile.Read(ref localMenuAdmission);
        return revision == Volatile.Read(ref admissionRevision) && CanCommit(expected.Owner)
            && expected.Owner.HasConfirmedConnection
            && current is not null && ReferenceEquals(current.Owner, expected.Owner)
            && current.Snapshot.Revision == expected.Snapshot.Revision
            && current.Receipt.Binding == expected.Receipt.Binding;
    }

    private async Task<LocalMenuAdmissionSnapshot?> ValidateLocalMenuOpeningOnWorkerAsync(
        LocalAdmissionState expected, long revision, CancellationToken callerToken) {
        try {
            // Any earlier polling read must finish before this command starts its own new source read
            while (true) {
                Task read;
                bool fresh;
                await lifecycleGate.WaitAsync(callerToken);
                try {
                    if (!MatchesLocalMenuAdmission(expected, revision)) return null;
                    var prior = expected.Owner.ObservationRead;
                    fresh = prior is null || prior.IsCompleted;
                    read = fresh ? StartLocalObservationRead(expected.Owner) : prior!;
                } finally { lifecycleGate.Release(); }
                // Caller cancellation drops admission, while the owner still retains and joins the source task
                await read.WaitAsync(callerToken);
                if (!fresh) continue;
                await lifecycleGate.WaitAsync(callerToken);
                try {
                    return MatchesLocalMenuAdmission(expected, revision)
                        ? Volatile.Read(ref localMenuAdmission)!.Snapshot : null;
                } finally { lifecycleGate.Release(); }
            }
        } catch (OperationCanceledException) {
            return null;
        }
    }
}
