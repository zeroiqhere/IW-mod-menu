#pragma once
#include <cstdint>

namespace iw::overlay {
// Signed coordinates preserve exact saved positions without a screen-size assumption
struct MenuPosition {
    std::int32_t x = 0;
    std::int32_t y = 85;
    bool operator==(const MenuPosition&) const = default;
};
// An absent saved position retains the source panel origin
constexpr MenuPosition DefaultMenuPosition() { return {0, 85}; }
}
