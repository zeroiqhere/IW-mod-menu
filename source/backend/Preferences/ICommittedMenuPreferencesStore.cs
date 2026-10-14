using Apparition.Backend.Session;

namespace Apparition.Backend.Preferences;

// Storage has no process, feature, or menu-opening authority
public interface ICommittedMenuPreferencesStore {
    bool TryRead(LocalMenuPreferenceScope scope, out CommittedMenuPreferences? preferences);
    void Write(LocalMenuPreferenceScope scope, CommittedMenuPreferences preferences);
}
