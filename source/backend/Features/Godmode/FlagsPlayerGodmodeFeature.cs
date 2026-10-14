namespace syndishanx_iw_tool.Features.Godmode;

// Keep flag ownership in the existing primitive while adding read-only preflight
public sealed class FlagsPlayerGodmodeFeature : IGodmodeTransitionFeature {
    private readonly OwnedGodmodeFlagsTransport transport;
    private readonly TeammateGodmodeFeature feature;
    private bool closed;

    public FlagsPlayerGodmodeFeature(ITeammateFlagsTransport transport, TimeSpan? interval = null) {
        ArgumentNullException.ThrowIfNull(transport);
        this.transport = new(transport);
        feature = new TeammateGodmodeFeature(this.transport, interval);
    }

    public bool Connected => transport.Connected;
    public bool Active => feature.Active;
    public bool HasPendingRestore => feature.HasPendingRestore;
    public string? FailureReason => feature.FailureReason;

    public GodmodePreflightResult Preflight(bool desired) {
        try {
            if (desired && closed) {
                return Rejected("The bound feature is closed");
            }
            if (!Connected) {
                // Cleanup remains allowed after the original process has ended
                return desired ? Rejected("The original process is disconnected")
                    : new(true, GodmodeValue.Unknown);
            }
            uint observed = transport.ReadFlags();
            if (HasPendingRestore) {
                uint? originalFlags = transport.OriginalFlags;
                if (originalFlags is null || (observed != originalFlags && observed != (originalFlags | 1U))) {
                    return Rejected("The owned player flags changed; cleanup remains unconfirmed");
                }
                if (desired && !Active) {
                    return Rejected("The previous flag change needs confirmed cleanup");
                }
                return new(true, Active && observed == (originalFlags | 1U)
                    ? GodmodeValue.On : GodmodeValue.Unknown);
            }
            if ((observed & 1U) != 0) {
                return Rejected("The godmode flag is already set and is not owned by this feature");
            }
            // The primitive captures unrelated flags again immediately before enable
            return new(true, GodmodeValue.Off);
        } catch (Exception exception) {
            return Rejected(exception.Message);
        }
    }

    public async Task<bool> SetAsync(bool desired, CancellationToken cancellationToken = default) =>
        (await SetWithTransitionAsync(desired, cancellationToken).ConfigureAwait(false)).Succeeded;

    public async Task<GodmodeTransitionResult> SetWithTransitionAsync(bool desired,
        CancellationToken cancellationToken = default) {
        GodmodeFlagsWrite? previous = transport.LastWrite;
        bool succeeded = await feature.SetAsync(desired, cancellationToken).ConfigureAwait(false);
        GodmodeFlagsWrite? written = transport.LastWrite;
        bool confirmed = desired ? feature.Active : !feature.Active && !feature.HasPendingRestore;
        // An observed game-cleared flag or a rejected write is not an owned transition
        bool ownedWrite = succeeded && confirmed && written is not null && written.Accepted
            && !ReferenceEquals(previous, written) && ((written.Desired & 1U) != 0) == desired;
        return new(succeeded, ownedWrite);
    }

    public async Task<GodmodeCloseResult> CloseAsync() {
        closed = true;
        try {
            bool restored = await feature.CloseAsync().ConfigureAwait(false);
            // A normal return follows the primitive's awaited worker shutdown
            return new(true, restored && !feature.Active && !feature.HasPendingRestore, feature.FailureReason);
        } catch (Exception exception) {
            // A thrown close cannot prove that its worker has finished
            return new(false, false, exception.Message);
        }
    }

    private static GodmodePreflightResult Rejected(string reason) => new(false, GodmodeValue.Unknown, reason);
}
