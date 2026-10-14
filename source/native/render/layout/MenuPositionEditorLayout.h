#pragma once
#include "ApparitionLayout.h"

namespace iw::overlay::layout {
struct MenuPositionTextLayout {
    Point first, offset;
    float lineAdvance = 0;
};
// Source anchors stay fixed while substitute native font spacing remains explicit
MenuPositionTextLayout MakeMenuPositionTextLayout(const Hud& hud) noexcept;
}
