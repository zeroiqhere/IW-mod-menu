#pragma once
#include "../layout/ApparitionLayout.h"
#include <cstdint>
#include <optional>

namespace iw::overlay {
// Logical source colors stay separate from row redraws and graphics resources
class BasicScrollerHistory final {
public:
    std::optional<layout::Color> Update(std::optional<std::uint64_t> creationRevision,
        bool existingBasicScroller, std::optional<layout::Color> rainbowSample) noexcept;
private:
    std::optional<std::uint64_t> revision_;
    std::optional<layout::Color> color_;
    bool retired_ = false;
};
}
