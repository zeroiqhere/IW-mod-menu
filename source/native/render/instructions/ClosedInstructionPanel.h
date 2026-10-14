#pragma once
#include "../InstructionPanel.h"
namespace iw::overlay {
layout::Hud InstructionPaintHud(layout::Hud hud, const MenuSnapshot& snapshot) noexcept;
// This path emits only the independent footer, never ordinary menu or status widgets
bool DrawClosedInstructionPanel(const MenuSnapshot& snapshot,
    std::optional<layout::Color> presentationColor = std::nullopt, bool admitted = true,
    std::optional<InstructionHudColors> retainedColors = std::nullopt);
}
