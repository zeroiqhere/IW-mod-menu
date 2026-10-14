using syndishanx_iw_tool.Players;

namespace syndishanx_iw_tool.Features.Godmode;

public sealed partial class PlayerGodmodeRegistry {
    private long metadataRevision;

    private void BeginMetadataOperation(PlayerIdentity[] captured) {
        lock (entriesLock) {
            // Even an OFF to ON to OFF cycle permanently invalidates an older copied receipt
            metadataRevision = checked(metadataRevision + 1);
            Volatile.Write(ref operating, captured);
        }
    }

    // Other retained owners can affect cleanup even when one player is selected
    internal GodmodeMetadataStamp ReadAllOwnerMetadata() {
        lock (entriesLock) {
            var members = entries.Values.Select(entry => new GodmodeOwnerMetadata(entry.Identity,
                entry.Known, entry.CleanupRequired, entry.UnconfirmedStop, entry.Feature.Active,
                entry.Feature.HasPendingRestore, entry.Failure ?? entry.Feature.FailureReason, closed,
                Volatile.Read(ref operating).Contains(entry.Identity))).ToArray();
            return new(metadataRevision, Array.AsReadOnly(members));
        }
    }

    internal GodmodeMetadataStamp ReadMetadataStamp(IReadOnlyList<PlayerIdentity> captured,
        bool ownOperationReceipt = false) {
        PlayerIdentity[] identities = CapturedGodmodeRoster.Copy(captured);
        if (!CapturedGodmodeRoster.Valid(identities)) identities = Array.Empty<PlayerIdentity>();
        lock (entriesLock) {
            var members = identities.Select(identity => {
                entries.TryGetValue(identity, out Entry? entry);
                // Active and pending are cached primitive fields; no adapter I/O runs here
                return new GodmodeOwnerMetadata(identity, entry?.Known == true,
                    entry?.CleanupRequired == true, entry?.UnconfirmedStop == true,
                    entry?.Feature.Active == true, entry?.Feature.HasPendingRestore == true,
                    entry?.Failure ?? entry?.Feature.FailureReason, closed,
                    !ownOperationReceipt && Volatile.Read(ref operating).Contains(identity));
            }).ToArray();
            return new(metadataRevision, Array.AsReadOnly(members));
        }
    }
}
