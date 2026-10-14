#pragma once
#include "../layout/ApparitionLayout.h"
#include "../../shared/MenuSnapshot.h"
#include <string>
#include "../chrome/theme/ChromeColorFrame.h"
struct ImDrawList;

namespace iw::overlay {
struct FittedDesignTitle { std::string text; float font, width, available; };
// Keep substitute titles readable without moving the source counter anchor
FittedDesignTitle FitDesignTitle(const MenuSnapshot& snapshot, const layout::Hud& hud);
// Secondary labels identify the local IW presentation, never a fabricated access level
void PrepareDesignText(const MenuSnapshot& snapshot, const layout::Hud& hud);
void DrawDesignTitles(ImDrawList& draw, const MenuSnapshot& snapshot, const layout::Hud& hud, layout::Color accentText, const ChromeColorFrame* ownedColors = nullptr);
void DrawDesignCounter(ImDrawList& draw, const layout::Hud& hud);
}
