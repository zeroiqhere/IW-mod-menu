#pragma once
#include "../shared/MenuSnapshot.h"
#include "layout/ApparitionLayout.h"
#include "ScrollPresentation.h"
#include "chrome/theme/ChromeColorFrame.h"
#include "instructions/color/InstructionHudColorHistory.h"
#include <optional>

namespace iw::overlay {
struct PanelDrawSummary {
    bool noticeContinues = false;
    std::size_t firstRow = 0, shownRows = 0;
};
// Draw only the immutable presentation model into the current ImGui frame
bool DrawApparitionPanel(const MenuSnapshot& snapshot, PanelDrawSummary* summary = nullptr,
    std::optional<float> presentationWidth = std::nullopt,
    std::optional<layout::Color> presentationColor = std::nullopt,
    const ScrollFrame* presentationRows = nullptr,
    std::optional<double> presentationCreditsElapsed = std::nullopt,
    std::optional<layout::Color> basicScrollerColor = std::nullopt,
    bool instructionHudAdmitted = true,
    std::optional<InstructionHudColors> instructionColors = std::nullopt,
    const ChromeColorFrame* chromeColors = nullptr);
}
