#pragma once
#include "../shared/MenuSnapshot.h"
#include <stdexcept>
namespace iw::overlay::runtime {
struct ScrollSpacerCounts {
    unsigned up = 0, down = 0;
};
inline ScrollSpacerCounts CountScrollSpacers(const MenuSnapshot &snapshot) {
    const auto size = snapshot.rows.size();
    if (!size) {
        return {};
    }
    if (size > 128 || snapshot.selectedIndex >= size || snapshot.rows[snapshot.selectedIndex].isSpacer) {
        throw std::invalid_argument("Spacer cadence requires a valid final selection");
    }
    const auto count = [&](bool up) {
        auto index = snapshot.selectedIndex;
        unsigned skipped = 0;
        for (std::size_t visited = 1; visited < size; ++visited) {
            index = up ? (index ? index - 1 : size - 1) : (index + 1 == size ? 0 : index + 1);
            if (!snapshot.rows[index].isSpacer) {
                break;
            }
            ++skipped;
        }
        return skipped;
    };
    // Disabled ordinary rows are not source blanks and are never counted here
    return {count(true), count(false)};
}
} // namespace iw::overlay::runtime
