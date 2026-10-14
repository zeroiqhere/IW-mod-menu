#include "DesignLayoutTests.h"
#include "../../native/render/layout/ApparitionLayout.h"
#include <array>
#include <cmath>
#include <limits>
#include <stdexcept>

namespace iw::overlay::tests {
namespace {
using namespace layout;
void Require(bool result, const char* message) { if (!result) throw std::runtime_error(message); }
void Equal(float actual, float expected) { Require(std::abs(actual - expected) < .01f, "A design reference coordinate differs"); }
constexpr std::array<MenuDesign, 5> Designs{MenuDesign::Apparition, MenuDesign::Classic, MenuDesign::Native, MenuDesign::Aio, MenuDesign::Basic};
Request Model(MenuDesign design, std::size_t rows = 8) {
    Request request{640, 480, rows, 0}; request.appearance.design = design; return request;
}
void Finite(Rect rectangle) {
    Require(std::isfinite(rectangle.x) && std::isfinite(rectangle.y) && std::isfinite(rectangle.width)
        && std::isfinite(rectangle.height), "A design produced nonfinite geometry");
}
}
void AddDesignLayoutChecks(std::vector<std::pair<std::string, std::function<void()>>>& checks) {
    checks.emplace_back("Five designs use source body row and scroller formulas", [] {
        const std::array<float, 5> extra{0, 7, 0, 13, 22}, rowOffset{8, 11, 8, 15, 30}, scrollOffset{0, 3, 0, 4, 22};
        for (std::size_t design = 0; design < Designs.size(); ++design) for (std::size_t rows : {1u, 8u, 10u, 14u}) {
            auto request = Model(Designs[design], rows);
            // Fourteen rows use the source non-box, hidden-instructions capacity
            if (rows == 14) { request.appearance.booleanDisplay = "Text"; request.appearance.instructionsVisible = false; }
            const auto hud = MakeHud(request); Equal(hud.body.height, 18 * rows + extra[design]);
            Equal(hud.OptionCenter(0).y, 85 + rowOffset[design]); Equal(hud.scroller.y, 85 + scrollOffset[design]);
            Equal(hud.scroller.width, design == 3 ? 2 : 260); Equal(hud.scroller.height, design == 3 ? 23 : 18);
            Require(hud.design == Designs[design], "The design identity was discarded");
        }
    });
    checks.emplace_back("Classic Native AIO and Basic retain distinct header and secondary anchors", [] {
        const auto classic = MakeHud(Model(MenuDesign::Classic));
        Equal(classic.banner.x, -1); Equal(classic.banner.y, 72); Equal(classic.banner.width, 262);
        Equal(classic.banner.height, classic.body.height + 14); Equal(classic.title.x, 4); Equal(classic.title.y, 78);
        Require(!classic.titleCentered, "Classic title was centered");
        const auto native = MakeHud(Model(MenuDesign::Native));
        Equal(native.banner.y, 30); Equal(native.banner.height, 38); Equal(native.separator.y, 68); Equal(native.separator.height, 17);
        Equal(native.title.y, 75.5f); Equal(native.secondaryTitle.x, 130); Equal(native.secondaryTitle.y, 49);
        Require(native.secondaryVisible && !native.titleCentered, "Native lost its separate brand");
        const auto aio = MakeHud(Model(MenuDesign::Aio));
        Equal(aio.separator.y, 60); Equal(aio.separator.height, 25); Equal(aio.bottomLine.height, 25);
        Equal(aio.outer.x, -2); Equal(aio.outer.y, 58); Equal(aio.outer.width, 264); Equal(aio.outer.height, aio.body.height + 54);
        Equal(aio.title.y, 71.5f); Equal(aio.secondaryTitle.y, aio.body.Bottom() + 11.5f);
        Require(aio.secondaryVisible, "AIO lost its lower status label");
        const auto basic = MakeHud(Model(MenuDesign::Basic));
        Equal(basic.banner.y, 85); Equal(basic.banner.height, 20); Equal(basic.title.y, 94);
        Require(basic.titleCentered && !basic.secondaryVisible, "Basic reused another design header");
    });
    checks.emplace_back("Only supported design counters occupy their source header or footer positions", [] {
        for (const auto design : Designs) {
            auto request = Model(design); request.appearance.optionCounter = true;
            const auto hud = MakeHud(request);
            Require(hud.counterVisible == (design != MenuDesign::Aio && design != MenuDesign::Basic),
                "AIO or Basic displayed an unsupported counter");
            if (design == MenuDesign::Apparition) Equal(hud.counter.y, hud.body.Bottom() + 8);
            if (design == MenuDesign::Classic || design == MenuDesign::Native) Equal(hud.counter.y, hud.title.y);
            Require(hud.counterAlpha == (design == MenuDesign::Classic ? 255 : 179), "Counter opacity differs from the source");
        }
    });
    checks.emplace_back("Every design preserves exact modal body sizes and width-only title removal", [] {
        for (const auto design : Designs) for (unsigned modal = 0; modal < 4; ++modal) {
            auto request = Model(design); request.widthEditor = modal == 0; request.positionEditor = modal == 1;
            request.menuPositionEditor = modal == 2; request.menuCredits = modal == 3;
            const auto hud = MakeHud(request); const std::array<float, 4> heights{120, 20, 122, 220};
            Equal(hud.body.height, heights[modal]); Require(!hud.hasSelection && !hud.counterVisible, "Modal retained selection or counter");
            Require(hud.titleVisible == (modal != 0), "A modal incorrectly removed or retained the title");
            Require(hud.secondaryVisible == (modal != 0 && (design == MenuDesign::Native || design == MenuDesign::Aio)),
                "Width editing retained a brand or status, or another modal removed it");
            if (design == MenuDesign::Aio) { Equal(hud.bottomLine.y, hud.body.Bottom() - 1); Equal(hud.outer.height, hud.body.height + 53); }
        }
    });
    checks.emplace_back("Adaptive instructions follow each design's exact source offsets and minimum width", [] {
        const std::array<float, 5> dx{1, 0, 1, -1, 0}, dy{18, 5, 5, 30, 5}, dw{-2, 0, -2, 2, 0};
        for (std::size_t index = 0; index < Designs.size(); ++index) {
            auto request = Model(Designs[index]); request.instructionWidth = 100; request.appearance.adaptiveInstructions = true;
            const auto hud = MakeHud(request); Equal(hud.instructions.x, dx[index]);
            Equal(hud.instructions.y, hud.body.Bottom() + dy[index]); Equal(hud.instructions.width, 260 + dw[index]);
        }
    });
    checks.emplace_back("Five moved or extreme design origins remain finite with bounded readable status", [] {
        for (const auto design : Designs) for (const auto point : {MenuPosition{37, 126}, MenuPosition{-80, 450},
            MenuPosition{std::numeric_limits<std::int32_t>::max(), std::numeric_limits<std::int32_t>::min()}}) {
            auto request = Model(design); request.notice = true; request.appearance.menuPosition = point;
            const auto hud = MakeHud(request);
            for (const auto rectangle : {hud.body, hud.banner, hud.separator, hud.outer, hud.chromeBounds, hud.instructions, hud.notice}) Finite(rectangle);
            Require(hud.notice.x >= 0 && hud.notice.y >= 0 && hud.notice.Right() <= 640 && hud.notice.Bottom() <= 480,
                "The IW status rode offscreen with a saved design origin");
        }
    });
}
}
