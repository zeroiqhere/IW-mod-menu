#pragma once
#include "../../../../native/shared/MenuOptionCallback.h"
#include <array>
#include <optional>

namespace iw::overlay::tests::callback {
struct Expected {
    const char* file;
    std::optional<MenuOptionCallback> callback;
    bool quick;
    bool open;
    bool root;
    std::uint64_t navigation;
    std::uint64_t menu;
    std::uint64_t sequence;
    std::optional<int> openingElapsed;
    std::uint64_t admission;
    bool controlsDisabled;
    bool blocked;
    bool busy;
    bool enabled;
};
// These expectations come from the controller paths, not the exported inventory
inline constexpr auto BooleanPending = MenuOptionCallback{1, MenuOptionCallbackKind::Boolean, MenuOptionCallbackPhase::Pending};
inline constexpr auto BooleanCooldown = MenuOptionCallback{1, MenuOptionCallbackKind::Boolean, MenuOptionCallbackPhase::Cooldown};
inline constexpr auto PlainCooldown = MenuOptionCallback{1, MenuOptionCallbackKind::Plain, MenuOptionCallbackPhase::Cooldown};
inline const std::array<Expected, 14> Profiles{{
    {"boolean-pending-immediate.json", BooleanPending, true, true, true, 1, 3, 2, 550, 10, true, false, false, false},
    {"boolean179.json", BooleanPending, true, true, true, 1, 3, 2, 729, 10, true, false, false, false},
    {"boolean180.json", BooleanCooldown, true, true, true, 1, 4, 2, {}, 10, true, false, false, false},
    {"boolean429.json", BooleanCooldown, true, true, true, 1, 4, 2, {}, 10, true, false, false, false},
    {"boolean430-ready.json", {}, true, true, true, 1, 4, 2, {}, 10, false, false, false, true},
    {"boolean-pending-busy.json", BooleanPending, true, true, true, 1, 3, 2, 550, 10, true, true, true, false},
    {"boolean180-busy.json", BooleanCooldown, true, true, true, 1, 4, 2, {}, 10, true, true, true, false},
    {"boolean430-busy.json", {}, true, true, true, 1, 4, 2, {}, 10, false, true, true, false},
    {"plain-lobby-cooldown0.json", PlainCooldown, false, true, false, 17, 4, 17, {}, 10, true, false, false, true},
    {"plain-lobby-cooldown249.json", PlainCooldown, false, true, false, 17, 4, 17, {}, 10, true, false, false, true},
    {"plain-lobby250-ready.json", {}, false, true, false, 17, 4, 17, {}, 10, false, false, false, true},
    {"focus-hidden-pending.json", {}, true, false, true, 1, 3, 2, {}, 10, true, false, false, false},
    {"replaced-owner-closed.json", {}, true, false, true, 2, 4, 2, {}, 11, false, false, false, true},
    {"shutdown-no-callback.json", {}, true, true, true, 1, 3, 2, {}, 10, true, true, true, false}
}};
}
