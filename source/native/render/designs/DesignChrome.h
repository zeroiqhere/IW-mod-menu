#pragma once
#include <optional>
#include "../chrome/theme/ChromeColorFrame.h"
#include "../layout/ApparitionLayout.h"
struct ImDrawList;

namespace iw::overlay {
// Chrome only draws source rectangles; text and shared options remain separate
void DrawDesignChrome(ImDrawList& draw, const layout::Hud& hud, layout::Color theme, bool rainbow, std::optional<layout::Color> basicScrollerColor = std::nullopt, const ChromeColorFrame* ownedColors = nullptr);
}
