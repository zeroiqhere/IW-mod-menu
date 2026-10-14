#pragma once
#include "CreditsTimeline.h"
#include "../layout/ApparitionLayout.h"
#include <imgui.h>

namespace iw::overlay::credits {
// Preparation loads every live line before the panel emits triangles
void PreparePanel(const Frame& frame, const layout::Hud& hud);
void DrawPanel(ImDrawList& draw, const Frame& frame, const layout::Hud& hud);
}
