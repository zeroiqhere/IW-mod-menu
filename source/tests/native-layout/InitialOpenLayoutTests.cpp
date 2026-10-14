#include "InitialOpenLayoutTests.h"
#include "../../native/render/layout/ApparitionLayout.h"
#include "../../native/render/designs/DesignGeometry.h"
#include <array>
#include <cmath>
#include <limits>
#include <stdexcept>

namespace iw::overlay::tests {
namespace {
using namespace layout;
void Require(bool value, const char* reason) { if (!value) throw std::runtime_error(reason); }
void Equal(float actual, float expected) { Require(std::abs(actual - expected) < .01f, "Initial HUD footer coordinate differs"); }
constexpr std::array<MenuDesign, 5> Designs{MenuDesign::Apparition, MenuDesign::Classic, MenuDesign::Native, MenuDesign::Aio, MenuDesign::Basic};
Request Model(MenuDesign design) {
    Request request{640, 480, 10, 0, 100, 3, true, 5}; request.appearance.design = design;
    request.appearance.adaptiveInstructions = true; request.appearance.menuPosition = MenuPosition{17, 200};
    request.appearance.instructionPosition = InstructionPosition{300, 460}; return request;
}
}
void AddInitialOpenLayoutChecks(std::vector<std::pair<std::string, std::function<void()>>>& checks) {
    checks.emplace_back("Missing HUD adaptive help retains design X and width while using saved instruction Y", [] {
        for (const auto design : Designs) for (const int width : {200, 260, 500}) {
            auto request = Model(design); request.appearance.menuWidth = width; request.menuHudPresence = MenuHudPresence::Absent;
            const auto hud = MakeHud(request); const auto metrics = MetricsFor(design);
            Equal(hud.instructionText.x, 17 + metrics.footerX); Equal(hud.instructionText.y, 420);
            Equal(hud.instructions.width, width + metrics.footerWidth);
        }
    });
    checks.emplace_back("Present HUD restores adaptive body bottom offsets for empty single and populated menus", [] {
        for (const auto design : Designs) for (const std::size_t count : {0u, 1u, 10u}) {
            auto request = Model(design); request.rowCount = count; const auto hud = MakeHud(request);
            Equal(hud.instructionText.y, hud.body.Bottom() + MetricsFor(design).footerY);
            request.menuHudPresence = MenuHudPresence::Absent;
            Equal(MakeHud(request).instructionText.y, 420);
        }
    });
    checks.emplace_back("Missing HUD does not change nonadaptive saved or alternate centered anchors", [] {
        for (const bool alternate : {false, true}) {
            auto request = Model(MenuDesign::Apparition); request.appearance.adaptiveInstructions = false;
            request.appearance.alternateInstructions = alternate; const auto present = MakeHud(request);
            request.menuHudPresence = MenuHudPresence::Absent; const auto absent = MakeHud(request);
            Equal(present.instructions.x, absent.instructions.x); Equal(present.instructions.y, absent.instructions.y);
            Equal(present.instructionText.x, absent.instructionText.x); Equal(present.instructionText.y, absent.instructionText.y);
        }
    });
    checks.emplace_back("Missing HUD respects hidden help and plain text without changing saved preferences", [] {
        auto request = Model(MenuDesign::Native); request.menuHudPresence = MenuHudPresence::Absent;
        request.appearance.instructionsBackground = false; auto hud = MakeHud(request);
        Require(hud.instructionsVisible && hud.outline.width == 0 && hud.instructionText.height == 40,
            "Plain initial help was removed or its outline remained");
        request.appearance.instructionsVisible = false; hud = MakeHud(request);
        Require(!hud.instructionsVisible && hud.outline.width == 0 && request.appearance.adaptiveInstructions,
            "Hidden initial help altered the saved adaptive preference");
    });
    checks.emplace_back("Extreme saved and menu anchors stay finite with readable viewport status during missing HUD", [] {
        for (const auto size : {Point{320, 240}, Point{960, 720}, Point{480, 960}}) {
            auto request = Model(MenuDesign::Aio); request.width = size.x; request.height = size.y;
            request.menuHudPresence = MenuHudPresence::Absent;
            request.appearance.menuPosition = MenuPosition{std::numeric_limits<std::int32_t>::max(), std::numeric_limits<std::int32_t>::min()};
            request.appearance.instructionPosition = InstructionPosition{std::numeric_limits<std::int32_t>::min(), std::numeric_limits<std::int32_t>::max()};
            const auto hud = MakeHud(request);
            Require(std::isfinite(hud.instructionText.x) && std::isfinite(hud.instructionText.y)
                && hud.notice.x >= 0 && hud.notice.y >= 0 && hud.notice.Right() <= size.x && hud.notice.Bottom() <= size.y,
                "An initial offscreen anchor produced nonfinite geometry or hid status");
        }
    });
}
}
