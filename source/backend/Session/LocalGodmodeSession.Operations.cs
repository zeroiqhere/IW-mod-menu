using syndishanx_iw_tool.Features;
using syndishanx_iw_tool.Features.Godmode;
using syndishanx_iw_tool.Players.LocalBinding;

namespace Apparition.Backend.Session;

public sealed partial class LocalGodmodeSession {
    // An explicit enable must never enter the toggle's retained OFF branch
    private enum Request { Capture, Toggle, Restore, Enable, Disable }

    public Task<bool> CaptureAsync(CancellationToken cancellationToken = default,
        LocalAccountSessionBinding? expectedBinding = null) =>
        RunSucceededAsync(Request.Capture, cancellationToken, expectedBinding);

    public Task<bool> ToggleAsync(CancellationToken cancellationToken = default) =>
        RunSucceededAsync(Request.Toggle, cancellationToken);

    // Explicit restoration never reads strict living eligibility or becomes a new enable
    public Task<bool> RestoreAsync(CancellationToken cancellationToken = default) =>
        RunSucceededAsync(Request.Restore, cancellationToken);

    public Task<bool> SetAsync(bool desired, CancellationToken cancellationToken = default) =>
        RunSucceededAsync(desired ? Request.Enable : Request.Disable, cancellationToken);

    // Aggregate actions can reuse the local owner and its actual write receipt
    public Task<GodmodeTransitionResult> SetWithTransitionAsync(bool desired,
        CancellationToken cancellationToken = default) =>
        RunAsync(desired ? Request.Enable : Request.Disable, cancellationToken);

    private async Task<bool> RunSucceededAsync(Request request, CancellationToken cancellationToken,
        LocalAccountSessionBinding? expectedBinding = null) =>
        (await RunAsync(request, cancellationToken, expectedBinding).ConfigureAwait(false)).Succeeded;

    private async Task<GodmodeTransitionResult> RunAsync(Request request, CancellationToken cancellationToken,
        LocalAccountSessionBinding? expectedBinding = null) {
        bool entered = false;
        try {
            if (Volatile.Read(ref closing) != 0 || cancellationToken.IsCancellationRequested) return new(false, false);
            // A background receipt cannot wait behind an explicit action and recover its old visit
            if (expectedBinding is not null) {
                entered = await gate.WaitAsync(0, cancellationToken).ConfigureAwait(false);
                if (!entered) return new(false, false);
            } else {
                await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                entered = true;
            }
            if (Volatile.Read(ref closing) != 0) return new(false, false);
            Volatile.Write(ref operating, 1);
            // Both receipt samples stay under this gate so a queued action cannot supply this result's write
            GodmodeFlagsWrite? previous = transport?.LastWrite;
            bool succeeded = await ExecuteLockedAsync(request, cancellationToken, expectedBinding).ConfigureAwait(false);
            GodmodeFlagsWrite? written = transport?.LastWrite;
            bool desired = request == Request.Enable;
            bool confirmed = desired ? feature?.Active == true
                : feature is not null && !feature.Active && !feature.HasPendingRestore;
            bool ownedWrite = request is Request.Enable or Request.Disable && succeeded && confirmed
                && written is { Accepted: true } && !ReferenceEquals(previous, written)
                && ((written.Desired & 1U) != 0) == desired;
            return new(succeeded, ownedWrite);
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            return new(false, false);
        } catch (Exception error) {
            Publish(GodmodeValue.Unknown, false, error.Message);
            return new(false, false);
        } finally {
            if (entered) {
                Volatile.Write(ref operating, 0);
                gate.Release();
            }
        }
    }

    private async Task<bool> ExecuteLockedAsync(Request request, CancellationToken cancellationToken,
        LocalAccountSessionBinding? expectedBinding) {
        bool toggle = request == Request.Toggle;
        bool restoreOnly = request is Request.Restore or Request.Disable;
        // OFF uses the retained owner even when strict living eligibility is no longer available
        if (restoreOnly && feature is null) return false;
        if (feature is not null && (restoreOnly || toggle && (feature.Active || feature.HasPendingRestore
            || Volatile.Read(ref view).Value == GodmodeValue.On))) {
            bool restored = await feature.SetAsync(false, cancellationToken).ConfigureAwait(false);
            Publish(restored && !feature.HasPendingRestore ? GodmodeValue.Off : GodmodeValue.Unknown,
                false, restored ? "Off; a fresh living observation is required to enable" : feature.FailureReason);
            return restored && !feature.HasPendingRestore;
        }

        if (unconfirmedStop || feature?.HasPendingRestore == true && feature.Active == false) {
            Publish(GodmodeValue.Unknown, false, "The original flag still needs cleanup");
            return false;
        }
        var current = readCurrent() ?? throw new IOException("The local actor observation is unavailable");
        // Close can start while a source callback is blocked, before any binding or enable
        if (Volatile.Read(ref closing) != 0 || cancellationToken.IsCancellationRequested) return false;
        // Independent flags must still describe the complete actor copied by the observation
        if (expectedBinding is not null && current.Binding != expectedBinding) {
            // A proven replacement still retires an existing owner without adopting an unbound candidate
            if (binding is not null && !ValidateStrictObservation(current)) return false;
            Publish(GodmodeValue.Unknown, false, "The observation and flags describe different local actors");
            return false;
        }
        if (!ValidateStrictObservation(current)) return false;

        // An ON value is confirmed only against this feature's actual successful write
        if (feature?.Active == true) {
            var receipt = transport?.LastWrite;
            bool owned = receipt is { Accepted: true } && (receipt.Desired & 1U) != 0
                && receipt.Desired == current.Flags;
            Publish(owned ? GodmodeValue.On : GodmodeValue.Unknown, owned,
                owned ? "On" : "The player flags changed; cleanup remains unconfirmed");
            return owned;
        }
        if ((current.Flags & 1U) != 0) {
            Publish(GodmodeValue.Unknown, false, "The godmode flag is set by another owner");
            return false;
        }
        if (feature is null) {
            // Creating the retained adapter is read-only and cannot borrow an existing flag
            var bound = bind(current)
                ?? throw new IOException("The local actor flags transport is unavailable");
            transport = new OwnedGodmodeFlagsTransport(new LocalGodmodeOwnershipTransport(bound,
                () => Volatile.Read(ref invalidated) != 0,
                () => Volatile.Read(ref closing) == 0));
            feature = new TeammateGodmodeFeature(transport, monitorInterval);
        }
        // A blocked factory can return after close or caller cancellation; retain it for cleanup
        if (Volatile.Read(ref closing) != 0 || cancellationToken.IsCancellationRequested) return false;
        if (!toggle && request != Request.Enable) {
            // A clear strict snapshot cannot revive a retired or newly changed native adapter
            uint confirmed = transport!.ReadFlags();
            if ((confirmed & 1U) != 0) {
                Publish(GodmodeValue.Unknown, false, "The godmode flag is set by another owner");
                return false;
            }
            Publish(GodmodeValue.Off, true, "Off");
            return true;
        }
        // The write adapter also checks admission after the primitive's own native baseline read
        if (Volatile.Read(ref closing) != 0 || cancellationToken.IsCancellationRequested) return false;
        bool enabled = await feature.SetAsync(true, cancellationToken).ConfigureAwait(false);
        Publish(enabled && feature.Active ? GodmodeValue.On : GodmodeValue.Unknown,
            enabled && feature.Active, enabled ? "On" : feature.FailureReason);
        return enabled && feature.Active;
    }
}
