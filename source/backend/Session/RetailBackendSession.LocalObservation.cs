using syndishanx_iw_tool.Features.Godmode;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Apparition.Backend.Session;

internal sealed partial class RetailBackendSession {
    public LocalPlayerObservation ReadLocalObservation() {
        var owner = Volatile.Read(ref connection);
        if (owner is null || !owner.HasConfirmedConnection || closing || cleanupBlocked || restartRequired) return LocalPlayerObservation.BindingUnavailable;
        // Cached views never invoke a provider or inspect the native transport
        var observed = Volatile.Read(ref owner.Observation);
        return ReferenceEquals(owner, Volatile.Read(ref connection)) && owner.HasConfirmedConnection && !closing && !cleanupBlocked && !restartRequired
            ? observed : LocalPlayerObservation.BindingUnavailable;
    }

    public Task RefreshLocalObservationAsync(CancellationToken cancellationToken = default) {
        // A connected owner needs liveness sampling even when no actor provider is configured
        long revision = Volatile.Read(ref admissionRevision);
        bool admitted = !closing;
        // Even a synchronous factory or provider remains on this retained worker
        return Task.Run(() => RefreshLocalObservationOnWorkerAsync(revision, admitted, cancellationToken));
    }

    private async Task RefreshLocalObservationOnWorkerAsync(long revision, bool admitted, CancellationToken callerToken) {
        Task? read = null;
        RetailConnection? owner = null;
        await lifecycleGate.WaitAsync(callerToken).ConfigureAwait(false);
        try {
            if (!admitted || revision != Volatile.Read(ref admissionRevision) || closing || cleanupBlocked || restartRequired) return;
            owner = Volatile.Read(ref connection);
            if (owner is null) return;
            if (owner.ObservationRead is null || owner.ObservationRead.IsCompleted) {
                _ = StartLocalObservationRead(owner);
            }
            read = owner.ObservationRead;
        } catch (Exception exception) when (exception is not OperationCanceledException || !callerToken.IsCancellationRequested) {
            if (owner is not null && CanCommit(owner)) {
                Volatile.Write(ref owner.Observation, LocalPlayerObservation.Unknown("The original observation source is unavailable"));
                PublishLocalMenuAdmission(owner, null);
            }
        } finally { lifecycleGate.Release(); }
        // Caller cancellation abandons only this wait, never another caller's shared source
        if (read is not null) await read.WaitAsync(callerToken).ConfigureAwait(false);
    }

    private Task StartLocalObservationRead(RetailConnection owner) {
        var ticket = new object();
        owner.ObservationTicket = ticket;
        // The gate publishes the retained task before its worker can commit a copied result
        return owner.ObservationRead = Task.Run(() => ReadLocalObservationOnWorkerAsync(owner, ticket));
    }

    private async Task ReadLocalObservationOnWorkerAsync(RetailConnection owner, object ticket) {
        LocalPlayerObservation observed;
        LocalPlayerObservationReceipt? receipt = null;
        var token = owner.ObservationCancellation.Token;
        try {
            token.ThrowIfCancellationRequested();
            if (!owner.ObserveOriginalConnection()) {
                observed = LocalPlayerObservation.Unknown("The original connection is unavailable");
            } else if (observationProviderFactory is null) {
                observed = LocalPlayerObservation.BindingUnavailable;
            } else {
                // Source queries may wait on native locks, so the full query also stays off the commit gate
                token.ThrowIfCancellationRequested();
                var provider = owner.ReadObservationProvider();
                token.ThrowIfCancellationRequested();
                LocalPlayerObservation? supplied;
                if (provider is ILocalPlayerObservationReceiptProvider proof) {
                    receipt = await proof.ReadReceiptAsync(token).ConfigureAwait(false);
                    supplied = receipt?.Observation;
                } else {
                    // Legacy display providers cannot authorize a new menu opening
                    supplied = provider is null ? null : await provider.ReadAsync(token).ConfigureAwait(false);
                }
                token.ThrowIfCancellationRequested();
                // A with-copy or an unrelated process report cannot bypass shape and connection checks
                observed = supplied is null ? LocalPlayerObservation.BindingUnavailable
                    : supplied.IsSupported() && (supplied.ProcessId == processId || supplied.ProcessId == 0 && supplied.Identity is null)
                        ? supplied : LocalPlayerObservation.Unknown("The supplied local observation could not be confirmed");
                if (!owner.ObserveOriginalConnection()) {
                    observed = LocalPlayerObservation.Unknown("The original connection is unavailable");
                    receipt = null;
                }
            }
        } catch (OperationCanceledException) when (token.IsCancellationRequested) {
            observed = LocalPlayerObservation.Unknown("The local observation was stopped");
            receipt = null;
        } catch (Exception) {
            // Read failures revoke old facts without faulting the backend's normal polling loop
            observed = LocalPlayerObservation.Unknown("The local observation could not be confirmed");
            receipt = null;
        }

        try {
            // Recovery only captures a matching OFF capability; enabling remains an explicit action
            if (receipt is not null && observed.ProcessId == processId
                && observed.Life == LocalPlayerLife.Alive && observed.HasMenu == true
                && CanCommit(owner) && owner.HasConfirmedConnection
                && owner.LocalGodmode is { } personal
                && personal.ReadView() is { HasPendingCleanup: false,
                    Value: GodmodeValue.Unknown or GodmodeValue.Off } personalView
                // A captured adapter still needs its shared registry entry before the menu can use it
                && (!personalView.CanToggle || !owner.GodmodeOwners.OwnsFeature(personal))) {
                await owner.RecoverPersonalGodmodeAsync(receipt.Binding, observationRecoveryClock, token).ConfigureAwait(false);
            }
            // Group polling shares this retained task, so shutdown joins every source before release
            if (Volatile.Read(ref cohortObservationWanted) && owner.HasConfirmedConnection && CanCommit(owner))
                await owner.CohortGodmode.RefreshAsync(token).ConfigureAwait(false);
            // Peer checks stay on the same retained worker even without a local observation provider
            if ((owner.Teammates.HasSelection || Volatile.Read(ref cohortObservationWanted))
                && owner.HasConfirmedConnection && CanCommit(owner))
                await owner.Teammates.RefreshAsync(token).ConfigureAwait(false);
        } catch (OperationCanceledException) when (token.IsCancellationRequested) {
            // Closing the source revokes facts without faulting the normal polling owner
            // Caller cancellation still affects only the caller's WaitAsync above
            observed = LocalPlayerObservation.Unknown("The local observation was stopped");
            receipt = null;
        }

        // Actor and group reads may cross process loss after the first native sample
        if (CanCommit(owner) && !owner.ObserveOriginalConnection()) {
            observed = LocalPlayerObservation.Unknown("The original connection is unavailable");
            receipt = null;
        }

        await lifecycleGate.WaitAsync().ConfigureAwait(false);
        try {
            if (CanCommit(owner) && ReferenceEquals(owner.ObservationTicket, ticket)) {
                Volatile.Write(ref owner.Observation, observed);
                PublishLocalMenuAdmission(owner, receipt);
            }
        } finally { lifecycleGate.Release(); }
    }

    private async Task StopLocalObservationAsync(RetailConnection? owner) {
        if (owner is null) return;
        // Cancellation callbacks may use the source too, so their full task is also retained
        owner.ObservationCancellationWork ??= owner.ObservationCancellation.CancelAsync();
        var reads = Task.WhenAll(owner.ObservationRead ?? Task.CompletedTask, owner.ObservationCancellationWork);
        try { await reads.WaitAsync(observationStopTimeout).ConfigureAwait(false); }
        catch (TimeoutException) {
            throw new IOException("Local observation is still running; its original source remains retained for cleanup retry");
        } catch (Exception) when (reads.IsCompleted) {
            // A finished callback failure is observed; no task can still be using the source
        }
    }
}
