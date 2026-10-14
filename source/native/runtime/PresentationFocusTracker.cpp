#include "PresentationFocusTracker.h"
#include <limits>
#include <stdexcept>

namespace iw::overlay::runtime {
std::uint64_t PresentationFocusTracker::Observe(bool foregroundAllowed) {
    if (failed_) throw std::overflow_error("Presentation focus tracking has stopped");
    if (seeded_ && foregroundAllowed != previous_) {
        // A wrapped epoch could make a later frame inherit stale presentation state
        if (revision_ == std::numeric_limits<std::uint64_t>::max()) {
            failed_ = true;
            throw std::overflow_error("Presentation focus revision exhausted");
        }
        ++revision_;
    }
    seeded_ = true;
    previous_ = foregroundAllowed;
    return revision_;
}
}
