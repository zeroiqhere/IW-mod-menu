#include "InstructionHudPanelTests.h"
#include "../palette/PalettePanel.h"
#include "../../../native/render/InstructionPanel.h"
#include <algorithm>
#include "../../../native/render/instructions/InstructionHudLifetime.h"

namespace iw::overlay::tests {
namespace {
MenuSnapshot Owned(bool open = false) {
    auto state = palette::PaletteModel(); state.isOpen = open;
    state.localMenuEligibility = LocalMenuEligibility{7, PlayerLifeState::Alive, true, false, false};
    state.instructionHud = InstructionHud{true, 2, 7, MenuDesign::Apparition, false, true,
        {"RMB + E: Open Main Menu", "RMB + G: Open Quick Menu"}};
    return state;
}
}
void AddInstructionHudPanelChecks(std::vector<std::pair<std::string, std::function<void()>>>& checks) {
    using palette::RequirePalette;
    checks.emplace_back("Independent footer lifetime survives graphics-style withdrawal but rejects stale creation", [] {
        InstructionHudLifetime owner; auto state = Owned();
        RequirePalette(owner.Observe(&state), "First verified creation was not observed");
        RequirePalette(!owner.Observe(nullptr), "Missing snapshot kept actionable ownership");
        auto hidden = state; hidden.instructionHud = InstructionHud{};
        RequirePalette(!owner.Observe(&hidden) && owner.Observe(&state), "Focus withdrawal erased a valid retained lifetime");
        state.instructionHud->creationBackground = false;
        RequirePalette(!owner.Observe(&state), "Same creation silently changed widget style");
        state.instructionHud->creationRevision = 3;
        RequirePalette(owner.Observe(&state), "A new creation could not replace quarantined history");
        state.instructionHud->creationRevision = 2;
        RequirePalette(!owner.Observe(&state), "Regressing creation reused retained history");
        state.instructionHud->creationRevision = 3;
        RequirePalette(!owner.Observe(&state), "A quarantined creation recovered without a new owner event");
        state.instructionHud->creationRevision = 4;
        RequirePalette(owner.Observe(&state), "New creation failed after observed rollback");
        state.localMenuEligibility->hasMenu = false;
        RequirePalette(!owner.Observe(&state), "Revoked permission retained footer ownership");
    });
    checks.emplace_back("Closed verified instructions draw only the persistent footer", [] {
        palette::PalettePanel panel; auto state = Owned();
        state.title = "FORBIDDEN TITLE"; state.notice = "FORBIDDEN STATUS";
        const auto& data = panel.Draw(state);
        RequirePalette(data.TotalVtxCount > 0, "Verified closed footer emitted no vertices");
        for (const auto* list : data.CmdLists) for (const auto& vertex : list->VtxBuffer) {
            RequirePalette(vertex.pos.y >= 440, "Closed footer emitted menu rows, chrome or status");
        }
        RequirePalette(palette::HasGlyphColor(data, IM_COL32_WHITE, {0, 440, 640, 40}),
            "Closed saved-binding hints were not drawn");
    });
    checks.emplace_back("Owned absence and unknown admission suppress footer without granting closed legacy drawing", [] {
        palette::PalettePanel panel;
        for (bool open : {false, true}) {
            auto state = Owned(open); state.instructionHud = InstructionHud{};
            const auto& data = panel.Draw(state);
            RequirePalette(!palette::HasGlyphColor(data, IM_COL32_WHITE, {0, 440, 640, 40}),
                "Explicitly absent footer remained visible");
        }
        auto state = Owned(); state.instructionHud.reset();
        RequirePalette(panel.Draw(state).TotalVtxCount == 0, "Legacy closed state invented independent instructions");
        state = Owned(); state.localMenuEligibility->life = PlayerLifeState::Unknown;
        RequirePalette(panel.Draw(state).TotalVtxCount == 0, "Unknown actor created closed footer ownership");
        state = Owned(); state.localMenuEligibility->revision = 8;
        RequirePalette(panel.Draw(state).TotalVtxCount == 0, "Stale admission created closed footer ownership");
    });
    checks.emplace_back("Closed adaptive footer stays at the saved anchor while creation paint is independent", [] {
        palette::PalettePanel panel; auto state = Owned();
        state.appearance.adaptiveInstructions = true;
        state.appearance.instructionPosition = InstructionPosition{80, 420};
        state.appearance.design = MenuDesign::Basic;
        state.instructionHud->creationDesign = MenuDesign::Classic;
        const auto& data = panel.Draw(state);
        RequirePalette(palette::HasGlyphColor(data, IM_COL32_WHITE, {80, 390, 384, 30}),
            "Closed adaptive instructions followed menu chrome instead of the saved anchor");
        for (const auto* list : data.CmdLists) for (const auto& vertex : list->VtxBuffer)
            RequirePalette(vertex.pos.y > 380 && vertex.pos.x >= 79, "Closed instructions used adaptive open placement");
    });
}
}
