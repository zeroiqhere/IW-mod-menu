#pragma once
#include "../MenuOptionCallback.h"

namespace iw::overlay {
// The backend keeps private action and target proof behind this copied invocation receipt
struct MenuUseContinuation {
    std::uint64_t revision = 0;
    std::uint64_t sequence = 0;
    std::uint64_t admissionRevision = 0;
    MenuOptionCallbackKind kind = MenuOptionCallbackKind::Plain;
    bool operator==(const MenuUseContinuation&) const = default;
};
// Shape checks do not grant permission or claim that a gameplay action succeeded
constexpr bool IsValidMenuUseContinuation(const MenuUseContinuation& value) noexcept {
    return value.revision && value.sequence && value.admissionRevision
        && (value.kind == MenuOptionCallbackKind::Plain || value.kind == MenuOptionCallbackKind::Boolean);
}
}
