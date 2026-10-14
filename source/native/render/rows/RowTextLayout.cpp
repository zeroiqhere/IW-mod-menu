#include "RowTextLayout.h"
#include <algorithm>
#include <cmath>

namespace iw::overlay::rows {
RowTextLayout MakeRowTextLayout(const RowTextRequest &request) noexcept {
    if (!std::isfinite(request.bodyX) || !std::isfinite(request.bodyWidth) || !std::isfinite(request.scale) ||
        !std::isfinite(request.valueWidth) || request.bodyWidth <= 0 || request.scale <= 0 ||
        request.valueWidth < 0) {
        return {};
    }
    const float right = request.bodyX + request.bodyWidth;
    const float padding = 4 * request.scale;
    // Every supported placement must remain finite before clipping is applied
    if (!std::isfinite(right) || !std::isfinite(padding) || !std::isfinite(20 * request.scale)) {
        return {};
    }
    RowTextLayout result;
    result.labelLeft = request.bodyX + padding;
    result.labelRight = right - padding;
    result.valueRight = right - padding;
    result.valueLeft = result.valueRight;
    result.valueClipLeft = right;
    switch (request.placement) {
    case ValuePlacement::None:
        break;
    case ValuePlacement::Text:
        // The full measured value keeps its right anchor even when it is wider than
        // the body
        result.valueLeft = result.valueRight - request.valueWidth;
        result.valueClipLeft = std::max(request.bodyX, result.valueLeft - padding);
        result.labelRight = result.valueClipLeft;
        break;
    case ValuePlacement::BoxLeft:
        // The ten-unit left box ends at fourteen, followed by the existing
        // four-unit gap
        result.labelLeft = request.bodyX + 18 * request.scale;
        break;
    case ValuePlacement::BoxRight:
        // The established right box geometry leaves the same label boundary as
        // before
        result.labelRight = right - 20 * request.scale;
        break;
    default:
        return {};
    }
    if (!std::isfinite(result.labelLeft) || !std::isfinite(result.labelRight) ||
        !std::isfinite(result.valueLeft) || !std::isfinite(result.valueRight) ||
        !std::isfinite(result.valueClipLeft)) {
        return {};
    }
    // A wide value may leave no label room, but it never moves its declared glyph
    // origin
    result.labelLeft = std::clamp(result.labelLeft, request.bodyX, right);
    result.labelRight = std::clamp(result.labelRight, result.labelLeft, right);
    result.valueClipLeft = std::clamp(result.valueClipLeft, request.bodyX, right);
    result.valid = true;
    return result;
}
} // namespace iw::overlay::rows
