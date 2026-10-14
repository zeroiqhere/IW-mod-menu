#pragma once
#include "ApparitionLayout.h"

namespace iw::overlay::layout {
// Status stays on the viewport even when a saved menu position is outside it
Rect PlaceNoticeAroundMovedMenu(Rect preferred, Rect menu, Rect footer) noexcept;
}
