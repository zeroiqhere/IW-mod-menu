#pragma once
#include <cstdint>

namespace iw::overlay {
// Coordinates use the source's reference space rather than desktop pixels
struct InstructionPosition {
    std::int32_t x = 255;
    std::int32_t y = 472;
    bool operator==(const InstructionPosition&) const = default;
};

// A missing saved position follows the selected source instruction style
inline InstructionPosition DefaultInstructionPosition(bool alternate) {
    return {alternate ? 320 : 255, 472};
}
}
