using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using syndishanx_iw_tool.Players;

namespace syndishanx_iw_tool.Overlay.Players;

// This receipt is private session proof, never a wire player identifier
public sealed class PlayerMenuTarget : IEquatable<PlayerMenuTarget> {
    public PlayerMenuTarget(PlayerIdentity identity, PlayerMenuRoute route, object ownerToken, ulong selectionRevision) {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(ownerToken);
        if (!Enum.IsDefined(route)) throw new ArgumentOutOfRangeException(nameof(route));
        if (selectionRevision == 0) throw new ArgumentOutOfRangeException(nameof(selectionRevision));
        Identity = identity;
        Route = route;
        OwnerToken = ownerToken;
        SelectionRevision = selectionRevision;
    }

    [JsonIgnore] public PlayerIdentity Identity { get; }
    [JsonIgnore] public PlayerMenuRoute Route { get; }
    [JsonIgnore] public object OwnerToken { get; }
    [JsonIgnore] public ulong SelectionRevision { get; }

    // Equal-looking owner tokens cannot share a captured selection or a row topology
    public bool Equals(PlayerMenuTarget? other) => other is not null
        && ReferenceEquals(OwnerToken, other.OwnerToken) && Identity == other.Identity
        && Route == other.Route && SelectionRevision == other.SelectionRevision;
    public override bool Equals(object? other) => other is PlayerMenuTarget target && Equals(target);
    public override int GetHashCode() => HashCode.Combine(Identity, Route,
        RuntimeHelpers.GetHashCode(OwnerToken), SelectionRevision);
}
