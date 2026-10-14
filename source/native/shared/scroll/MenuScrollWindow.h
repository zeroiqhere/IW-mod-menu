#pragma once
#include "SpacerScrollReceipt.h"
#include <cstddef>
#include <optional>

namespace iw::overlay {
// Shared integer math keeps input planning and painted windows on the same source boundary
inline std::optional<std::size_t> MenuScrollWindowFirst(std::size_t rowCount, std::size_t capacity,
                                                        std::size_t selected) noexcept {
    if (rowCount > MaximumSpacerScrollRows || capacity == 0 || capacity > 14 ||
        (rowCount == 0 ? selected != 0 : selected >= rowCount)) {
        return std::nullopt;
    }
    if (rowCount <= capacity) {
        return 0;
    }
    const auto above = (capacity - 1) / 2;
    const auto below = (capacity + 1) / 2;
    return selected <= above ? 0 : selected >= rowCount - below ? rowCount - capacity : selected - above;
}
} // namespace iw::overlay
