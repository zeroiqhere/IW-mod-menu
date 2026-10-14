using syndishanx_iw_tool.Players;

namespace syndishanx_iw_tool.Features.Godmode;

// Cached owner facts invalidate a display without reading game memory on a frame
internal sealed record GodmodeOwnerMetadata(PlayerIdentity Identity, bool Known, bool CleanupRequired,
    bool UnconfirmedStop, bool Active, bool Pending, string? Failure, bool Closed, bool Operating);

internal sealed record GodmodeMetadataStamp(long Revision, IReadOnlyList<GodmodeOwnerMetadata> Members) {
    internal bool Matches(GodmodeMetadataStamp? other) => other is not null && Revision == other.Revision
        && Members.SequenceEqual(other.Members);
}
