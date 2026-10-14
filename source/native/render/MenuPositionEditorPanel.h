#pragma once
#include "../shared/MenuSnapshot.h"
#include "layout/ApparitionLayout.h"
#include <imgui.h>

namespace iw::overlay {
// The reposition editor keeps the source title while its controls replace ordinary rows
void PrepareMenuPositionEditorText(const MenuSnapshot& snapshot, const layout::Hud& hud);
void DrawMenuPositionEditorPanel(ImDrawList& draw, const MenuSnapshot& snapshot, const layout::Hud& hud);
}
