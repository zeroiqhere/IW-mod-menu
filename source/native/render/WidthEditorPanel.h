#pragma once
#include "../shared/MenuSnapshot.h"
#include "layout/ApparitionLayout.h"
#include <imgui.h>

namespace iw::overlay {
// Prepare editor glyphs before any triangle is emitted into the current frame
void PrepareWidthEditorText(const MenuSnapshot& snapshot, const layout::Hud& hud);
// The editor owns its body instructions and offset, never ordinary option rows
void DrawWidthEditorPanel(ImDrawList& draw, const MenuSnapshot& snapshot, const layout::Hud& hud);
}
