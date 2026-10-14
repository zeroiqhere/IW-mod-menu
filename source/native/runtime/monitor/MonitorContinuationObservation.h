#pragma once
#include "../../shared/monitor/MenuMonitorContinuation.h"

namespace iw::overlay::runtime {
// The receipt and acknowledgment belong to one immutable controller packet
struct MonitorContinuationObservation {
    const MenuMonitorContinuation* continuation = nullptr;
    std::uint64_t acknowledgedSequence = 0;
};
}
