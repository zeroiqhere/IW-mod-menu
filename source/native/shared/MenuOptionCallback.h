#pragma once
#include <cstdint>

namespace iw::overlay {
// Callback cadence is separate from the lifetime and result of backend work
// Plain callbacks have no generic boolean redraw
// Both phases pause ordinary monitor input
// Only the backend advances these owner boundaries
enum class MenuOptionCallbackKind { Boolean, Plain };
enum class MenuOptionCallbackPhase { Pending, Cooldown };
struct MenuOptionCallback {
    std::uint64_t revision = 0;
    MenuOptionCallbackKind kind = MenuOptionCallbackKind::Boolean;
    MenuOptionCallbackPhase phase = MenuOptionCallbackPhase::Pending;
    bool operator==(const MenuOptionCallback&) const = default;
};
// Direct native callers receive the same bounded owner shape as parsed packets
constexpr bool IsValidMenuOptionCallback(const MenuOptionCallback& value) noexcept {
    return value.revision != 0
        && (value.kind == MenuOptionCallbackKind::Boolean || value.kind == MenuOptionCallbackKind::Plain)
        && (value.phase == MenuOptionCallbackPhase::Pending || value.phase == MenuOptionCallbackPhase::Cooldown)
        && (value.kind != MenuOptionCallbackKind::Plain || value.phase == MenuOptionCallbackPhase::Cooldown);
}
// A boolean redraw replaces old option HUDs, while a plain callback keeps them
constexpr bool CallbackKeepsOpening(const MenuOptionCallback& value) noexcept {
    return IsValidMenuOptionCallback(value)
        && (value.kind == MenuOptionCallbackKind::Plain || value.phase == MenuOptionCallbackPhase::Pending);
}
}
