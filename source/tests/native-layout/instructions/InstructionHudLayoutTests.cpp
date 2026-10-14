#include "InstructionHudLayoutTests.h"
#include "../../../native/render/layout/ApparitionLayout.h"
#include "../../../native/render/designs/DesignGeometry.h"
#include <array>
#include <cmath>
#include <limits>
#include <stdexcept>

namespace iw::overlay::tests {
namespace {
void Require(bool value, const char* reason) {
    if (!value) throw std::runtime_error(reason);
}
}
void AddInstructionHudLayoutChecks(std::vector<std::pair<std::string, std::function<void()>>>& checks) {
    checks.emplace_back("Closed saved anchors remain separate from open absent adaptive geometry across all designs", [] {
        const std::array<MenuDesign, 5> designs{MenuDesign::Apparition, MenuDesign::Classic, MenuDesign::Native, MenuDesign::Aio, MenuDesign::Basic};
        const std::array<std::optional<InstructionPosition>, 4> anchors{std::nullopt, InstructionPosition{80, 420},
            InstructionPosition{std::numeric_limits<std::int32_t>::min(), std::numeric_limits<std::int32_t>::max()},
            InstructionPosition{std::numeric_limits<std::int32_t>::max(), std::numeric_limits<std::int32_t>::min()}};
        for (auto design : designs) for (auto viewport : {layout::Point{640,480}, layout::Point{320,240}, layout::Point{160,120}})
            for (int width : {200,260,500}) for (auto anchor : anchors) for (bool alternate : {false,true})
                for (bool background : {false,true}) for (bool adaptive : {false,true}) {
                    layout::Request closed{viewport.x,viewport.y,12,4,150,3};
                    closed.appearance.design = design; closed.appearance.menuWidth = width;
                    closed.appearance.instructionPosition = anchor; closed.appearance.alternateInstructions = alternate;
                    closed.appearance.instructionsBackground = background; closed.appearance.adaptiveInstructions = adaptive;
                    closed.closedInstructions = true;
                    auto saved = closed; saved.appearance.adaptiveInstructions = false;
                    const auto a = layout::MakeHud(closed), b = layout::MakeHud(saved);
                    Require(a.visible && b.visible && std::isfinite(a.instructionText.x) && std::isfinite(a.instructionText.y),
                        "Supported closed footer geometry became invalid");
                    Require(a.instructions.x == b.instructions.x && a.instructions.y == b.instructions.y
                        && a.instructions.width == b.instructions.width && a.instructionAnchor.x == b.instructionAnchor.x,
                        "Closed adaptive help changed the saved source anchor");
                    if (adaptive && !alternate && !anchor) {
                        auto initial = closed; initial.closedInstructions = false;
                        initial.menuHudPresence = layout::MenuHudPresence::Absent;
                        const auto gap = layout::MakeHud(initial);
                        const auto metrics = layout::MetricsFor(design);
                        Require(std::abs(gap.instructions.x - metrics.footerX * gap.scale) < .01f
                            && gap.instructions.y == a.instructions.y && gap.instructions.width != a.instructions.width,
                            "Open absent-HUD help lost its distinct adaptive X and minimum width");
                    }
                }
    });
}
}
