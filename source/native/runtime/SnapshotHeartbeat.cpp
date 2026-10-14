#include "SnapshotHeartbeat.h"
#include <stdexcept>

namespace iw::overlay::runtime {

SnapshotHeartbeat::SnapshotHeartbeat(unsigned long long generation, unsigned long long nowMilliseconds)
    : generation_(generation), lastAdvance_(nowMilliseconds) {}

bool SnapshotHeartbeat::Observe(unsigned long long generation, unsigned long long nowMilliseconds) {
    if (generation < generation_ || nowMilliseconds < lastAdvance_) {
        throw std::runtime_error("Snapshot generation or clock moved backwards");
    }
    if (generation > generation_) {
        generation_ = generation;
        lastAdvance_ = nowMilliseconds;
    }
    // An unchanged generation is stale only after the agreed three-second interval
    return nowMilliseconds - lastAdvance_ <= 3000;
}

}
