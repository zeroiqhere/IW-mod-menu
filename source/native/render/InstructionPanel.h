#pragma once
#include "../shared/MenuSnapshot.h"
#include "layout/ApparitionLayout.h"
#include "instructions/color/InstructionHudColorHistory.h"
#include <array>
#include <string_view>
struct ImDrawList;

namespace iw::overlay {
// Six bounded views and one fixed join buffer keep footer work independent of packet size
struct InstructionPanelContent {
    std::array<std::string_view, 6> lines{};
    std::array<char, 1562> joined{};
    std::size_t count = 0, joinedLength = 0;
    bool alternate = false;
    std::size_t LineCount() const noexcept { return alternate && count ? 1 : count; }
    std::string_view Line(std::size_t index) const noexcept;
};
InstructionPanelContent MakeInstructionContent(const MenuSnapshot& snapshot) noexcept;
float MeasureInstructionWidth(const InstructionPanelContent& content, const layout::Hud& hud);
float FitInstructionText(const InstructionPanelContent& content, const layout::Hud& hud);
void PrepareInstructionText(const InstructionPanelContent& content, const layout::Hud& hud, float font);
void DrawInstructionPanel(ImDrawList& draw, const InstructionPanelContent& content,
    const layout::Hud& hud, layout::Color theme, float font, bool rainbow = false,
    std::optional<layout::Color> fixedTheme = std::nullopt,
    std::optional<InstructionHudColors> retainedColors = std::nullopt);
}
