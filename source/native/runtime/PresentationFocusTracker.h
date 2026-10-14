#pragma once
#include <cstdint>

namespace iw::overlay::runtime {
// Observed focus transitions remain private and cannot be supplied by the session owner
class PresentationFocusTracker {
public:
    explicit PresentationFocusTracker(std::uint64_t initialRevision = 0) : revision_(initialRevision) {}
    std::uint64_t Observe(bool foregroundAllowed);
private:
    std::uint64_t revision_ = 0;
    bool seeded_ = false;
    bool previous_ = false;
    bool failed_ = false;
};
}
