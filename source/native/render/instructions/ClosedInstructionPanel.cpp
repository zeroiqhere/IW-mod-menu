#include "ClosedInstructionPanel.h"
#include "InstructionHudPresence.h"
#include <imgui.h>
namespace iw::overlay {
layout::Hud InstructionPaintHud(layout::Hud hud, const MenuSnapshot& snapshot) noexcept {
    if (HasOwnedInstructionHud(snapshot)) {
        // Creation styles affect existing paint; current appearance still owns geometry
        hud.design = *snapshot.instructionHud->creationDesign;
        hud.instructionsBackground = *snapshot.instructionHud->creationBackground;
        hud.instructionsCentered = *snapshot.instructionHud->creationAlternate && hud.instructionsBackground;
    }
    return hud;
}
bool DrawClosedInstructionPanel(const MenuSnapshot& snapshot,
    std::optional<layout::Color> presentationColor, bool admitted, std::optional<InstructionHudColors> retainedColors) {
    if (snapshot.isOpen || !admitted || !HasOwnedInstructionHud(snapshot)) return false;
    const auto content = MakeInstructionContent(snapshot);
    if (!content.LineCount()) return false;
    const auto viewport = ImGui::GetIO().DisplaySize;
    layout::Request request{viewport.x, viewport.y, 0, 0, 260, content.LineCount(), false, 0, snapshot.appearance};
    // Closed placement uses saved coordinates even when adaptive help is enabled
    request.closedInstructions = true;
    auto hud = layout::MakeHud(request);
    if (!hud.visible) return false;
    request.instructionWidth = MeasureInstructionWidth(content, hud);
    hud = layout::MakeHud(request);
    const auto font = FitInstructionText(content, hud);
    PrepareInstructionText(content, hud, font);
    const auto theme = presentationColor.value_or(layout::Theme(snapshot.accent));
    DrawInstructionPanel(*ImGui::GetBackgroundDrawList(), content, InstructionPaintHud(hud, snapshot),
        theme, font, snapshot.smoothRainbowTheme, layout::Theme(snapshot.accent), retainedColors);
    return true;
}
}
