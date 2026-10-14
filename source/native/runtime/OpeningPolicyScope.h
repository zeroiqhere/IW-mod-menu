#pragma once
#include "../shared/MenuSnapshot.h"
#include <stdexcept>
namespace iw::overlay::runtime {
// Commands capture the epoch of the observed owner rather than a later controller state
inline std::optional<std::uint64_t> ObservedOpeningControlsRevision(const MenuSnapshot &snapshot) {
    if (!IsValidOpeningPolicyOwnership(snapshot.mainOpeningControls, snapshot.openingPolicies)) {
        throw std::runtime_error("Cannot capture an invalid opening owner");
    }
    if (snapshot.openingPolicies) return snapshot.openingPolicies->revision;
    return snapshot.mainOpeningControls ? std::optional{snapshot.mainOpeningControls->revision} : std::nullopt;
}
} // namespace iw::overlay::runtime
