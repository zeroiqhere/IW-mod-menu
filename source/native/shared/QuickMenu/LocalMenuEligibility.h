#pragma once
#include "ClosedMenuOpening.h"
#include <cstdint>
#include <optional>

namespace iw::overlay {
// Family identity is explicit and never inferred from a title or root flag
enum class MenuSurface { Main, Quick };

// The owner publishes only copied admission facts, without account or entity addresses
struct LocalMenuEligibility {
    std::uint64_t revision = 0;
    PlayerLifeState life = PlayerLifeState::Unknown;
    // A missing observation remains unknown rather than becoming permission
    std::optional<bool> hasMenu = std::nullopt;
    std::optional<bool> controlsDisabled = std::nullopt;
    bool blocked = false;
    bool operator==(const LocalMenuEligibility &) const = default;
};

inline bool IsKnownMenuSurface(MenuSurface surface) noexcept {
    return surface == MenuSurface::Main || surface == MenuSurface::Quick;
}
inline bool IsValidLocalMenuEligibility(const LocalMenuEligibility &eligibility) noexcept {
    // Positive revisions identify one complete owner admission observation
    return eligibility.revision != 0 && (eligibility.life == PlayerLifeState::Unknown ||
        eligibility.life == PlayerLifeState::Alive || eligibility.life == PlayerLifeState::Dead);
}
} // namespace iw::overlay
