#include "DesignFooterLayoutTests.h"
#include "../../native/render/layout/ApparitionLayout.h"
#include "../../native/render/designs/DesignGeometry.h"
#include <array>
#include <cmath>
#include <limits>
#include <stdexcept>

namespace iw::overlay::tests {
namespace {
using namespace layout;
constexpr std::array<MenuDesign, 5> Designs{MenuDesign::Apparition, MenuDesign::Classic, MenuDesign::Native,
                                            MenuDesign::Aio, MenuDesign::Basic};
void Require(bool value, const char* reason) {
    if (!value) {
        throw std::runtime_error(reason);
    }
}
void Equal(float actual, float expected) {
    Require(std::abs(actual - expected) < .01f, "Design footer coordinate differs");
}
Request Model(MenuDesign captured, MenuDesign target) {
    Request request{640, 480, 10, 4, 100, 3, true, 5};
    request.appearance.design = captured;
    request.appearance.adaptiveInstructions = true;
    request.menuHudPresence = MenuHudPresence::Absent;
    request.instructionGeometryDesign = target;
    return request;
}
} // namespace
void AddDesignFooterLayoutChecks(std::vector<std::pair<std::string, std::function<void()>>>& checks) {
    checks.emplace_back("All twenty-five Design pairs use target adaptive offsets during the absent HUD gap", [] {
        for (auto old : Designs) {
            for (auto target : Designs) {
                for (int width : {200, 260, 500}) {
                    auto request = Model(old, target);
                    request.appearance.menuWidth = width;
                    const auto hud = MakeHud(request);
                    const auto metrics = MetricsFor(target);
                    Equal(hud.instructionText.x, metrics.footerX);
                    Equal(hud.instructions.width, width + metrics.footerWidth);
                    Equal(hud.instructionText.y, 432);
                    Require(hud.design == old, "Geometry override replaced captured paint Design");
                }
            }
        }
    });
    checks.emplace_back("Target footer metrics leave captured body chrome title capacity and counter untouched", [] {
        for (auto old : Designs) {
            for (auto target : Designs) {
                auto request = Model(old, target);
                request.appearance.optionCounter = true;
                request.menuHudPresence = MenuHudPresence::Present;
                const auto targetHud = MakeHud(request);
                request.instructionGeometryDesign.reset();
                const auto oldHud = MakeHud(request);
                Equal(targetHud.body.height, oldHud.body.height);
                Equal(targetHud.banner.height, oldHud.banner.height);
                Equal(targetHud.title.x, oldHud.title.x);
                Equal(targetHud.counter.y, oldHud.counter.y);
                Equal(targetHud.instructionText.y, oldHud.instructionText.y);
                Require(targetHud.capacity == oldHud.capacity && targetHud.counterVisible == oldHud.counterVisible,
                        "Footer target leaked into retained menu geometry");
            }
        }
    });
    checks.emplace_back("Custom saved anchors and measured help width survive the target geometry override", [] {
        for (auto old : Designs) {
            for (auto target : Designs) {
                auto request = Model(old, target);
                request.appearance.menuPosition = MenuPosition{17, 200};
                request.appearance.instructionPosition = InstructionPosition{300, 460};
                request.instructionWidth = 530;
                const auto hud = MakeHud(request);
                Equal(hud.instructionText.x, 17 + MetricsFor(target).footerX);
                Equal(hud.instructionText.y, 420);
                Equal(hud.instructions.width, 530);
            }
        }
    });
    checks.emplace_back("Nonadaptive alternate hidden and plain help retain existing anchor and visibility rules", [] {
        for (auto old : Designs) {
            for (auto target : Designs) {
                for (bool alternate : {false, true}) {
                    auto request = Model(old, target);
                    request.appearance.adaptiveInstructions = false;
                    request.appearance.alternateInstructions = alternate;
                    request.appearance.instructionPosition = InstructionPosition{300, 460};
                    request.appearance.instructionsBackground = false;
                    const auto overridden = MakeHud(request);
                    request.instructionGeometryDesign.reset();
                    const auto retained = MakeHud(request);
                    Equal(overridden.instructionText.x, retained.instructionText.x);
                    Equal(overridden.instructionText.y, retained.instructionText.y);
                    Require(overridden.instructionsVisible && overridden.outline.width == 0,
                            "Plain footer lost its text or retained decoration");
                    request.appearance.instructionsVisible = false;
                    Require(!MakeHud(request).instructionsVisible, "Hidden help became visible");
                }
            }
        }
    });
    checks.emplace_back(
        "Missing and invalid direct target enums preserve captured geometry without inventing a Design", [] {
            for (auto old : Designs) {
                auto request = Model(old, old);
                request.instructionGeometryDesign.reset();
                request.menuHudPresence = MenuHudPresence::Present;
                const auto legacy = MakeHud(request);
                Equal(legacy.instructionText.y, legacy.body.Bottom() + MetricsFor(old).footerY);
                request.instructionGeometryDesign = static_cast<MenuDesign>(999);
                const auto invalid = MakeHud(request);
                Equal(legacy.instructionText.x, invalid.instructionText.x);
                Equal(legacy.instructions.width, invalid.instructions.width);
                Require(legacy.design == invalid.design, "Invalid local target changed captured Design");
            }
        });
    checks.emplace_back("Target footer geometry keeps signed extremes finite and diagnostic regions bounded", [] {
        for (auto target : Designs) {
            for (auto size : {Point{320, 240}, Point{960, 720}, Point{480, 960}}) {
                auto request = Model(MenuDesign::Apparition, target);
                request.width = size.x;
                request.height = size.y;
                request.appearance.menuPosition =
                    MenuPosition{std::numeric_limits<std::int32_t>::max(), std::numeric_limits<std::int32_t>::min()};
                request.appearance.instructionPosition = InstructionPosition{std::numeric_limits<std::int32_t>::min(),
                                                                             std::numeric_limits<std::int32_t>::max()};
                const auto hud = MakeHud(request);
                Require(std::isfinite(hud.instructionText.x) && std::isfinite(hud.instructionText.y) &&
                            hud.notice.x >= 0 && hud.notice.y >= 0 && hud.notice.Right() <= size.x &&
                            hud.notice.Bottom() <= size.y,
                        "Target footer anchors produced nonfinite drawing or offscreen diagnostic text");
            }
        }
    });
}
} // namespace iw::overlay::tests
