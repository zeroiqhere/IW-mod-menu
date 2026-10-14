#pragma once
#include "ApparitionLayout.h"

namespace iw::overlay::layout {
// Moving the footer never moves the saved anchor to make room for status text
Rect PlaceNoticeAwayFromInstructions(Rect preferred, Rect below, Rect side, Rect footer) noexcept;
}
