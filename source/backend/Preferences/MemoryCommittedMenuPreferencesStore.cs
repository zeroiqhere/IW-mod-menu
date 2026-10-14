using Apparition.Backend.Session;

namespace Apparition.Backend.Preferences;

// An explicitly shared instance retains committed choices only for this process lifetime
public sealed class MemoryCommittedMenuPreferencesStore : ICommittedMenuPreferencesStore {
    private readonly object valuesLock = new();
    private readonly Dictionary<ulong, CommittedMenuPreferences> values = new();

    public bool TryRead(LocalMenuPreferenceScope scope, out CommittedMenuPreferences? preferences) {
        ArgumentNullException.ThrowIfNull(scope);
        // The account key is private; the scope itself remains the caller's authority receipt
        lock (valuesLock) return values.TryGetValue(scope.AccountId, out preferences);
    }

    public void Write(LocalMenuPreferenceScope scope, CommittedMenuPreferences preferences) {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(preferences);
        // One immutable value replaces another without exposing mixed committed fields
        lock (valuesLock) values[scope.AccountId] = preferences;
    }
}
