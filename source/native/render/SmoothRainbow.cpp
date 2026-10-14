#include "SmoothRainbow.h"
#include <cmath>

namespace iw::overlay {
layout::Color SampleSmoothRainbow(std::uint64_t milliseconds) noexcept {
    // Reduce uptime before conversion so even the largest clock keeps its exact tick
    const auto tick = (milliseconds % 10000) / 10;
    const double hue = static_cast<double>(tick) * 0.001;
    const double scaled = hue * 6;
    const auto sector = static_cast<unsigned>(scaled);
    const double fraction = scaled - sector;
    constexpr double value = 0.95;
    double red = 0, green = 0, blue = 0;
    // These six linear sectors match the source producer rather than a sine wave
    switch (sector) {
        case 0: red = value; green = fraction * value; break;
        case 1: red = (1 - fraction) * value; green = value; break;
        case 2: green = value; blue = fraction * value; break;
        case 3: green = (1 - fraction) * value; blue = value; break;
        case 4: red = fraction * value; blue = value; break;
        default: red = value; blue = (1 - fraction) * value; break;
    }
    // Eight-bit output uses nearest rounding and never exceeds the source value
    return {static_cast<std::uint8_t>(std::lround(red * 255)),
        static_cast<std::uint8_t>(std::lround(green * 255)),
        static_cast<std::uint8_t>(std::lround(blue * 255)), 255};
}
}
