#pragma once
#include "layout/ApparitionLayout.h"

namespace iw::overlay {
// Saved positions remain unchanged while emitted drawing uses their visible intersection
layout::Rect VisiblePanelRect(layout::Rect rectangle, float width, float height) noexcept;
}
