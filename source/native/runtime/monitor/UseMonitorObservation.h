#pragma once
#include "../../shared/monitor/MenuUseContinuation.h"

namespace iw::overlay::runtime {
// All three fields come from the same immutable backend packet
struct UseMonitorObservation {
    const MenuUseContinuation* continuation = nullptr;
    const MenuOptionCallback* callback = nullptr;
    std::uint64_t acknowledgedSequence = 0;
};
}
