#pragma once
#include "layout/ApparitionLayout.h"
#include <cstdint>

namespace iw::overlay {
// The clock is explicit so source colors can be checked without a graphics device
layout::Color SampleSmoothRainbow(std::uint64_t milliseconds) noexcept;
}
