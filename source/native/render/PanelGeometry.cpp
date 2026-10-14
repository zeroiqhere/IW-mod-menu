#include "PanelGeometry.h"
#include <algorithm>
#include <cmath>

namespace iw::overlay {
layout::Rect VisiblePanelRect(layout::Rect rectangle, float width, float height) noexcept {
    if (!std::isfinite(width) || !std::isfinite(height) || width <= 0 || height <= 0
        || !std::isfinite(rectangle.x) || !std::isfinite(rectangle.y)
        || !std::isfinite(rectangle.width) || !std::isfinite(rectangle.height)) return {};
    // Both corners are bounded before subtraction, even when an anchor is far offscreen
    const float x = std::clamp(rectangle.x, 0.0f, width), y = std::clamp(rectangle.y, 0.0f, height);
    return {x, y, std::max(0.0f, std::clamp(rectangle.Right(), 0.0f, width) - x),
        std::max(0.0f, std::clamp(rectangle.Bottom(), 0.0f, height) - y)};
}
}
