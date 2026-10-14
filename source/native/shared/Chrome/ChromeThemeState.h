#pragma once
#include "../MenuDesign.h"
#include <array>
#include <cstdint>
#include <optional>
#include <vector>
namespace iw::overlay {
// Role bits describe existing source objects, independently of pixel visibility
using ChromeRgb = std::array<std::uint8_t, 3>;
enum class ChromeRole : unsigned { Scroller, Banner, Title, Separator, BottomLine };
constexpr unsigned ChromeRoleBit(ChromeRole role) noexcept { return 1U << static_cast<unsigned>(role); }
constexpr unsigned ChromeCreationMask(MenuDesign design) noexcept {
    switch (design) {
        case MenuDesign::Apparition: return 31;
        case MenuDesign::Classic: return 7;
        case MenuDesign::Native: return 15;
        case MenuDesign::Aio: return 29;
        case MenuDesign::Basic: return 7;
    }
    return 0;
}
constexpr unsigned ChromeFixedMask(MenuDesign design) noexcept {
    switch (design) {
        case MenuDesign::Apparition: return 28;
        case MenuDesign::Classic: case MenuDesign::Native: return 3;
        case MenuDesign::Aio: return 25;
        case MenuDesign::Basic: return 4;
    }
    return 0;
}
constexpr unsigned ChromeRainbowMask(MenuDesign design) noexcept {
    return design == MenuDesign::Basic ? 5U : ChromeFixedMask(design);
}
struct ChromeThemeEvent {
    std::uint64_t revision = 0;
    unsigned targetMask = 0;
    ChromeRgb accent{};
    std::uint64_t elapsedMilliseconds = 0;
    std::optional<unsigned> predecessorGapMilliseconds;
    bool stoppedRainbow = false;
};
struct ChromeThemeState {
    bool exists = false;
    std::optional<std::uint64_t> creationRevision, admissionRevision;
    std::optional<MenuDesign> creationDesign;
    std::optional<ChromeRgb> creationAccent;
    std::optional<bool> creationRainbow;
    unsigned currentRoleMask = 0;
    bool prefixComplete = true;
    std::vector<ChromeThemeEvent> events;
};
inline bool IsValidChromeThemeState(const ChromeThemeState& state) noexcept {
    if (!state.exists) {
        // Canonical absence cannot carry a retired owner's color authority
        return !state.creationRevision && !state.admissionRevision && !state.creationDesign
            && !state.creationAccent && !state.creationRainbow && !state.currentRoleMask
            && state.prefixComplete && state.events.empty();
    }
    if (!state.creationRevision || !*state.creationRevision || !state.admissionRevision || !*state.admissionRevision
        || !state.creationDesign || !IsKnownMenuDesign(*state.creationDesign) || !state.creationAccent
        || !state.creationRainbow || state.events.size() > 8) return false;
    const auto normal = ChromeCreationMask(*state.creationDesign);
    if (state.currentRoleMask != normal && state.currentRoleMask != (normal & ~4U)) return false;
    std::uint64_t previousRevision = 0, previousAge = 0;
    for (const auto& event : state.events) {
        if (!event.revision || event.targetMask != ChromeFixedMask(*state.creationDesign)
            || (event.predecessorGapMilliseconds && *event.predecessorGapMilliseconds > 500)) return false;
        if (previousRevision) {
            // The retained journal is a contiguous tail for both complete and pruned prefixes
            if (event.revision <= previousRevision || event.revision - previousRevision != 1
                || event.elapsedMilliseconds > previousAge || !event.predecessorGapMilliseconds) return false;
            const auto difference = previousAge - event.elapsedMilliseconds;
            const auto gap = *event.predecessorGapMilliseconds;
            if (gap == 500) { if (difference < 499) return false; }
            else if (difference > gap ? difference - gap > 1 : gap - difference > 1) return false;
        } else if (state.prefixComplete && event.predecessorGapMilliseconds) return false;
        previousRevision = event.revision; previousAge = event.elapsedMilliseconds;
    }
    return true;
}
}
