#pragma once
#include "ScrollPresentation.h"
#include "../shared/MenuSnapshot.h"
#include "layout/ApparitionLayout.h"
#include <imgui.h>

namespace iw::overlay {
ScrollRequest MakeScrollRequest(const MenuSnapshot& snapshot, float width, float height,
    std::optional<float> presentationWidth = std::nullopt) noexcept;
// Foreign or malformed presentation frames fall back to current confirmed layout
bool MatchesScrollFrame(const ScrollFrame& frame, const layout::Hud& hud, const ScrollRequest& request) noexcept;
void PrepareScrollRows(const MenuSnapshot& snapshot, const layout::Hud& hud, const ScrollFrame& frame);
void DrawScrollRows(ImDrawList& draw, const MenuSnapshot& snapshot, const layout::Hud& hud,
    const ScrollFrame& frame, layout::Color theme);
}
