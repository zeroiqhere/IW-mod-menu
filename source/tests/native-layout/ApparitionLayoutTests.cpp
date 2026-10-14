#include "instructions/InstructionHudLayoutTests.h"
#include "../../native/render/layout/ApparitionLayout.h"
#include "../../native/render/PanelText.h"
#include "DesignLayoutTests.h"
#include "InitialOpenLayoutTests.h"
#include "DesignFooterLayoutTests.h"
#include "StealthLayoutTests.h"
#include "../../native/render/layout/MenuPositionEditorLayout.h"
#include "../../native/render/layout/MenuNoticePlacement.h"
#include <cmath>
#include <functional>
#include <iostream>
#include <limits>
#include <stdexcept>
#include <string>
#include <vector>

namespace {
using namespace iw::overlay::layout;
void Require(bool result, const char* message) { if (!result) { throw std::runtime_error(message); } }
void Equal(float actual, float expected) { Require(std::abs(actual - expected) < 0.01f, "A reference coordinate does not match"); }
void Inside(const Rect& box, float width, float height) {
    Require(box.x >= 0 && box.y >= 0 && box.width >= 0 && box.height >= 0
        && box.Right() <= width + 0.01f && box.Bottom() <= height + 0.01f, "A HUD box leaves the viewport");
}
}

int main() {
    using namespace iw::overlay::layout;
    std::vector<std::pair<std::string, std::function<void()>>> tests;
    tests.emplace_back("Default viewport uses the source body, banner, line and font proportions", [] {
        auto hud = MakeHud({640, 480, 10, 0}); Require(hud.visible, "Reference HUD is missing");
        Equal(hud.scale, 1); Equal(hud.body.x, 0); Equal(hud.body.y, 85); Equal(hud.body.width, 260); Equal(hud.body.height, 180);
        Equal(hud.banner.y, 65); Equal(hud.banner.height, 215); Equal(hud.separator.y, 84); Equal(hud.separator.height, 1);
        Equal(hud.bottomLine.y, 265); Equal(hud.title.x, 130); Equal(hud.title.y, 73);
        Equal(hud.titleFont / hud.rowFont, 1.5f); Equal(hud.instructionFont / hud.rowFont, 1.1f);
    });
    tests.emplace_back("Source options and right-side boolean boxes retain their exact offsets", [] {
        auto hud = MakeHud({640, 480, 10, 0}); auto first = hud.OptionCenter(0), second = hud.OptionCenter(1);
        Equal(first.x, 4); Equal(first.y, 93); Equal(second.y - first.y, 18);
        auto box = hud.BooleanBox(0); Equal(box.x, 247); Equal(box.y, 88); Equal(box.width, 10); Equal(box.height, 10);
        Equal(hud.scroller.y, 85); Equal(hud.scroller.height, 18);
    });
    tests.emplace_back("Short menus shrink to their visible row count", [] {
        auto hud = MakeHud({640, 480, 3, 2}); Require(hud.shown == 3 && hud.first == 0, "Short menu paging changed");
        Equal(hud.body.height, 54); Equal(hud.scroller.y, 121); Equal(hud.bottomLine.y, 139);
    });
    tests.emplace_back("Empty menus reserve one fallback row without a selected option", [] {
        auto hud = MakeHud({640, 480, 0, 100}); Require(hud.visible && !hud.hasSelection && hud.count == 0 && hud.shown == 1, "Empty menu fallback changed");
        Equal(hud.body.height, 18); Equal(hud.scroller.height, 0);
    });
    tests.emplace_back("Middle selection uses Apparition centered scrolling rather than bottom anchoring", [] {
        auto hud = MakeHud({640, 480, 25, 12}); Require(hud.first == 8 && hud.selected == 12 && hud.shown == 10, "Centered scroll window changed");
        Equal(hud.scroller.y, 157);
    });
    tests.emplace_back("Scrolling clamps both ends and bounds hostile counts and selection", [] {
        Require(MakeHud({640, 480, 25, 0}).first == 0, "Top paging changed");
        auto end = MakeHud({640, 480, 25, 24}); Require(end.first == 15 && end.selected == 24, "Bottom paging changed");
        auto bounded = MakeHud({640, 480, std::numeric_limits<std::size_t>::max(), std::numeric_limits<std::size_t>::max()});
        Require(bounded.count == MaxRows && bounded.selected == MaxRows - 1 && bounded.first == MaxRows - 10, "Untrusted counts are not bounded");
    });
    tests.emplace_back("Instructions remain a separate gray panel above the source bottom anchor", [] {
        auto hud = MakeHud({640, 480, 10, 0, 220, 4}); Equal(hud.instructions.x, 255); Equal(hud.instructions.width, 220);
        Equal(hud.instructions.height, 54); Equal(hud.instructions.Bottom(), 472); Equal(hud.outline.x, 254);
        Require(hud.instructions.y > hud.banner.Bottom(), "Instructions overlap the option panel");
    });
    tests.emplace_back("Viewport conversion keeps all regions bounded across aspect ratios", [] {
        for (auto viewport : std::vector<Point>{{320, 240}, {960, 720}, {1920, 1080}, {3440, 1440}, {480, 960}}) {
            auto hud = MakeHud({viewport.x, viewport.y, 128, 99, 10000, 10000, true});
            Require(hud.visible, "A supported viewport unexpectedly hides the HUD");
            for (auto rect : {hud.body, hud.banner, hud.separator, hud.bottomLine, hud.scroller, hud.instructions, hud.outline, hud.notice}) {
                Inside(rect, viewport.x, viewport.y);
            }
        }
    });
    tests.emplace_back("Invalid and unreadably small viewports do not produce drawable geometry", [] {
        for (auto viewport : std::vector<Point>{{0, 480}, {-1, 480}, {640, 0}, {64, 32},
            {std::numeric_limits<float>::infinity(), 480}, {640, std::numeric_limits<float>::quiet_NaN()}}) {
            Require(!MakeHud({viewport.x, viewport.y, 10, 0}).visible, "An invalid viewport became drawable");
        }
    });
    tests.emplace_back("Theme values accept complete RGB and otherwise retain the source blue", [] {
        auto source = Theme("broken"), blue = Theme("#3998FE"), mint = Theme("#46dbb1");
        Require(source.red == 57 && source.green == 152 && source.blue == 254 && source.alpha == 255, "Fallback source theme changed");
        Require(blue.red == source.red && blue.green == source.green && blue.blue == source.blue, "Source blue parsing changed");
        Require(mint.red == 70 && mint.green == 219 && mint.blue == 177, "Valid theme parsing changed");
        Require(Theme("#12345G").blue == 254 && Theme("#123").blue == 254, "Invalid RGB theme accepted");
    });
    tests.emplace_back("Bounded player text preserves complete UTF-8 characters at every cut", [] {
        const std::string value = "A\xc3\xa9\xe4\xb8\xad\xf0\x9f\xa7\x9f";
        const std::vector<std::size_t> expected{0, 1, 1, 3, 3, 3, 6, 6, 6, 6, 10};
        for (std::size_t limit = 0; limit < expected.size(); ++limit) {
            Require(iw::overlay::BoundedPanelText(value, limit).size() == expected[limit], "A UTF-8 character was split");
        }
        Require(iw::overlay::BoundedPanelText(value, 100).size() == value.size(), "A complete name was shortened");
    });
    tests.emplace_back("Appearance row capacity follows all source display and instruction combinations", [] {
        for (const auto& display : {"Boxes", "Text", "Text Color"}) {
            for (bool instructions : {false, true}) { for (bool counter : {false, true}) {
                Request request{640, 480, 36, 18}; request.appearance.booleanDisplay = display;
                request.appearance.instructionsVisible = instructions; request.appearance.optionCounter = counter;
                const std::size_t expected = 10 + (!instructions ? 1 : 0)
                    + (display != std::string("Boxes") ? 2 + (!instructions ? 1 : 0) : 0) - (counter ? 2 : 0);
                auto hud = MakeHud(request);
                Require(hud.shown == expected && hud.capacity == expected, "Preference row capacity differs from source");
                Require(hud.first == 18 - (expected - 1) / 2, "Preference scrolling lost source centering");
            } }
        }
    });
    tests.emplace_back("Left boolean boxes use the source center offset without changing size", [] {
        Request request{640, 480, 3, 0}; request.appearance.boolBoxLocation = "Left";
        auto hud = MakeHud(request); auto box = hud.BooleanBox(0);
        Equal(box.x, 4); Equal(box.width, 10); Equal(hud.OptionCenter(0).x, 18);
    });
    tests.emplace_back("Option counter follows the final footer position and short-count offset", [] {
        Request request{640, 480, 36, 18}; request.appearance.optionCounter = true;
        auto hud = MakeHud(request); Require(hud.counterVisible, "Counter preference was ignored");
        Equal(hud.counter.x, 244); Equal(hud.counter.y, hud.body.Bottom() + 8);
        request.rowCount = 3; auto shortHud = MakeHud(request); Equal(shortHud.counter.x, 248);
    });
    tests.emplace_back("Committed width updates every horizontal source menu anchor", [] {
        Request request{640, 480, 10, 0}; request.appearance.menuWidth = 500;
        auto hud = MakeHud(request);
        Equal(hud.body.width, 500); Equal(hud.banner.width, 500); Equal(hud.scroller.width, 500);
        Equal(hud.separator.width, 500); Equal(hud.bottomLine.width, 500); Equal(hud.title.x, 250);
        Equal(hud.counter.x, 484); Equal(hud.BooleanBox(0).x, 487);
        request.appearance.menuWidth = 200; hud = MakeHud(request);
        Equal(hud.title.x, 100); Equal(hud.body.width, 200); Equal(hud.BooleanBox(0).x, 187);
        request.appearance.menuWidth = -100; Equal(MakeHud(request).body.width, 200);
        request.appearance.menuWidth = 1000; Equal(MakeHud(request).body.width, 500);
    });
    tests.emplace_back("Dedicated width editor suppresses normal row geometry and uses source soft-lock bounds", [] {
        Request request{640, 480, 36, 18}; request.widthEditor = true;
        request.appearance.optionCounter = true; request.appearance.instructionsVisible = false;
        const auto hud = MakeHud(request);
        Equal(hud.body.height, 120); Equal(hud.banner.height, 155); Equal(hud.bottomLine.y, 204);
        Require(!hud.hasSelection && !hud.counterVisible && hud.count == 0 && hud.shown == 0,
            "The dedicated editor retained ordinary row or counter geometry");
        Equal(hud.scroller.height, 0);
    });
    tests.emplace_back("Wide menus and editor notices remain bounded without overlapping menu or instruction regions", [] {
        auto overlaps = [](const Rect& left, const Rect& right) {
            return left.width > 0 && left.height > 0 && right.width > 0 && right.height > 0
                && left.x < right.Right() && right.x < left.Right() && left.y < right.Bottom() && right.y < left.Bottom();
        };
        for (auto viewport : std::vector<Point>{{320, 240}, {960, 720}, {1920, 1080}, {480, 960}}) {
            for (int width : {200, 260, 500}) { for (bool editor : {false, true}) {
                for (const auto& display : {"Boxes", "Text", "Text Color"}) {
                    for (bool instructions : {false, true}) { for (bool counter : {false, true}) {
                        Request request{viewport.x, viewport.y, 128, 80, 384, 6, true, 128};
                        request.appearance.menuWidth = width; request.widthEditor = editor;
                        request.appearance.booleanDisplay = display; request.appearance.instructionsVisible = instructions;
                        request.appearance.optionCounter = counter;
                        const auto hud = MakeHud(request);
                        for (auto box : {hud.body, hud.banner, hud.separator, hud.bottomLine, hud.scroller,
                            hud.notice, hud.instructions, hud.outline}) { Inside(box, viewport.x, viewport.y); }
                        Require(!overlaps(hud.notice, hud.banner) && !overlaps(hud.notice, hud.outline),
                            "A wide-menu notice overlaps source menu or instruction HUDs");
                    } }
                }
            } }
        }
    });
    tests.emplace_back("Fractional presentation width changes source anchors without changing the committed width", [] {
        Request request{640, 480, 10, 0}; request.appearance.menuWidth = 500; request.presentationWidth = 380.5f;
        const auto hud = MakeHud(request);
        Equal(hud.body.width, 380.5f); Equal(hud.banner.width, 380.5f); Equal(hud.title.x, 190.25f);
        Equal(hud.BooleanBox(0).x, 367.5f); Equal(hud.counter.x, 364.5f);
        Require(request.appearance.menuWidth == 500, "Presentation changed the confirmed width");
    });
    tests.emplace_back("Invalid local presentation widths fall back or clamp inside source limits", [] {
        Request request{640, 480, 10, 0}; request.appearance.menuWidth = 260;
        for (float value : {std::numeric_limits<float>::infinity(), -std::numeric_limits<float>::infinity(),
            std::numeric_limits<float>::quiet_NaN()}) {
            request.presentationWidth = value; Equal(MakeHud(request).body.width, 260);
        }
        request.presentationWidth = -1000; Equal(MakeHud(request).body.width, 200);
        request.presentationWidth = 1000; Equal(MakeHud(request).body.width, 500);
    });
    tests.emplace_back("Adaptive instructions follow the actual menu edge and minimum width", [] {
        Request request{640, 480, 8, 0, 80, 3}; request.appearance.adaptiveInstructions = true;
        auto hud = MakeHud(request); Equal(hud.instructions.x, hud.body.x + 1);
        Equal(hud.instructions.y, hud.body.Bottom() + 18); Equal(hud.instructions.width, hud.body.width - 2);
        request.appearance.menuWidth = 500; hud = MakeHud(request); Equal(hud.instructions.width, 498);
    });
    tests.emplace_back("Alternate instructions use one centered source rectangle rather than multiline placement", [] {
        Request request{640, 480, 8, 0, 220, 4}; request.appearance.alternateInstructions = true;
        auto hud = MakeHud(request); Equal(hud.instructions.height, 12);
        Equal(hud.instructions.x + hud.instructions.width / 2, 320);
        Equal(hud.instructions.y + hud.instructions.height / 2, 460);
    });
    tests.emplace_back("Disabling instruction background removes only its outline geometry", [] {
        Request request{640, 480, 8, 0, 220, 3}; auto ordinary = MakeHud(request);
        request.appearance.instructionsBackground = false; auto clear = MakeHud(request);
        Equal(clear.instructions.x, ordinary.instructions.x); Equal(clear.instructions.y, ordinary.instructions.y);
        Equal(clear.instructions.width, ordinary.instructions.width); Equal(clear.outline.width, 0); Equal(clear.outline.height, 0);
    });
    tests.emplace_back("Alternate help without a background starts at the source center anchor", [] {
        Request request{640, 480, 8, 0, 220, 4}; request.appearance.alternateInstructions = true;
        request.appearance.instructionsBackground = false;
        const auto hud = MakeHud(request);
        Equal(hud.instructionText.x, 320); Equal(hud.instructionAnchor.x, 320);
        Equal(hud.instructionAnchor.y, 460); Equal(hud.outline.width, 0);
        Require(!hud.instructionsCentered, "No-background alternate text retained centered alignment");
    });
    tests.emplace_back("Empty help hides decorations without changing source row capacity", [] {
        Request request{640, 480, 128, 80, 200, 0}; request.appearance.adaptiveInstructions = true;
        const auto hud = MakeHud(request);
        Require(!hud.instructionsVisible && hud.capacity == 10, "Empty local help changed applied menu preferences");
        Equal(hud.outline.width, 0);
    });
    tests.emplace_back("Instruction styles keep text and notices separate across supported viewports and capacities", [] {
        auto overlaps = [](const Rect& a, const Rect& b) {
            return a.width > 0 && a.height > 0 && b.width > 0 && b.height > 0
                && a.x < b.Right() && b.x < a.Right() && a.y < b.Bottom() && b.y < a.Bottom();
        };
        for (auto viewport : std::vector<Point>{{320, 240}, {960, 720}, {1920, 1080}, {480, 960}}) {
            for (int width : {200, 260, 500}) { for (bool editor : {false, true}) {
                for (const auto& display : {"Boxes", "Text", "Text Color"}) {
                    for (bool counter : {false, true}) { for (bool background : {false, true}) {
                        for (unsigned style = 0; style < 3; ++style) { for (std::size_t rows : {0u, 1u, 8u, 128u}) {
                            Request request{viewport.x, viewport.y, rows, 80, 10000, 6, true, 128};
                            request.appearance.menuWidth = width; request.widthEditor = editor;
                            request.appearance.booleanDisplay = display; request.appearance.optionCounter = counter;
                            request.appearance.instructionsBackground = background;
                            request.appearance.alternateInstructions = style == 1;
                            request.appearance.adaptiveInstructions = style == 2;
                            const auto hud = MakeHud(request);
                            for (auto box : {hud.body, hud.banner, hud.instructions, hud.instructionText, hud.outline, hud.notice}) {
                                Inside(box, viewport.x, viewport.y);
                            }
                            Require(!overlaps(hud.notice, hud.banner) && !overlaps(hud.notice, hud.instructionText),
                                "A status notice overlaps the instruction text or menu panel");
                        } }
                    } }
                }
            } }
        }
    });
    tests.emplace_back("Instruction reposition soft-lock retains the source title with a twenty-unit body", [] {
        Request request{640, 480, 36, 18}; request.positionEditor = true;
        request.appearance.optionCounter = true;
        const auto hud = MakeHud(request);
        Equal(hud.body.height, 20); Equal(hud.banner.height, 55); Equal(hud.bottomLine.y, 104);
        Equal(hud.title.x, 130); Equal(hud.title.y, 73);
        Require(!hud.hasSelection && !hud.counterVisible && hud.count == 0 && hud.shown == 0,
            "The position editor retained ordinary rows or counter geometry");
        Equal(hud.scroller.height, 0);
    });
    tests.emplace_back("Adaptive reposition entering and active phases use different source anchors", [] {
        Request request{640, 480, 6, 4, 260, 5}; request.positionEditor = true;
        request.appearance.adaptiveInstructions = true;
        auto entering = MakeHud(request); Equal(entering.instructions.x, 1); Equal(entering.instructions.y, 123);
        request.bypassAdaptiveInstructions = true;
        auto editing = MakeHud(request); Equal(editing.instructions.x, 255); Equal(editing.instructions.Bottom(), 472);
    });
    tests.emplace_back("Custom ordinary instruction anchors keep the saved bottom coordinate", [] {
        Request request{640, 480, 8, 0, 220, 3}; request.appearance.instructionPosition = iw::overlay::InstructionPosition{123, 200};
        auto hud = MakeHud(request); Equal(hud.instructions.x, 123); Equal(hud.instructions.Bottom(), 200);
        Equal(hud.instructionText.x, 123); Equal(hud.instructions.height, 40);
        request.appearance.instructionPosition.reset(); hud = MakeHud(request);
        Equal(hud.instructions.x, 255); Equal(hud.instructions.Bottom(), 472);
    });
    tests.emplace_back("Custom alternate anchors preserve center and no-background left alignment", [] {
        Request request{640, 480, 8, 0, 220, 5}; request.appearance.alternateInstructions = true;
        request.appearance.instructionPosition = iw::overlay::InstructionPosition{111, 222};
        auto hud = MakeHud(request); Equal(hud.instructionAnchor.x, 111); Equal(hud.instructionAnchor.y, 210);
        Equal(hud.instructions.x + hud.instructions.width / 2, 111);
        Equal(hud.instructions.y + hud.instructions.height / 2, 210);
        request.appearance.instructionsBackground = false; hud = MakeHud(request); Equal(hud.instructionText.x, 111);
        Equal(hud.outline.width, 0);
    });
    tests.emplace_back("Adaptive position ignores saved anchors only until editing or exiting", [] {
        Request request{640, 480, 6, 4, 260, 5}; request.positionEditor = true;
        request.appearance.adaptiveInstructions = true;
        request.appearance.instructionPosition = iw::overlay::InstructionPosition{-40, 180};
        auto hud = MakeHud(request); Equal(hud.instructions.x, 1); Equal(hud.instructions.y, 123);
        request.bypassAdaptiveInstructions = true; hud = MakeHud(request);
        Equal(hud.instructions.x, -40); Equal(hud.instructions.Bottom(), 180);
        request.positionEditor = false; request.bypassAdaptiveInstructions = false; hud = MakeHud(request);
        Equal(hud.instructions.x, 1); Equal(hud.instructions.y, hud.body.Bottom() + 18);
        Require(request.appearance.instructionPosition->x == -40, "Adaptive placement changed a saved anchor");
    });
    tests.emplace_back("Signed extreme anchors remain offscreen and finite without hiding status text", [] {
        for (auto anchor : std::vector<iw::overlay::InstructionPosition>{{-100, -100}, {700, 600},
            {std::numeric_limits<std::int32_t>::min(), std::numeric_limits<std::int32_t>::min()},
            {std::numeric_limits<std::int32_t>::max(), std::numeric_limits<std::int32_t>::max()}}) {
            for (bool alternate : {false, true}) {
                Request request{960, 720, 8, 0, 384, 5, true, 128};
                request.appearance.instructionPosition = anchor; request.appearance.alternateInstructions = alternate;
                const auto hud = MakeHud(request);
                for (auto rect : {hud.instructions, hud.instructionText, hud.outline}) {
                    Require(std::isfinite(rect.x) && std::isfinite(rect.y) && std::isfinite(rect.Right())
                        && std::isfinite(rect.Bottom()), "A signed anchor produced nonfinite geometry");
                }
                Inside(hud.notice, 960, 720); Require(hud.notice.width >= 80 * hud.scale && hud.notice.height >= 48 * hud.scale,
                    "An offscreen anchor hid the session notice");
                Require(request.appearance.instructionPosition == anchor, "An offscreen anchor was relocated");
            }
        }
    });
    tests.emplace_back("Moved instruction text and outlines never overlap notices across editor and style combinations", [] {
        auto overlaps = [](const Rect& a, const Rect& b) {
            return a.width > 0 && a.height > 0 && b.width > 0 && b.height > 0
                && a.x < b.Right() && b.x < a.Right() && a.y < b.Bottom() && b.y < a.Bottom();
        };
        for (auto viewport : std::vector<Point>{{320, 240}, {960, 720}, {1920, 1080}, {480, 960}}) {
            for (int width : {200, 260, 500}) { for (bool editor : {false, true}) {
                for (bool alternate : {false, true}) { for (bool background : {false, true}) {
                    for (auto anchor : std::vector<iw::overlay::InstructionPosition>{{0, 180}, {255, 200}, {320, 450},
                        {600, 100}, {-50, 380}, {260, 360}}) {
                        Request request{viewport.x, viewport.y, 128, 80, 638, 6, true, 128};
                        request.appearance.menuWidth = width; request.positionEditor = editor;
                        request.appearance.instructionPosition = anchor; request.appearance.alternateInstructions = alternate;
                        request.appearance.instructionsBackground = background;
                        const auto hud = MakeHud(request);
                        Inside(hud.notice, viewport.x, viewport.y);
                        Require(hud.notice.width > 0 && hud.notice.height >= 48 * hud.scale,
                            "A moved footer removed the readable session notice");
                        Require(!overlaps(hud.notice, hud.banner) && !overlaps(hud.notice, hud.instructionText)
                            && !overlaps(hud.notice, hud.outline), "Moved instruction text overlaps a status notice");
                    }
                } }
            } }
        }
    });
    tests.emplace_back("A bounded anchor grid always leaves a visible nonoverlapping notice region", [] {
        auto overlaps = [](const Rect& a, const Rect& b) {
            return a.width > 0 && a.height > 0 && b.width > 0 && b.height > 0
                && a.x < b.Right() && b.x < a.Right() && a.y < b.Bottom() && b.y < a.Bottom();
        };
        for (int x = -400; x <= 800; x += 40) { for (int y = -100; y <= 600; y += 20) {
            for (bool alternate : {false, true}) { for (bool editor : {false, true}) {
                Request request{640, 480, 128, 100, 638, 6, true, 128};
                request.appearance.menuWidth = 500; request.appearance.booleanDisplay = "Text";
                request.appearance.instructionPosition = iw::overlay::InstructionPosition{x, y};
                request.appearance.alternateInstructions = alternate; request.positionEditor = editor;
                const auto hud = MakeHud(request); Inside(hud.notice, 640, 480);
                Require(hud.notice.width >= 80 && hud.notice.height >= 48 && !overlaps(hud.notice, hud.banner)
                    && !overlaps(hud.notice, hud.instructionText) && !overlaps(hud.notice, hud.outline),
                    "A saved anchor removed or obscured the session notice");
            } }
        } }
    });
    tests.emplace_back("Menu reposition soft lock keeps the source title band and exact122-unit body", [] {
        Request request{640, 480, 10, 4}; request.menuPositionEditor = true;
        request.appearance.optionCounter = true; const auto hud = MakeHud(request);
        Equal(hud.body.height, 122); Equal(hud.banner.height, 157); Equal(hud.bottomLine.y, 206);
        Equal(hud.title.x, 130); Equal(hud.title.y, 73);
        Require(!hud.hasSelection && !hud.counterVisible && hud.count == 0 && hud.shown == 0,
            "Menu reposition kept ordinary options, counter or selection");
    });
    tests.emplace_back("Menu reposition preserves the separate ordinary footer placement and visibility", [] {
        Request normal{640, 480, 10, 4, 220, 3}; auto editor = normal; editor.menuPositionEditor = true;
        const auto a = MakeHud(normal), b = MakeHud(editor);
        Equal(a.instructions.x, b.instructions.x); Equal(a.instructions.y, b.instructions.y);
        Equal(a.instructions.width, b.instructions.width); Equal(a.instructions.height, b.instructions.height);
        editor.appearance.instructionsVisible = false; Require(!MakeHud(editor).instructionsVisible,
            "Menu reposition re-enabled a hidden ordinary footer");
    });
    tests.emplace_back("Committed menu positions move every menu region without moving ordinary instructions", [] {
        for (bool widthEditor : {false, true}) for (bool instructionEditor : {false, true}) {
            Request base{640, 480, 10, 4, 220, 3}; base.widthEditor = widthEditor; base.positionEditor = instructionEditor;
            base.appearance.optionCounter = true; const auto original = MakeHud(base);
            base.appearance.menuPosition = iw::overlay::MenuPosition{37, 126}; const auto moved = MakeHud(base);
            for (const auto pair : {std::pair{original.body, moved.body}, std::pair{original.banner, moved.banner},
                std::pair{original.separator, moved.separator}, std::pair{original.bottomLine, moved.bottomLine},
                std::pair{original.scroller, moved.scroller}}) {
                Equal(pair.second.x - pair.first.x, 37); Equal(pair.second.y - pair.first.y, 41);
                Equal(pair.first.width, pair.second.width); Equal(pair.first.height, pair.second.height);
            }
            Equal(moved.title.x - original.title.x, 37); Equal(moved.title.y - original.title.y, 41);
            Equal(moved.counter.x - original.counter.x, 37); Equal(moved.counter.y - original.counter.y, 41);
            Equal(moved.instructions.x, original.instructions.x); Equal(moved.instructions.y, original.instructions.y);
            if (!widthEditor && !instructionEditor) {
                Equal(moved.OptionCenter(0).x - original.OptionCenter(0).x, 37);
                Equal(moved.BooleanBox(0).y - original.BooleanBox(0).y, 41);
            }
        }
    });
    tests.emplace_back("Adaptive instructions follow the committed menu body and menu reposition soft lock", [] {
        for (bool editor : {false, true}) {
            Request request{640, 480, 10, 4, 220, 3}; request.menuPositionEditor = editor;
            request.appearance.menuPosition = iw::overlay::MenuPosition{37, 126}; request.appearance.adaptiveInstructions = true;
            const auto hud = MakeHud(request); Equal(hud.instructions.x, 38);
            Equal(hud.instructions.y, hud.body.Bottom() + 18); Require(hud.instructions.width >= hud.body.width - 2,
                "Adaptive help stopped following the moved menu width");
        }
    });
    tests.emplace_back("Extreme saved menu positions stay finite and notices stay bounded across viewport ratios", [] {
        for (auto viewport : std::vector<Point>{{320, 240}, {960, 720}, {1920, 1080}, {480, 960}})
            for (auto position : std::vector<iw::overlay::MenuPosition>{{-400, -300}, {700, 600},
                {std::numeric_limits<std::int32_t>::min(), std::numeric_limits<std::int32_t>::max()},
                {std::numeric_limits<std::int32_t>::max(), std::numeric_limits<std::int32_t>::min()}})
                for (bool editor : {false, true}) for (bool adaptive : {false, true}) {
                    Request request{viewport.x, viewport.y, 128, 100, 638, 6, true, 128};
                    request.menuPositionEditor = editor; request.appearance.menuPosition = position;
                    request.appearance.adaptiveInstructions = adaptive; const auto hud = MakeHud(request);
                    for (auto rectangle : {hud.body, hud.banner, hud.separator, hud.bottomLine, hud.scroller,
                        hud.instructions, hud.outline, hud.notice})
                        Require(std::isfinite(rectangle.x) && std::isfinite(rectangle.y) && std::isfinite(rectangle.width)
                            && std::isfinite(rectangle.height), "An extreme signed position emitted nonfinite geometry");
                    Inside(hud.notice, viewport.x, viewport.y);
                    Require(hud.notice.width >= 80 * hud.scale && hud.notice.height >= 48 * hud.scale,
                        "An offscreen menu removed its readable status region");
                    Require(request.appearance.menuPosition == position, "Layout silently clamped saved coordinates");
                }
    });
    tests.emplace_back("Moved menus reserve an unobscured readable notice whenever a free region exists", [] {
        const auto overlaps = [](Rect a, Rect b) {
            return a.width > 0 && a.height > 0 && b.width > 0 && b.height > 0
                && a.x < b.Right() && b.x < a.Right() && a.y < b.Bottom() && b.y < a.Bottom();
        };
        for (auto position : std::vector<iw::overlay::MenuPosition>{{1, 86}, {11, 96}, {260, 85},
            {-130, 85}, {0, 230}, {20, 50}, {0, -120}, {500, 440}})
            for (int width : {200, 260, 500}) for (bool editor : {false, true}) {
                Request request{640, 480, 10, 4, 260, 3, true, 8}; request.menuPositionEditor = editor;
                request.appearance.menuWidth = width; request.appearance.menuPosition = position;
                const auto hud = MakeHud(request); Inside(hud.notice, 640, 480);
                Require(hud.notice.width >= 80 && hud.notice.height >= 48 && !overlaps(hud.notice, hud.banner)
                    && !overlaps(hud.notice, hud.instructionText) && !overlaps(hud.notice, hud.outline),
                    "An available unobscured region was ignored for a moved menu");
            }
    });
    tests.emplace_back("Menu editor text preserves source first and offset anchors without overlapping the final line", [] {
        for (auto viewport : std::vector<Point>{{640, 480}, {320, 240}, {960, 720}}) {
            Request request{viewport.x, viewport.y}; request.menuPositionEditor = true;
            const auto hud = MakeHud(request); const auto text = MakeMenuPositionTextLayout(hud);
            Equal(text.first.x, hud.body.x + 4 * hud.scale); Equal(text.first.y, hud.body.y + 22 * hud.scale);
            Equal(text.offset.y, hud.body.y + 113 * hud.scale);
            Require(text.first.y + 6 * text.lineAdvance + hud.rowFont / 2 <= text.offset.y - hud.rowFont / 2 + .01f,
                "The last editor hint and offset label overlap");
            Require(text.offset.y + hud.rowFont / 2 <= hud.body.Bottom(), "Offset text exceeds source body122");
        }
    });
    tests.emplace_back("An impossible free-space layout uses a deterministic readable viewport status fallback", [] {
        const auto status = PlaceNoticeAroundMovedMenu({275, 85, 355, 100}, {0, 0, 640, 480}, {0, 0, 640, 480});
        Inside(status, 640, 480); Equal(status.x, 4); Equal(status.y, 4);
        Require(status.width >= 180 && status.height >= 48 && status.height <= 120,
            "Fully covered free space silently removed diagnostic text");
        const auto repeated = PlaceNoticeAroundMovedMenu({275, 85, 355, 100}, {0, 0, 640, 480}, {0, 0, 640, 480});
        Equal(status.width, repeated.width); Equal(status.height, repeated.height);
    });
    tests.emplace_back("Credits soft lock retains the title while using exact220-body255-banner geometry", [] {
        Request request{640, 480, 10, 4}; request.menuCredits = true; request.appearance.optionCounter = true;
        const auto hud = MakeHud(request); Equal(hud.body.height, 220); Equal(hud.banner.height, 255);
        Equal(hud.bottomLine.y, 304); Equal(hud.title.x, 130); Equal(hud.title.y, 73);
        Require(!hud.hasSelection && !hud.counterVisible && hud.count == 0 && hud.shown == 0,
            "Credits retained normal options, selection or counter");
    });
    iw::overlay::tests::AddInstructionHudLayoutChecks(tests);
    iw::overlay::tests::AddDesignLayoutChecks(tests);
    iw::overlay::tests::AddInitialOpenLayoutChecks(tests);
    iw::overlay::tests::AddDesignFooterLayoutChecks(tests);
    iw::overlay::tests::AddStealthLayoutChecks(tests);
    unsigned passed = 0;
    for (const auto& [name, test] : tests) {
        try { test(); ++passed; std::cout << "PASS " << name << '\n'; }
        catch (const std::exception& failure) { std::cerr << "FAIL " << name << ": " << failure.what() << '\n'; return 1; }
    }
    std::cout << "PASS " << passed << " portable Apparition layout checks\n";
}
