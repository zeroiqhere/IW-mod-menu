namespace Apparition.Backend.Session;

internal sealed partial class RetailBackendSession {
    public object? ReadOwnedCleanupOwner() {
        var owner = Volatile.Read(ref connection);
        if (owner is null || closing || cleanupBlocked || restartRequired || !owner.HasConfirmedConnection) return null;
        // Registered writers retain original cleanup responsibility even when a display read fails
        var members = owner.GodmodeOwners.ReadAllOwnerMetadata().Members;
        if (members.Any(member => member.Operating)) return null;
        bool ownedChanges = owner.Ammo.Active || owner.Ammo.HasPendingRestore
            || members.Any(member => member.Active || member.Pending || member.CleanupRequired || member.UnconfirmedStop);
        // A replaced or closing connection cannot transfer this private token to another visit
        return ownedChanges && !closing && !cleanupBlocked && !restartRequired
            && owner.HasConfirmedConnection && ReferenceEquals(owner, Volatile.Read(ref connection)) ? owner : null;
    }
}
