using syndishanx_iw_tool.Players;

namespace syndishanx_iw_tool.Features.Godmode;

public enum GodmodeValue { Unknown, Off, On, Mixed }

// Members are copied into read-only collections before leaving the registry
public sealed record PlayerGodmodeSnapshot(PlayerIdentity Identity, GodmodeValue Value,
    bool Connected, bool Active, bool HasPendingRestore, string? FailureReason);

public sealed record GodmodeRegistrySnapshot(GodmodeValue Value, IReadOnlyList<PlayerGodmodeSnapshot> Members,
    bool HasPendingCleanup, bool Closed);

public sealed record GodmodeOperationResult(bool Succeeded, bool Cancelled, string? FailureReason,
    GodmodeRegistrySnapshot Snapshot) {
    internal GodmodeMetadataStamp? OwnerMetadata { get; init; }
}

public sealed record GodmodeRegistryCloseResult(bool Stopped, bool Restored, GodmodeRegistrySnapshot Snapshot) {
    public bool SafeToRelease => Stopped && Restored;
}
