#pragma once
#include <cstddef>
#include <string_view>

namespace iw::overlay {
// Bound work while keeping a complete UTF-8 prefix for player and option names
std::string_view BoundedPanelText(std::string_view text, std::size_t limit) noexcept;
}
