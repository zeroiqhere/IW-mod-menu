#pragma once
#include "../../../../native/shared/MenuOptionCallback.h"
#include <array>
#include <optional>

namespace iw::overlay::tests::localOptions {
struct Expected {
    const char* file;
    const char* title;
    std::size_t rows;
    std::size_t selected;
    std::uint64_t navigation, menu, sequence;
    const char* accent;
    bool rainbow;
    const char* display;
    bool stealth;
    std::optional<MenuOptionCallback> callback;
    std::uint64_t paintRevision;
    int currentOn, cachedOn;
};
inline constexpr auto BooleanPending = MenuOptionCallback{1, MenuOptionCallbackKind::Boolean, MenuOptionCallbackPhase::Pending};
inline constexpr auto BooleanCooldown = MenuOptionCallback{1, MenuOptionCallbackKind::Boolean, MenuOptionCallbackPhase::Cooldown};
inline constexpr auto PlainCooldown = MenuOptionCallback{1, MenuOptionCallbackKind::Plain, MenuOptionCallbackPhase::Cooldown};
// The rejected slider follows the completed Stealth callback on the same controller
inline constexpr auto PlainCooldownAfterStealth = MenuOptionCallback{2, MenuOptionCallbackKind::Plain, MenuOptionCallbackPhase::Cooldown};
// Expected paths and epochs remain fixed rather than being inferred from exported files
inline const std::array<Expected, 12> Profiles{{
    {"palette-before.json", "Main Design Color", 37, 6, 8, 4, 7, "#3998FE", false, "Boxes", false, {}, 1, 6, 6},
    {"palette0.json", "Main Design Color", 37, 7, 9, 4, 9, "#0064C8", false, "Boxes", false, BooleanPending, 1, 7, 6},
    {"palette179.json", "Main Design Color", 37, 7, 9, 4, 9, "#0064C8", false, "Boxes", false, BooleanPending, 1, 7, 6},
    {"palette180.json", "Main Design Color", 37, 7, 9, 5, 9, "#0064C8", false, "Boxes", false, BooleanCooldown, 2, 7, 7},
    {"palette430.json", "Main Design Color", 37, 7, 9, 5, 9, "#0064C8", false, "Boxes", false, {}, 2, 7, 7},
    {"rainbow0.json", "Main Design Color", 37, 36, 38, 4, 38, "#3998FE", true, "Boxes", false, BooleanPending, 1, 36, 6},
    {"rainbow180.json", "Main Design Color", 37, 36, 38, 5, 38, "#3998FE", true, "Boxes", false, BooleanCooldown, 2, 36, 36},
    {"slider-changed0.json", "Menu Preferences", 12, 1, 3, 5, 4, "#3998FE", false, "Text", false, PlainCooldown, 0, -1, -1},
    {"slider250.json", "Menu Preferences", 12, 1, 3, 5, 4, "#3998FE", false, "Text", false, {}, 0, -1, -1},
    {"slider-rejected0.json", "Menu Preferences", 12, 1, 15, 5, 16, "#3998FE", false, "Text", true, PlainCooldownAfterStealth, 0, -1, -1},
    {"reset0.json", "Menu Instructions", 6, 5, 7, 4, 7, "#3998FE", false, "Boxes", false, PlainCooldown, 1, -1, -1},
    {"reset250.json", "Menu Instructions", 6, 5, 7, 4, 7, "#3998FE", false, "Boxes", false, {}, 1, -1, -1}
}};
}
