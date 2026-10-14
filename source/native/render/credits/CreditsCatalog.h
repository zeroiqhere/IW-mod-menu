#pragma once
#include <array>
#include <span>
#include <string_view>

namespace iw::overlay::credits {
struct Entry {
    std::string_view text;
    bool title = false;
    int launchSixths = 0;
    bool separator = false;
};
// Rational sixths preserve the source five-sixths cadence without accumulated rounding
std::span<const Entry, 26> Catalog() noexcept;
struct TextRun { std::string_view text; bool red = false; };
struct TextRuns { std::array<TextRun, 8> runs{}; std::size_t count = 0; };
// Only the source red marker receives styling; other characters remain ordinary text
TextRuns SplitText(std::string_view text) noexcept;
}
