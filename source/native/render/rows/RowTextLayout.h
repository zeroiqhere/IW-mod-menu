#pragma once

namespace iw::overlay::rows {
// Text values and fixed boxes reserve different ink at the existing source
// anchors
enum class ValuePlacement { None, Text, BoxLeft, BoxRight };
struct RowTextRequest {
    float bodyX = 0, bodyWidth = 0, scale = 0, valueWidth = 0;
    ValuePlacement placement = ValuePlacement::None;
};
struct RowTextLayout {
    bool valid = false;
    float labelLeft = 0, labelRight = 0;
    float valueLeft = 0, valueRight = 0, valueClipLeft = 0;
};
// Invalid geometry yields no text bounds instead of allowing nonfinite draw
// coordinates
RowTextLayout MakeRowTextLayout(const RowTextRequest &request) noexcept;
} // namespace iw::overlay::rows
