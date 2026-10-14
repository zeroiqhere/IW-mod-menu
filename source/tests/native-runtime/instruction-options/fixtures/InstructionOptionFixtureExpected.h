#pragma once
#include "../../../../native/shared/MenuOptionCallback.h"
#include <array>
#include <optional>

namespace iw::overlay::tests::instructionOptions {
struct Expected {
    const char* file;
    std::size_t selected;
    std::uint64_t navigation, menu, sequence, paintRevision;
    unsigned currentMask, cachedMask;
    std::optional<MenuOptionCallback> callback;
};
inline constexpr auto Pending = MenuOptionCallback{1, MenuOptionCallbackKind::Boolean, MenuOptionCallbackPhase::Pending};
inline constexpr auto Cooldown = MenuOptionCallback{1, MenuOptionCallbackKind::Boolean, MenuOptionCallbackPhase::Cooldown};
inline constexpr auto SecondPending = MenuOptionCallback{2, MenuOptionCallbackKind::Boolean, MenuOptionCallbackPhase::Pending};
inline constexpr auto SecondCooldown = MenuOptionCallback{2, MenuOptionCallbackKind::Boolean, MenuOptionCallbackPhase::Cooldown};
// Each bit represents a checked source row, including both inverse Disable labels
inline const std::array<Expected, 16> Profiles{{
    {"instructions-before.json", 0, 2, 4, 1, 1, 0, 0, {}},
    {"disable0.json", 0, 2, 5, 2, 2, 1, 1, Pending},
    {"disable179.json", 0, 2, 5, 2, 2, 1, 1, Pending},
    {"disable180.json", 0, 2, 6, 2, 3, 1, 1, Cooldown},
    {"disable430.json", 0, 2, 6, 2, 3, 1, 1, {}},
    {"alternate0.json", 1, 3, 4, 3, 1, 2, 0, Pending},
    {"alternate180.json", 1, 3, 5, 3, 2, 2, 2, Cooldown},
    {"background0.json", 2, 4, 4, 4, 1, 4, 0, Pending},
    {"background180.json", 2, 4, 5, 4, 2, 4, 4, Cooldown},
    {"adaptive0.json", 3, 5, 4, 5, 1, 8, 0, Pending},
    {"adaptive180.json", 3, 5, 5, 5, 2, 8, 8, Cooldown},
    {"alternate-clears0.json", 1, 9, 5, 10, 2, 2, 8, SecondPending},
    {"alternate-clears180.json", 1, 9, 6, 10, 3, 2, 2, SecondCooldown},
    {"adaptive-rejected0.json", 3, 5, 5, 6, 2, 2, 2, SecondPending},
    {"adaptive-rejected180.json", 3, 5, 6, 6, 3, 2, 2, SecondCooldown},
    {"adaptive-rejected430.json", 3, 5, 6, 6, 3, 2, 2, {}}
}};
}
