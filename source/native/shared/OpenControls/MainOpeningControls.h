#pragma once
#include "OpeningAction.h"
#include <cstdint>
#include <vector>
namespace iw::overlay {
struct MainOpeningControls {
    std::uint64_t revision = 0;
    std::vector<OpeningAction> actions;
    bool operator==(const MainOpeningControls &) const = default;
};
// Validate IDs before shifting so malformed values cannot alias a known action
inline bool IsValidMainOpeningControls(const MainOpeningControls &controls) noexcept {
    if (!controls.revision || controls.actions.empty() || controls.actions.size() > 3) {
        return false;
    }
    unsigned seen = 0;
    for (auto action : controls.actions) {
        if (!IsKnownOpeningAction(action)) {
            return false;
        }
        const unsigned bit = 1u << static_cast<unsigned>(action);
        if (seen & bit) {
            return false;
        }
        seen |= bit;
    }
    // The source rejects pairing both offhands in the same tuple
    const unsigned offhands = (1u << static_cast<unsigned>(OpeningAction::Frag)) |
                              (1u << static_cast<unsigned>(OpeningAction::SecondaryOffhand));
    return (seen & offhands) != offhands;
}
} // namespace iw::overlay
