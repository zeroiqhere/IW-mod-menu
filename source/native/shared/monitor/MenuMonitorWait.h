#pragma once
#include <cstdint>

namespace iw::overlay {
// Mechanical monitor waits do not report feature results or grant actor access
enum class MenuMonitorWaitKind { Choice, Back, ChoiceNoop };
struct MenuMonitorWait {
    std::uint64_t revision = 0;
    MenuMonitorWaitKind kind = MenuMonitorWaitKind::Choice;
    bool operator==(const MenuMonitorWait&) const = default;
};
constexpr bool IsValidMenuMonitorWait(const MenuMonitorWait& wait) noexcept {
    return wait.revision != 0 && (wait.kind == MenuMonitorWaitKind::Choice
        || wait.kind == MenuMonitorWaitKind::Back || wait.kind == MenuMonitorWaitKind::ChoiceNoop);
}
}
