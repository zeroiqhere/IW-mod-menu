using syndishanx_iw_tool.Features.Godmode;
using syndishanx_iw_tool.Players;

namespace Apparition.Backend.Session;

// Native observations are copied once by a worker rather than repeated on each frame
public sealed partial class TeammateGodmodeSession {
    private sealed record Publication(SelectedGodmodeView View, GodmodeMetadataStamp Metadata, long ProofRevision);
    private Publication? publication;
    private long proofRevision;
    private int observing;

    public SelectedGodmodeView ReadView() {
        var copied = Volatile.Read(ref publication);
        GodmodeMetadataStamp metadata;
        try { metadata = registry.ReadAllOwnerMetadata(); }
        catch (Exception) {
            // Adapter failures withdraw authority without entering any native proof reader
            return new(Volatile.Read(ref selected), GodmodeValue.Unknown,
                "The selected teammate state could not be confirmed", false, false, true);
        }
        bool stopped = Volatile.Read(ref closing) != 0;
        bool pending = metadata.Members.Any(member => member.CleanupRequired || member.UnconfirmedStop
            || !member.Active && member.Pending);
        bool current = copied is not null && copied.Metadata.Matches(metadata)
            && copied.ProofRevision == Volatile.Read(ref proofRevision) && Volatile.Read(ref observing) == 0;
        if (current && !stopped) return copied!.View;
        // Unknown identity evidence leaves every retained owned change a possible cleanup target
        if (!current) pending |= metadata.Members.Any(member => member.Active || member.Pending);
        // A changed receipt must never combine a historical ON value with new cleanup facts
        var view = copied?.View ?? new(Volatile.Read(ref selected), GodmodeValue.Unknown,
            Volatile.Read(ref status), true, false, false);
        return view with { Value = GodmodeValue.Unknown, CanToggle = false,
            CanSelect = !stopped && copied is null && metadata.Members.Count == 0 && !pending,
            HasCurrentIdentity = false,
            HasPendingCleanup = pending || !stopped && view.HasPendingCleanup,
            Status = stopped ? "The teammate session is closing or closed"
                : copied is null ? Volatile.Read(ref status) : "Refreshing the selected teammate state" };
    }

    internal bool HasSelection => Volatile.Read(ref selected) is not null;

    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.Run(async () => {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            cancellationToken.ThrowIfCancellationRequested();
            if (Volatile.Read(ref closing) == 0) RefreshPublication();
        } finally { gate.Release(); }
    }, cancellationToken);

    private void RefreshPublication() {
        if (Volatile.Read(ref closing) != 0) return;
        Interlocked.Exchange(ref observing, 1);
        try {
            var before = registry.ReadAllOwnerMetadata();
            long proofBefore = Volatile.Read(ref proofRevision);
            var fresh = ReadFreshView();
            var after = registry.ReadAllOwnerMetadata();
            long proofAfter = Volatile.Read(ref proofRevision);
            if (!before.Matches(after) || proofBefore != proofAfter) {
                // The next owned refresh can confirm stable facts after an overlapping operation
                fresh = fresh with { Value = GodmodeValue.Unknown, CanSelect = false, CanToggle = false,
                    HasCurrentIdentity = false, Status = "The selected teammate state changed during refresh" };
            }
            Volatile.Write(ref publication, new(fresh, after, proofAfter));
        } catch (Exception) {
            // A failed refresh must not fault an action or retain its selection gate
            Volatile.Write(ref status, "The selected teammate state could not be confirmed");
            Volatile.Write(ref publication, null);
        } finally { Volatile.Write(ref observing, 0); }
    }

    private SelectedGodmodeView ReadFreshView() {
        PlayerIdentity? identity = Volatile.Read(ref selected);
        // Snapshot checks retained ownership without creating adapters or starting workers
        GodmodeRegistrySnapshot snapshot = registry.SnapshotForDisplay(identity is null
            ? Array.Empty<PlayerIdentity>() : new[] { identity });
        PlayerGodmodeSnapshot? member = snapshot.Members.FirstOrDefault();
        bool stopped = Volatile.Read(ref closing) != 0;
        bool pending = snapshot.HasPendingCleanup;
        bool cleanupTarget = member is not null && (member.Active || member.HasPendingRestore);
        // Only an existing owned change can use the life-independent cleanup proof
        bool knownDead = false;
        bool current = identity is not null && (cleanupTarget
            ? MatchesRetainedIdentity(identity) : MatchesCurrentIdentity(identity, out knownDead));
        // Confirm the retained identity after strict sampling, which can retire a replaced owner
        bool currentIdentity = !stopped && member?.Connected == true && identity is not null
            && MatchesRetainedIdentity(identity);
        current &= currentIdentity;
        bool canToggle = !stopped && current && member is { Connected: true }
            && (!pending || cleanupTarget) && (member.Value != GodmodeValue.Unknown || cleanupTarget);
        string detail = member?.FailureReason ?? Volatile.Read(ref status);
        if (identity is not null && !current) detail = knownDead
            ? "The selected teammate is not living; new changes are unavailable"
            : currentIdentity
                ? "The selected teammate's living state could not be confirmed; new changes are unavailable"
                // A failed read withdraws proof but cannot establish a replacement by itself
                : "The selected player or match identity could not be confirmed; new changes are unavailable";
        if (pending && string.IsNullOrEmpty(member?.FailureReason))
            detail = "An owned change needs confirmed cleanup; turn off the original selection or stop the session";
        if (stopped) detail = "The teammate session is closing or closed";
        return new(identity, current ? member?.Value ?? GodmodeValue.Unknown : GodmodeValue.Unknown,
            detail, !stopped && !pending, canToggle, pending) {
                // This proof remains valid through death, but not replacement or observed retirement
                HasCurrentIdentity = currentIdentity
            };
    }

    private void RetireIdentity(PlayerIdentity identity) {
        // A fresh failed action can withdraw copied identity evidence before another poll
        if (retired.Add(identity)) Interlocked.Increment(ref proofRevision);
    }
}
