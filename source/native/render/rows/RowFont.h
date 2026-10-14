#pragma once
#include <algorithm>
#include <cmath>

namespace iw::overlay::rows {
// Preserve sampled source growth while keeping every baked font at least one
// pixel
inline float RowFontPixels(float base, bool large, float selection) noexcept {
    if (!std::isfinite(base) || !std::isfinite(selection) || base <= 0) {
        return 1;
    }
    const float font = base * (large ? 1 + .2f * std::clamp(selection, 0.0f, 1.0f) : 1);
    return std::isfinite(font) ? std::max(1.0f, font) : 1;
}
} // namespace iw::overlay::rows
