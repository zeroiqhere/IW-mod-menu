#pragma once
#include "MenuMonitorWait.h"

namespace iw::overlay {
// The producer retains private visit and target proof behind this copied monitor receipt
struct MenuMonitorContinuation {
    std::uint64_t revision = 0;
    std::uint64_t sequence = 0;
    std::uint64_t admissionRevision = 0;
    MenuMonitorWaitKind kind = MenuMonitorWaitKind::Choice;
    bool operator==(const MenuMonitorContinuation&) const = default;
};
// Valid shape identifies an invocation without granting actor or opening authority
constexpr bool IsValidMenuMonitorContinuation(const MenuMonitorContinuation& value) noexcept {
    return value.revision && value.sequence && value.admissionRevision
        && IsValidMenuMonitorWait({value.revision, value.kind});
}
}
