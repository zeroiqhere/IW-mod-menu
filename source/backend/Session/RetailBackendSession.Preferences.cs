namespace Apparition.Backend.Session;

internal sealed partial class RetailBackendSession {
    public LocalMenuPreferenceScope? ReadLocalMenuPreferenceScope() {
        var state = Volatile.Read(ref localMenuAdmission);
        // Account proof belongs to the retained receipt, independently of feature adapters
        if (state is null || !CanCommit(state.Owner) || !state.Owner.HasConfirmedConnection) return null;
        var scope = state.PreferenceScope;
        // A replaced receipt or closing owner cannot finish a cached read with earlier authority
        return ReferenceEquals(state, Volatile.Read(ref localMenuAdmission))
            && CanCommit(state.Owner) && state.Owner.HasConfirmedConnection ? scope : null;
    }
}
