namespace Apparition.Backend.Session;

// Account identity stays private and never travels in a published menu frame
public sealed class LocalMenuPreferenceScope {
    internal ulong AccountId { get; }
    internal object OwnerToken { get; }

    internal LocalMenuPreferenceScope(ulong accountId, object ownerToken) {
        if (accountId == 0) throw new ArgumentOutOfRangeException(nameof(accountId));
        AccountId = accountId;
        OwnerToken = ownerToken ?? throw new ArgumentNullException(nameof(ownerToken));
    }
}
