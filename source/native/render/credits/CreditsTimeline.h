#pragma once
#include <array>
#include <cstddef>

namespace iw::overlay::credits {
inline constexpr std::size_t MaximumLiveLines = 9;
struct Line {
    std::size_t index = 0;
    float center = 0, opacity = 0, fontScale = 1;
};
struct Frame { std::array<Line, MaximumLiveLines> lines{}; std::size_t count = 0; };
// Reported elapsed time owns the sequence; this sampler never closes a menu
Frame Sample(double elapsedMilliseconds) noexcept;
}
