#pragma once

namespace iw::overlay::runtime {

// A generation advances even while the owner is idle or waiting on an operation
class SnapshotHeartbeat {
public:
    SnapshotHeartbeat(unsigned long long generation, unsigned long long nowMilliseconds);
    bool Observe(unsigned long long generation, unsigned long long nowMilliseconds);
private:
    unsigned long long generation_;
    unsigned long long lastAdvance_;
};

}
