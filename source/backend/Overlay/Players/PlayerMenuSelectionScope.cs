using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace syndishanx_iw_tool.Overlay.Players;

// A name-only roster request retains its original session before strict capture
public sealed class PlayerMenuSelectionScope : IEquatable<PlayerMenuSelectionScope> {
    public PlayerMenuSelectionScope(object ownerToken, ulong revision) {
        ArgumentNullException.ThrowIfNull(ownerToken);
        OwnerToken = ownerToken;
        Revision = revision;
    }

    [JsonIgnore] public object OwnerToken { get; }
    // Zero describes a session that has not published its first selection
    [JsonIgnore] public ulong Revision { get; }

    public bool Equals(PlayerMenuSelectionScope? other) => other is not null
        && ReferenceEquals(OwnerToken, other.OwnerToken) && Revision == other.Revision;
    public override bool Equals(object? other) => other is PlayerMenuSelectionScope scope && Equals(scope);
    public override int GetHashCode() => HashCode.Combine(RuntimeHelpers.GetHashCode(OwnerToken), Revision);
}
