#pragma once
#include <cstdint>

namespace iw::overlay {
// Opening describes a local row fade and never pauses menu input
struct MenuOpening {
    // A producer must supply a positive event identity before this state is active
    std::uint64_t revision = 0;
    int elapsedMilliseconds = 0;
    bool operator==(const MenuOpening&) const = default;
};
}
