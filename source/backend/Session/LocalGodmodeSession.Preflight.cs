using syndishanx_iw_tool.Features.Godmode;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Apparition.Backend.Session;

// Registry calls share the session's existing writer and monitor
public sealed partial class LocalGodmodeSession : IGodmodeTransitionFeature {
    private string? preflightFailureReason;

    // These properties inspect retained metadata without entering a source read
    public bool Connected => Volatile.Read(ref transport)?.Connected == true;
    public bool Active => Volatile.Read(ref feature)?.Active == true;
    public bool HasPendingRestore => Volatile.Read(ref unconfirmedStop)
        || Volatile.Read(ref feature)?.HasPendingRestore == true;
    public string? FailureReason => Volatile.Read(ref preflightFailureReason)
        ?? Volatile.Read(ref feature)?.FailureReason;

    public GodmodePreflightResult Preflight(bool desired) {
        // Synchronous callers must not wait behind a binding callback or cleanup worker
        if (!gate.Wait(0)) return new(false, GodmodeValue.Unknown, "The local godmode owner is busy");
        try {
            if (feature is null || transport is null) return RejectPreflight("The local godmode owner is not captured");
            if (desired && Volatile.Read(ref closing) != 0) return RejectPreflight("The local godmode owner is closing");
            if (!transport.Connected) {
                // An ended process can release cleanup without borrowing a replacement actor
                return desired ? RejectPreflight("The original process is disconnected")
                    : AcceptPreflight(GodmodeValue.Unknown);
            }
            if (Volatile.Read(ref invalidated) != 0) return RejectPreflight("The original local actor binding has been retired");
            if (desired && (unconfirmedStop || feature.HasPendingRestore && !feature.Active)) {
                return RejectPreflight("The original flag still needs confirmed cleanup");
            }

            LocalActorFlagsSnapshot? strict = null;
            if (desired) {
                // A life-only failure leaves the retained OFF path available
                strict = readCurrent() ?? throw new IOException("The local actor observation is unavailable");
                if (!ValidateStrictObservation(strict)) return RejectPreflight("The original local actor or command time changed");
                if (Volatile.Read(ref closing) != 0) return RejectPreflight("The local godmode owner is closing");
            }
            // This adapter checks the original full binding and its monotone command time
            uint observed = transport.ReadFlags();
            if (strict is not null && strict.Flags != observed) return RejectPreflight("The local flags changed during preflight");
            uint? original = transport.OriginalFlags;
            if (feature.HasPendingRestore || unconfirmedStop) {
                if (original is null) return RejectPreflight("The original flag word is unavailable");
                if (observed != original.Value && observed != (original.Value | 1U)) {
                    return RejectPreflight("The owned player flags changed; cleanup remains unconfirmed");
                }
                if (unconfirmedStop) return AcceptPreflight(GodmodeValue.Unknown);
                // A game-cleared flag is not confirmed OFF until the original feature releases it
                return AcceptPreflight(feature.Active && observed == (original.Value | 1U)
                    ? GodmodeValue.On : GodmodeValue.Unknown);
            }
            if ((observed & 1U) != 0) return RejectPreflight("The godmode flag is set by another owner");
            // Unowned clear bits remain available for the primitive to capture at enable
            return AcceptPreflight(GodmodeValue.Off);
        } catch (Exception error) {
            return RejectPreflight(error.Message);
        } finally {
            gate.Release();
        }
    }

    // Selection and preflight use one retirement rule for the same strict observation
    private bool ValidateStrictObservation(LocalActorFlagsSnapshot current) {
        if (binding is null) binding = current.Binding;
        if (Volatile.Read(ref invalidated) != 0 || current.Binding != binding || current.CommandTime < lastCommandTime) {
            // A proven replacement or rollback cannot recover merely by restoring old scalars
            Volatile.Write(ref invalidated, 1);
            Publish(GodmodeValue.Unknown, false, "The original local actor or command time changed");
            return false;
        }
        lastCommandTime = current.CommandTime;
        return true;
    }

    private GodmodePreflightResult RejectPreflight(string reason) {
        // Only the held session gate can withdraw a cached capability after failed proof
        Publish(GodmodeValue.Unknown, false, reason);
        Volatile.Write(ref preflightFailureReason, reason);
        return new(false, GodmodeValue.Unknown, reason);
    }

    private GodmodePreflightResult AcceptPreflight(GodmodeValue value) {
        Volatile.Write(ref preflightFailureReason, null);
        return new(true, value);
    }

    // The interface joins the same feature close and preserves its unconfirmed outcome
    async Task<GodmodeCloseResult> IPlayerGodmodeFeature.CloseAsync() {
        var result = await CloseAsync().ConfigureAwait(false);
        return new(result.Stopped, result.Restored, result.ErrorMessage);
    }
}
