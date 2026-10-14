#include "StealthLayoutTests.h"
#include "../../native/render/designs/DesignGeometry.h"
#include "../../native/render/layout/ApparitionLayout.h"
#include <cmath>
#include <stdexcept>

namespace iw::overlay::tests {
namespace {
void Require(bool value, const char *reason) {
    if (!value)
        throw std::runtime_error(reason);
}
void Equal(float actual, float expected) {
    Require(std::abs(actual - expected) < .01f, "Compact geometry differs");
}
layout::Request Model(MenuDesign design, std::size_t selected = 0) {
    layout::Request request{640, 480, 12, selected, 100, 3, false, 0};
    request.appearance.design = design;
    request.appearance.stealthUI = true;
    request.appearance.booleanDisplay = "Text";
    return request;
}
} // namespace
void AddStealthLayoutChecks(std::vector<std::pair<std::string, std::function<void()>>> &checks) {
    checks.emplace_back("Stealth shows five rows with each Design's existing body height", [] {
        for (auto design : {MenuDesign::Apparition, MenuDesign::Classic, MenuDesign::Native, MenuDesign::Aio,
                            MenuDesign::Basic}) {
            for (bool visible : {false, true})
                for (bool alternate : {false, true}) {
                    auto request = Model(design);
                    request.appearance.instructionsVisible = visible;
                    request.appearance.alternateInstructions = alternate;
                    const auto hud = layout::MakeHud(request);
                    Require(hud.capacity == 5 && hud.shown == 5 && hud.count == 12 && !hud.counterVisible,
                            "Stealth inherited ordinary capacity or counter");
                    Equal(hud.body.height, 90 + layout::MetricsFor(design).bodyExtra);
                }
        }
    });
    checks.emplace_back("Stealth retains absolute cursor identity and centers two rows on either side", [] {
        for (std::size_t selected = 0; selected < 12; ++selected) {
            const auto hud = layout::MakeHud(Model(MenuDesign::Apparition, selected));
            const auto expectedFirst = selected < 2 ? 0 : selected > 9 ? 7 : selected - 2;
            Require(hud.selected == selected && hud.first == expectedFirst && hud.shown == 5,
                    "Compact window changed selected identity or edge clamping");
            Equal(hud.scroller.y, hud.body.y + hud.scrollerTopOffset + 18 * (selected - expectedFirst));
        }
    });
    checks.emplace_back("Stealth preserves short-page row bounds and saved panel width", [] {
        for (std::size_t count : {std::size_t{0}, std::size_t{1}, std::size_t{3}, std::size_t{5}}) {
            auto request = Model(MenuDesign::Classic);
            request.rowCount = count;
            request.appearance.menuWidth = 500;
            const auto hud = layout::MakeHud(request);
            Require(hud.count == count && hud.shown == (count ? count : 1) && hud.capacity == 5,
                    "Compact short page invented logical options");
            Equal(hud.body.width, 500);
        }
    });
    checks.emplace_back("Direct Stealth layout callers cannot draw a counter from conflicting flags", [] {
        auto request = Model(MenuDesign::Native);
        request.appearance.optionCounter = true;
        const auto hud = layout::MakeHud(request);
        Require(hud.capacity == 5 && hud.shown == 5 && !hud.counterVisible,
                "Conflicting local flags exposed a counter in the compact window");
    });
    checks.emplace_back("Absent rebuild footer uses the saved instruction Y without a target Design", [] {
        for (auto design : {MenuDesign::Apparition, MenuDesign::Classic, MenuDesign::Native, MenuDesign::Aio,
                            MenuDesign::Basic}) {
            auto request = Model(design);
            request.appearance.adaptiveInstructions = true;
            request.menuHudPresence = layout::MenuHudPresence::Absent;
            const auto hud = layout::MakeHud(request);
            Equal(hud.instructionText.y, 432);
            Equal(hud.instructionText.x, layout::MetricsFor(design).footerX);
            request.appearance.instructionPosition = InstructionPosition{320, 300};
            Equal(layout::MakeHud(request).instructionText.y, 260);
        }
    });
}
} // namespace iw::overlay::tests
