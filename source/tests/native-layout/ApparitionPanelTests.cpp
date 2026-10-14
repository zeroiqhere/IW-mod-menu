#include "chrome/theme/ActualChromeThemePanelTests.h"
#include "chrome/theme/ChromeThemePanelTests.h"
#include "instructions/withdrawal/FooterWithdrawalPanelTests.h"
#include "instructions/withdrawal/ActualFooterWithdrawalPanelTests.h"
#include "instructions/theme/ActualInstructionThemePanelTests.h"
#include "instructions/theme/InstructionThemePanelTests.h"
#include "instructions/color/ActualInstructionColorPanelTests.h"
#include "instructions/color/InstructionColorPanelTests.h"
#include "instructions/ActualInstructionHudPanelTests.h"
#include "instructions/InstructionHudPanelTests.h"
#include "palette/LiteralPaletteTests.h"
#include "palette/ActualPaletteTests.h"
#include "chrome/BasicScrollerPanelTests.h"
#include "chrome/BasicChromeFixturePanelTests.h"
#include "instruction-options/InstructionOptionPanelTests.h"
#include "local-options/LocalOptionPanelTests.h"
#include "paint/BooleanPaintPanelTests.h"
#include "callbacks/CallbackFrameTests.h"
#include "quick/EligibilityFrameTests.h"
#include "../../native/render/ApparitionPanel.h"
#include "../../native/render/InstructionPanel.h"
#include "../../native/render/WidthAnimation.h"
#include "../../native/render/SmoothRainbow.h"
#include "../../native/render/ScrollPresentation.h"
#include "../../native/render/ScrollRows.h"
#include "ScrollPanelTests.h"
#include "spacer/SpacerPanelTests.h"
#include "MenuPositionPanelTests.h"
#include "CreditsPanelTests.h"
#include "PreferenceRefreshPanelTests.h"
#include "DesignPanelTests.h"
#include "OpeningPanelTests.h"
#include "InitialOpenPanelTests.h"
#include "DesignFooterPanelTests.h"
#include "StealthPanelTests.h"
#include "rows/RowTextPanelTests.h"
#include "open-controls/OpeningPanelTests.h"
#include "../../native/render/layout/ApparitionLayout.h"
#include "CpuDrawlist.h"
#include "BackendPreview.h"
#include "../../native/runtime/ControlLabels.h"
#include <cmath>
#include <algorithm>
#include <functional>
#include <iostream>
#include <limits>
#include <stdexcept>
#include <string>
#include <vector>

namespace {
using namespace iw::overlay;
void Require(bool value, const char* message) { if (!value) { throw std::runtime_error(message); } }
class Frame final {
public:
    Frame() {
        // Each check owns its context and services font textures without a graphics backend
        context_ = ImGui::CreateContext();
        ImGui::SetCurrentContext(context_);
        auto& io = ImGui::GetIO();
        io.IniFilename = nullptr; io.LogFilename = nullptr;
        io.BackendFlags |= ImGuiBackendFlags_RendererHasTextures | ImGuiBackendFlags_RendererHasVtxOffset;
        ImGui::GetPlatformIO().Renderer_TextureMaxWidth = 4096;
        ImGui::GetPlatformIO().Renderer_TextureMaxHeight = 4096;
    }
    ~Frame() { ImGui::DestroyContext(context_); }
    ImDrawData& Draw(const MenuSnapshot& model, float width = 960, float height = 720,
        std::optional<float> presentationWidth = std::nullopt,
        std::optional<layout::Color> presentationColor = std::nullopt,
        const ScrollFrame* presentationRows = nullptr) {
        // This calls the production panel exactly once per ordinary ImGui frame
        ImGui::SetCurrentContext(context_);
        auto& io = ImGui::GetIO(); io.DisplaySize = {width, height}; io.DeltaTime = 1.0f / 60;
        ImGui::NewFrame(); drawn = DrawApparitionPanel(model, &summary, presentationWidth, presentationColor, presentationRows); ImGui::Render();
        auto& result = *ImGui::GetDrawData(); textures.Update(result); return result;
    }
    bool drawn = false;
    PanelDrawSummary summary;
    tests::CpuTextures textures;
private:
    ImGuiContext* context_;
};
MenuSnapshot Model() {
    MenuSnapshot model;
    model.isOpen = true; model.title = "Apparition";
    model.context = "PREVIEW / DISCONNECTED";
    model.notice = "Offline preview of the production panel. No game features are connected.";
    model.rows = {{"Basic Scripts", ">", true}, {"Menu Customization", ">", true},
        {"Message Menu", ">", true}, {"Teleport Menu", ">", true}, {"Power-Up Menu", ">", true},
        {"Profile Management", ">", true}, {"Model Manipulation", ">", true}, {"Weaponry", ">", true},
        {"Bullet Menu", ">", true}, {"Fun Scripts", ">", true}, {"Aimbot Menu", ">", true},
        {"Players Menu", ">", true}, {"All Players Menu", ">", true}};
    return model;
}
std::vector<std::string> ExampleSavedControls() {
    // These are declared test bindings, not inferred controls from a running game
    return runtime::DescribeControls({{2}, {86, 6}, {2, 38}, {1, 40}, {70}});
}
void CheckClips(const ImDrawData& data, float width, float height) {
    Require(data.TotalVtxCount < 100000 && data.TotalIdxCount < 150000, "Drawing work exceeded its test bound");
    for (auto* list : data.CmdLists) {
        for (const auto& vertex : list->VtxBuffer) {
            Require(std::isfinite(vertex.pos.x) && std::isfinite(vertex.pos.y) && std::isfinite(vertex.uv.x)
                && std::isfinite(vertex.uv.y), "A nonfinite draw vertex was emitted");
        }
        for (const auto& command : list->CmdBuffer) {
            Require(command.ClipRect.x >= 0 && command.ClipRect.y >= 0 && command.ClipRect.z <= width
                && command.ClipRect.w <= height && !command.UserCallback, "A draw command escaped its viewport clip");
            Require(command.GetTexID() != ImTextureID_Invalid, "A font texture request was not honored");
        }
    }
}
bool HasColor(const ImDrawData& data, ImU32 color) {
    for (auto* list : data.CmdLists) { for (const auto& vertex : list->VtxBuffer) { if (vertex.col == color) { return true; } } }
    return false;
}
bool HasColorIn(const ImDrawData& data, ImU32 color, const layout::Rect& region) {
    // Inspect text inside its own header rather than unrelated white instruction glyphs
    for (auto* list : data.CmdLists) {
        for (const auto& vertex : list->VtxBuffer) {
            if (vertex.col == color && vertex.pos.x >= region.x && vertex.pos.x < region.Right()
                && vertex.pos.y >= region.y && vertex.pos.y < region.Bottom()) { return true; }
        }
    }
    return false;
}
void CompareGlyphCoverage(const std::vector<unsigned char>& pixels, const std::vector<unsigned char>& reference) {
    // Compare local coverage so atlas packing and bilinear edge rounding cannot hide missing words
    for (int top = 0; top < 720; top += 16) {
        for (int left = 0; left < 960; left += 16) {
            unsigned difference = 0, samples = 0;
            for (int y = top; y < std::min(top + 16, 720); ++y) {
                for (int x = left; x < std::min(left + 16, 960); ++x) {
                    auto i = (x + y * 960) * 3;
                    for (int channel = 0; channel < 3; ++channel) {
                        difference += std::abs(int(pixels[i + channel]) - int(reference[i + channel])); ++samples;
                    }
                }
            }
            Require(difference <= samples * 8, "Switching menu models changed local glyph coverage");
        }
    }
}
}

int main(int count, char** arguments) {
    std::vector<std::pair<std::string, std::function<void()>>> checks;
    tests::AddFooterWithdrawalPanelChecks(checks);
#ifdef IW_FOOTER_WITHDRAWAL_FIXTURE_DIRECTORY
    tests::AddActualFooterWithdrawalPanelChecks(checks,IW_FOOTER_WITHDRAWAL_FIXTURE_DIRECTORY);
#endif
    tests::AddChromeThemePanelChecks(checks);
#ifdef IW_CHROME_THEME_FIXTURE_DIRECTORY
    tests::AddActualChromeThemePanelChecks(checks,IW_CHROME_THEME_FIXTURE_DIRECTORY);
#endif
    tests::AddInstructionHudPanelChecks(checks);
    tests::AddInstructionThemePanelChecks(checks);
#ifdef IW_INSTRUCTION_THEME_FIXTURE_DIRECTORY
    tests::AddActualInstructionThemePanelChecks(checks,IW_INSTRUCTION_THEME_FIXTURE_DIRECTORY);
#endif
    tests::AddInstructionColorPanelChecks(checks);
#ifdef IW_INSTRUCTION_COLOR_FIXTURE_DIRECTORY
    tests::AddActualInstructionColorPanelChecks(checks,IW_INSTRUCTION_COLOR_FIXTURE_DIRECTORY);
#endif
#ifdef IW_INSTRUCTION_HUD_FIXTURE_DIRECTORY
    tests::AddActualInstructionHudPanelChecks(checks, IW_INSTRUCTION_HUD_FIXTURE_DIRECTORY);
#endif
    tests::AddLiteralPaletteChecks(checks);
#ifdef IW_LITERAL_PALETTE_FIXTURE_DIRECTORY
    tests::AddActualLiteralPaletteChecks(checks, IW_LITERAL_PALETTE_FIXTURE_DIRECTORY);
#endif
    tests::AddBasicScrollerPanelChecks(checks);
#ifdef IW_BASIC_CHROME_FIXTURE_DIRECTORY
    tests::AddActualBasicChromeChecks(checks, IW_BASIC_CHROME_FIXTURE_DIRECTORY);
#endif
    tests::AddRowTextPanelChecks(checks);
    tests::AddOpeningControlsPanelChecks(checks);
    checks.emplace_back("Production panel creates a CPU drawlist with the source gray layers and blue lines", [] {
        Frame frame; auto model = Model(); auto& data = frame.Draw(model);
        Require(frame.drawn && data.TotalVtxCount > 0 && data.TotalIdxCount > 0, "The production panel did not draw");
        Require(HasColor(data, IM_COL32(55, 55, 55, 255)) && HasColor(data, IM_COL32(25, 25, 25, 128))
            && HasColor(data, IM_COL32(57, 152, 254, 255)), "Source HUD colors changed");
        CheckClips(data, 960, 720);
    });
    checks.emplace_back("Closed snapshots emit no panel triangles", [] {
        Frame frame; auto model = Model(); model.isOpen = false; auto& data = frame.Draw(model);
        Require(!frame.drawn && data.TotalVtxCount == 0, "A closed panel remained visible");
    });
    checks.emplace_back("Unreadably small viewports emit no panel triangles", [] {
        Frame frame; auto& data = frame.Draw(Model(), 64, 32);
        Require(!frame.drawn && data.TotalVtxCount == 0, "A tiny viewport drew an unusable panel");
    });
    checks.emplace_back("Empty models still draw the source fallback row safely", [] {
        Frame frame; auto model = Model(); model.rows.clear(); auto& data = frame.Draw(model);
        Require(frame.drawn && frame.summary.shownRows == 1 && data.TotalVtxCount > 0, "The empty menu fallback disappeared");
    });
    checks.emplace_back("Production scrolling exposes the centered source window for a middle option", [] {
        Frame frame; auto model = Model(); model.rows.resize(25, {"Option", ">", true}); model.selectedIndex = 12;
        auto& data = frame.Draw(model); Require(frame.summary.firstRow == 8 && frame.summary.shownRows == 10, "Production panel uses the wrong scroll window");
        CheckClips(data, 960, 720);
    });
    checks.emplace_back("Unicode and long names remain finite and clipped on every tested aspect ratio", [] {
        Frame frame; auto model = Model(); model.title = std::string(5000, 'T');
        model.rows[0] = {std::string(511, 'A') + "\xc3\xa9\xe4\xb8\xad\xf0\x9f\xa7\x9f", std::string(500, 'V'), true};
        for (auto viewport : std::vector<ImVec2>{{320, 240}, {1920, 1080}, {3440, 1440}, {480, 960}}) {
            CheckClips(frame.Draw(model, viewport.x, viewport.y), viewport.x, viewport.y);
        }
    });
    checks.emplace_back("Long status text grows within the viewport and visibly reports continuation", [] {
        Frame frame; auto model = Model(); model.notice = std::string(1500, 'A');
        auto& data = frame.Draw(model); Require(frame.summary.noticeContinues, "A long error was silently clipped");
        CheckClips(data, 960, 720);
    });
    checks.emplace_back("Short status text is displayed without a false continuation marker", [] {
        Frame frame; auto model = Model(); model.notice = "Read-only preview";
        frame.Draw(model); Require(!frame.summary.noticeContinues, "A short status claims missing text");
    });
    checks.emplace_back("A normal two-line status remains visible without a continuation marker", [] {
        Frame frame; auto model = Model();
        frame.Draw(model);
        Require(!frame.summary.noticeContinues, "A normal two-line notice lost its message to a continuation marker");
    });
    checks.emplace_back("Unchanged backend notice retains identical pixels across repeated frames", [] {
        Frame frame; auto model = Model(); model.notice = "Preview mode: game actions are disabled";
        auto& data = frame.Draw(model); auto pixels = frame.textures.Raster(data, 960, 720);
        for (int repeat = 0; repeat < 6; ++repeat) {
            auto& next = frame.Draw(model);
            Require(frame.textures.Raster(next, 960, 720) == pixels, "Unchanged notice pixels changed between frames");
        }
    });
    checks.emplace_back("Font atlas updates preserve notice glyphs when switching menu models", [] {
        auto finalModel = Model(); finalModel.title = "Main Design Color";
        finalModel.notice = "Preview mode: game actions are disabled";
        finalModel.rows.resize(36, {"Mint", "OFF", true});
        Frame frame; auto first = Model(); frame.Draw(first);
        first.notice = std::string(1500, 'A'); frame.Draw(first);
        auto& mixed = frame.Draw(finalModel); auto pixels = frame.textures.Raster(mixed, 960, 720);
        Frame fresh; auto& expected = fresh.Draw(finalModel);
        auto reference = fresh.textures.Raster(expected, 960, 720);
        CompareGlyphCoverage(pixels, reference);
        // The tolerance must still reject an erased message rather than concealing a missing word
        for (int y = 158; y < 177; ++y) { for (int x = 419; x < 939; ++x) {
            auto i = (x + y * 960) * 3; pixels[i] = pixels[i + 1] = pixels[i + 2] = 42;
        } }
        bool rejected = false;
        try { CompareGlyphCoverage(pixels, reference); } catch (const std::runtime_error&) { rejected = true; }
        Require(rejected, "Glyph coverage comparison accepted a missing message");
    });
    checks.emplace_back("The actual color-page notice produces visible message pixels", [] {
        Frame frame; auto model = Model(); model.title = "Main Design Color"; model.context = "PREVIEW";
        model.notice = "Preview mode: game actions are disabled";
        model.rows.resize(36, {"Mint", "OFF", true});
        auto& data = frame.Draw(model); auto pixels = frame.textures.Raster(data, 960, 720);
        unsigned visible = 0;
        for (int y = 158; y < 177; ++y) {
            for (int x = 419; x < 939; ++x) {
                auto i = (x + y * 960) * 3;
                visible += pixels[i] > 80 && pixels[i] == pixels[i + 1] && pixels[i] == pixels[i + 2];
            }
        }
        Require(visible > 200, "The color-page message had no readable glyph coverage");
    });
    checks.emplace_back("Actual saved-key hints replace generic instructions in the production drawlist", [] {
        Frame frame; auto model = Model(); auto& defaults = frame.Draw(model);
        auto defaultPixels = frame.textures.Raster(defaults, 960, 720);
        model.instructions = ExampleSavedControls();
        auto& actual = frame.Draw(model); auto actualPixels = frame.textures.Raster(actual, 960, 720);
        Require(actualPixels != defaultPixels, "Saved-key hints did not replace generic instructions");
        CheckClips(actual, 960, 720);
    });
    checks.emplace_back("Empty local hints draw no empty footer and preserve the applied row capacity", [] {
        Frame frame; auto model = Model(); model.instructions.clear();
        auto& data = frame.Draw(model, 640, 480);
        Require(frame.drawn && frame.summary.shownRows == 10, "Empty hints changed the applied menu preference");
        for (auto* list : data.CmdLists) { for (const auto& vertex : list->VtxBuffer) {
            Require(vertex.pos.y < 400, "Empty local hints drew an empty instruction footer");
        } }
        CheckClips(data, 640, 480);
    });
    checks.emplace_back("Four saved-control lines fit the instruction panel after long key remapping", [] {
        Frame frame; auto model = Model();
        runtime::Bindings bindings{{2}, {86, 6}, {2, 38}, {1, 40}, {70}, {8, 46}, {27, 9}};
        model.instructions = runtime::DescribeControls(bindings);
        Require(model.instructions.size() == 4, "The saved choice pair did not produce its fourth hint");
        auto& data = frame.Draw(model); CheckClips(data, 960, 720);
        bool finalLine = false;
        for (auto* list : data.CmdLists) { for (const auto& vertex : list->VtxBuffer) {
            if (vertex.col == IM_COL32_WHITE && vertex.pos.y > 620) {
                Require(vertex.pos.x <= 959 && vertex.pos.y <= 708, "A saved key hint extended past its instruction bounds");
                finalLine |= vertex.pos.y > 688;
            }
        } }
        Require(finalLine, "The saved choice hint's fourth line disappeared");
    });
    checks.emplace_back("Excess local hints retain the same bounded six-line panel", [] {
        Frame frame; auto model = Model(); model.instructions.assign(6, "Bounded hint");
        auto& bounded = frame.Draw(model); auto baseline = frame.textures.Raster(bounded, 960, 720);
        model.instructions.assign(1000, "Bounded hint"); auto& excessive = frame.Draw(model);
        Require(frame.textures.Raster(excessive, 960, 720) == baseline, "Excess hints changed the bounded instruction panel");
        CheckClips(excessive, 960, 720);
    });
    checks.emplace_back("ImGui 1.92 texture creation and subsequent font updates are honored", [] {
        Frame frame; auto model = Model(); frame.Draw(model, 640, 480);
        Require(frame.textures.Creates() > 0, "The initial atlas create request was not honored");
        unsigned changes = frame.textures.Updates() + frame.textures.Creates();
        auto& data = frame.Draw(model, 1280, 960);
        Require(frame.textures.Updates() + frame.textures.Creates() > changes, "A new font size failed to request an atlas update");
        CheckClips(data, 1280, 960);
    });
    checks.emplace_back("Theme changes tint title and lines without replacing the source gray background", [] {
        Frame frame; auto model = Model(); model.accent = "#46DBB1"; auto& data = frame.Draw(model);
        Require(HasColor(data, IM_COL32(70, 219, 177, 255)) && HasColor(data, IM_COL32(55, 55, 55, 255)), "A theme changed the wrong layers");
    });
    checks.emplace_back("Confirmed ON, OFF and UNKNOWN rows render distinct CPU pixels", [] {
        Frame frame; auto model = Model(); model.rows = {{"God Mode", "ON", true}, {"No Target", "OFF", true}, {"Unlimited Ammo", "UNKNOWN", false}};
        auto& data = frame.Draw(model, 640, 480); auto pixels = frame.textures.Raster(data, 640, 480);
        auto at = [&](int x, int y) { std::size_t i = (x + y * 640) * 3; return std::vector<unsigned char>{pixels[i], pixels[i + 1], pixels[i + 2]}; };
        Require(at(250, 93) != at(250, 111), "A confirmed on box looks like off");
        bool unknownDiffers = false;
        // Compare the complete mark rather than a pixel that may fall between glyph strokes
        for (int y = 124; y < 134; ++y) {
            for (int x = 247; x < 257; ++x) { unknownDiffers |= at(x, y) != at(x, y - 18); }
        }
        Require(unknownDiffers, "Unknown looks like a confirmed off box");
    });
    checks.emplace_back("Black source theme keeps literal title and identical ON and OFF fills", [] {
        Frame frame; auto model = Model(); model.accent = "#000000";
        model.rows = {{"God Mode", "ON", true}, {"No Target", "OFF", true}};
        model.selectedIndex = 9;
        auto& data = frame.Draw(model, 640, 480); auto pixels = frame.textures.Raster(data, 640, 480);
        Require(!HasColorIn(data, IM_COL32_WHITE, {0, 65, 260, 19}), "Black source title invented white contrast");
        const auto separator = (100 + 84 * 640) * 3;
        Require(pixels[separator] == 0 && pixels[separator + 1] == 0 && pixels[separator + 2] == 0,
            "The literal source separator changed its black RGB");
        // Compare matched selection opacity by drawing each state in the same row
        model.rows[0].value = "OFF";
        auto& offData = frame.Draw(model, 640, 480); auto off = frame.textures.Raster(offData, 640, 480);
        bool different = false;
        for (int y = 88; y < 98; ++y) {
            for (int x = 247; x < 257; ++x) {
                const auto i = (x + y * 640) * 3;
                different |= pixels[i] != off[i] || pixels[i + 1] != off[i + 1] || pixels[i + 2] != off[i + 2];
            }
        }
        Require(!different, "Black source ON fabricated a visible OFF distinction");
    });
    checks.emplace_back("Boolean text-color mode uses source green for ON and keeps UNKNOWN explicit", [] {
        Frame frame; auto model = Model(); model.rows = {{"Godmode", "ON", true}, {"Ammo", "UNKNOWN", false}};
        model.appearance.booleanDisplay = "Text Color";
        auto& data = frame.Draw(model, 640, 480);
        Require(HasColorIn(data, IM_COL32(0, 255, 0, 255), {0, 85, 230, 18}), "Text-color ON did not override the cursor theme");
        auto pixels = frame.textures.Raster(data, 640, 480);
        model.rows[1].value = "OFF"; auto& off = frame.Draw(model, 640, 480);
        Require(pixels != frame.textures.Raster(off, 640, 480), "Text-color UNKNOWN was hidden like confirmed OFF");
    });
    checks.emplace_back("Text display replaces right boxes while left boxes retain the source position", [] {
        Frame frame; auto model = Model(); model.rows = {{"Godmode", "ON", true}};
        auto& right = frame.Draw(model, 640, 480); auto baseline = frame.textures.Raster(right, 640, 480);
        model.appearance.booleanDisplay = "Text"; auto& text = frame.Draw(model, 640, 480);
        Require(frame.textures.Raster(text, 640, 480) != baseline, "Text display kept the source box");
        model.appearance.booleanDisplay = "Boxes"; model.appearance.boolBoxLocation = "Left";
        auto& left = frame.Draw(model, 640, 480);
        Require(HasColorIn(left, IM_COL32(57, 152, 254, 255), {5, 89, 8, 8}), "Left ON box was not at the source offset");
    });
    checks.emplace_back("Cursor color and spotlight choices apply independently to source option text", [] {
        Frame frame; auto model = Model(); model.rows = {{"First", ">", true}, {"Second", ">", true}};
        model.appearance.coloredCursor = false; model.appearance.spotlightCursor = false;
        auto& data = frame.Draw(model, 640, 480);
        Require(HasColorIn(data, IM_COL32_WHITE, {0, 85, 230, 18}), "Uncolored cursor text was not white");
        Require(HasColorIn(data, IM_COL32_WHITE, {0, 103, 230, 18}), "Disabled spotlight still faded other options");
    });
    checks.emplace_back("Large cursor enlarges selected text without enlarging boolean boxes", [] {
        Frame frame; auto model = Model(); model.rows = {{"Selected option", "ON", true}};
        auto& normal = frame.Draw(model, 640, 480); auto baseline = frame.textures.Raster(normal, 640, 480);
        model.appearance.largeCursor = true; auto& large = frame.Draw(model, 640, 480);
        auto enlarged = frame.textures.Raster(large, 640, 480);
        Require(enlarged != baseline, "Large cursor did not enlarge selected text");
        for (int y = 88; y < 98; ++y) { for (int x = 247; x < 257; ++x) {
            auto i = (x + y * 640) * 3;
            for (int channel = 0; channel < 3; ++channel) {
                Require(enlarged[i + channel] == baseline[i + channel], "Large cursor changed the source boolean box");
            }
        } }
        CheckClips(large, 640, 480);
    });
    checks.emplace_back("Hidden instructions remove footer geometry and option counter appears below rows", [] {
        Frame frame; auto model = Model(); model.rows.resize(36, {"Option", ">", true});
        model.appearance.instructionsVisible = false; model.appearance.optionCounter = true;
        auto& data = frame.Draw(model, 640, 480);
        Require(!HasColorIn(data, IM_COL32_WHITE, {250, 400, 390, 80}), "Hidden instruction text remained visible");
        Require(HasColorIn(data, IM_COL32(255, 255, 255, 179), {0, 247, 260, 17}), "Counter was not drawn beneath nine visible rows");
    });
    checks.emplace_back("The eighth quick-exit preference stays visible and changes only its confirmed row value", [] {
        Frame frame; auto model = Model(); model.title = "Menu Preferences";
        model.rows = {{"Boolean Display", "Boxes", true}, {"Boolean Box Location", "Right", true},
            {"Instructions", "ON", true}, {"Option Counter", "OFF", true},
            {"Colored Cursor", "ON", true}, {"Spotlight Cursor", "ON", true},
            {"Large Cursor", "OFF", true}, {"Quick Exit [ Hold melee ]", "OFF", true}};
        model.selectedIndex = 7;
        auto& off = frame.Draw(model, 640, 480); auto offPixels = frame.textures.Raster(off, 640, 480);
        Require(frame.summary.firstRow == 0 && frame.summary.shownRows == 8,
            "The eighth preference was clipped out of the source row window");
        // Input metadata must not invent a confirmed ON indicator from a stale row
        model.appearance.quickExit = true; model.isRootMenu = false;
        auto& metadata = frame.Draw(model, 640, 480);
        Require(frame.textures.Raster(metadata, 640, 480) == offPixels,
            "Quick-exit metadata fabricated a confirmed display state");
        model.rows[7].value = "ON";
        auto& on = frame.Draw(model, 640, 480); auto onPixels = frame.textures.Raster(on, 640, 480);
        unsigned changed = 0;
        for (int y = 214; y < 225; ++y) { for (int x = 246; x < 258; ++x) {
            const auto pixel = (x + y * 640) * 3;
            changed += onPixels[pixel] != offPixels[pixel] || onPixels[pixel + 1] != offPixels[pixel + 1]
                || onPixels[pixel + 2] != offPixels[pixel + 2];
        } }
        Require(changed > 20, "The confirmed eighth-row ON box did not differ from OFF");
        CheckClips(on, 640, 480);
    });
    checks.emplace_back("Root and nested quick-exit hints render their matching saved binding labels", [] {
        Frame frame; auto model = Model();
        const runtime::Bindings bindings{{2}, {86, 6}, {2, 38}, {1, 40}, {70}, {8, 46}, {27, 9}};
        model.instructions = runtime::DescribeControls(bindings, true, false);
        Require(model.instructions.size() == 4 && model.instructions[2].find("Tap back / hold close") != std::string::npos,
            "The nested demo did not use the real quick-exit formatter");
        auto& nested = frame.Draw(model); auto nestedPixels = frame.textures.Raster(nested, 960, 720);
        model.instructions = runtime::DescribeControls(bindings, true, true);
        Require(model.instructions[2].find(": Close") != std::string::npos,
            "The root demo did not use its immediate close label");
        auto& root = frame.Draw(model); auto rootPixels = frame.textures.Raster(root, 960, 720);
        Require(rootPixels != nestedPixels, "Different quick-exit hints retained identical legend pixels");
        Require(HasColorIn(root, IM_COL32_WHITE, {375, 624, 390, 90}), "Saved quick-exit hints produced no visible legend glyphs");
        CheckClips(root, 960, 720);
    });
    checks.emplace_back("A hidden instruction preference remains hidden with quick-exit hint variants", [] {
        Frame frame; auto model = Model(); model.appearance.instructionsVisible = false;
        model.appearance.quickExit = true; model.isRootMenu = false;
        const runtime::Bindings bindings{{2}, {86, 6}, {2, 38}, {1, 40}, {70}};
        model.instructions = runtime::DescribeControls(bindings, true, false);
        auto& nested = frame.Draw(model); auto hiddenPixels = frame.textures.Raster(nested, 960, 720);
        model.isRootMenu = true; model.instructions = runtime::DescribeControls(bindings, true, true);
        auto& root = frame.Draw(model);
        Require(frame.textures.Raster(root, 960, 720) == hiddenPixels,
            "Root quick exit overrode the confirmed hidden-instructions preference");
        CheckClips(root, 960, 720);
    });
    checks.emplace_back("The dedicated width editor hides normal options and draws its five hints despite a hidden footer", [] {
        Frame frame; auto model = Model(); model.widthEditor = WidthEditor{};
        model.appearance.instructionsVisible = false; model.appearance.optionCounter = true;
        auto& data = frame.Draw(model, 640, 480);
        Require(frame.summary.shownRows == 0 && frame.summary.firstRow == 0,
            "The dedicated width editor retained an ordinary option list");
        Require(HasColorIn(data, IM_COL32_WHITE, {4, 110, 250, 70})
            && HasColorIn(data, IM_COL32(0, 255, 0, 255), {4, 183, 250, 17}),
            "Editor controls or the green offset disappeared with the ordinary footer");
        auto pixels = frame.textures.Raster(data, 640, 480);
        model.title = "A different hidden title"; model.rows = {{"A hidden option", "ON", true}};
        model.selectedIndex = 0; model.appearance.largeCursor = true;
        model.appearance.booleanDisplay = "Text Color"; model.appearance.spotlightCursor = false;
        auto& changed = frame.Draw(model, 640, 480);
        Require(frame.textures.Raster(changed, 640, 480) == pixels,
            "Hidden ordinary title, rows or cursor style altered the dedicated editor");
        CheckClips(changed, 640, 480);
    });
    checks.emplace_back("Confirmed width-editor step changes its green value without inferring a value from rows", [] {
        Frame frame; auto model = Model(); model.widthEditor = WidthEditor{};
        auto& first = frame.Draw(model, 640, 480); auto one = frame.textures.Raster(first, 640, 480);
        model.rows = {{"Offset Value", "10", true}};
        auto& rowChange = frame.Draw(model, 640, 480);
        Require(frame.textures.Raster(rowChange, 640, 480) == one,
            "An ordinary row fabricated the editor offset value");
        model.widthEditor->step = 10;
        auto& tenth = frame.Draw(model, 640, 480);
        Require(frame.textures.Raster(tenth, 640, 480) != one, "The confirmed editor step did not update its value");
        model.widthEditor->step = 0; CheckClips(frame.Draw(model, 640, 480), 640, 480);
        model.widthEditor->step = 11; CheckClips(frame.Draw(model, 640, 480), 640, 480);
    });
    checks.emplace_back("The width editor preserves independent normal footer visibility and bounds remapped body hints", [] {
        Frame frame; auto model = Model(); model.widthEditor = WidthEditor{};
        model.widthEditor->instructions = {"Fire: Increase Width", "Aim: Decrease Width", "Next: Increase Offset",
            "Previous: Decrease Offset", "Melee: Exit"};
        model.appearance.instructionsVisible = false;
        auto& hidden = frame.Draw(model, 640, 480); auto hiddenPixels = frame.textures.Raster(hidden, 640, 480);
        model.instructions = {"Changed footer only"};
        auto& footerChange = frame.Draw(model, 640, 480);
        Require(frame.textures.Raster(footerChange, 640, 480) == hiddenPixels,
            "Hidden ordinary footer labels changed editor body pixels");
        model.appearance.instructionsVisible = true;
        auto& shown = frame.Draw(model, 640, 480);
        Require(HasColorIn(shown, IM_COL32_WHITE, {255, 430, 384, 43}),
            "The independent normal footer did not remain visible");
        model.widthEditor->instructions.assign(40, std::string(512, 'W') + "\xc3\xa9");
        for (int width : {200, 260, 500}) {
            model.appearance.menuWidth = width;
            for (const auto viewport : std::vector<ImVec2>{{320, 240}, {960, 720}, {480, 960}}) {
                CheckClips(frame.Draw(model, viewport.x, viewport.y), viewport.x, viewport.y);
            }
        }
        model.widthEditor->instructions.clear(); CheckClips(frame.Draw(model), 960, 720);
    });
    checks.emplace_back("Dynamic normal width and wide-menu notices stay finite for every source capacity style", [] {
        Frame frame; auto model = Model(); model.rows.resize(36, {"A bounded option", "ON", true}); model.selectedIndex = 18;
        for (int width : {200, 260, 500}) { for (const auto& display : {"Boxes", "Text", "Text Color"}) {
            for (bool instructions : {false, true}) { for (bool counter : {false, true}) {
                model.appearance.menuWidth = width; model.appearance.booleanDisplay = display;
                model.appearance.instructionsVisible = instructions; model.appearance.optionCounter = counter;
                model.notice = std::string(6000, 'A'); CheckClips(frame.Draw(model, 640, 480), 640, 480);
                Require(frame.summary.noticeContinues, "A long wide-menu notice was silently clipped");
            } }
        } }
        model = Model(); model.appearance.menuWidth = 260;
        auto& explicitWidth = frame.Draw(model); auto pixels = frame.textures.Raster(explicitWidth, 960, 720);
        model.appearance = MenuAppearance{};
        auto& defaults = frame.Draw(model);
        Require(frame.textures.Raster(defaults, 960, 720) == pixels,
            "The source default width changed ordinary panel pixels");
    });
    checks.emplace_back("The editor footer renders context help instead of inactive normal Scroll and Select controls", [] {
        Frame frame; auto model = Model(); model.widthEditor = WidthEditor{};
        model.instructions = {"Aim / fire: Scroll", "Use: Select", "Melee: Go back / exit"};
        auto& editor = frame.Draw(model); auto expected = frame.textures.Raster(editor, 960, 720);
        // Normal navigation help belongs to the resumed menu, not the editor's visible footer
        model.instructions = {"Unexpected Scroll binding", "Unexpected Select binding", "Unexpected Back binding"};
        auto& changedNormal = frame.Draw(model);
        Require(frame.textures.Raster(changedNormal, 960, 720) == expected,
            "The editor footer advertised inactive ordinary navigation controls");
        auto contextOnly = model; contextOnly.widthEditor.reset();
        contextOnly.instructions = model.widthEditor->footerInstructions;
        auto& ordinary = frame.Draw(contextOnly); auto reference = frame.textures.Raster(ordinary, 960, 720);
        for (int y = 620; y < 720; ++y) { for (int x = 370; x < 960; ++x) {
            const auto pixel = (x + y * 960) * 3;
            for (int channel = 0; channel < 3; ++channel) {
                Require(expected[pixel + channel] == reference[pixel + channel],
                    "The editor footer did not render its three dedicated context-help lines");
            }
        } }
        model.widthEditor->footerInstructions[2] = "V / Mouse 5: Exit editor";
        auto& remapped = frame.Draw(model);
        Require(frame.textures.Raster(remapped, 960, 720) != expected,
            "The saved melee binding did not change editor exit help");
        model.widthEditor->footerInstructions.clear(); CheckClips(frame.Draw(model), 960, 720);
        model.widthEditor->footerInstructions.assign(50, std::string(512, 'W'));
        CheckClips(frame.Draw(model), 960, 720);
    });
    checks.emplace_back("Production editor triangles use midpoint and completed fifty-millisecond widths", [] {
        Frame frame; WidthAnimation animation; auto model = Model(); model.widthEditor = WidthEditor{};
        animation.Update(260, true, true, 9, 0); model.appearance.menuWidth = 500;
        const float initial = animation.Update(500, true, true, 9, 100);
        auto& first = frame.Draw(model, 640, 480, initial);
        Require(HasColorIn(first, IM_COL32(57, 152, 254, 255), {259.9f, 84, .2f, 1.1f}),
            "The production panel skipped the initial animation edge");
        auto& midpoint = frame.Draw(model, 640, 480, animation.Update(500, true, true, 9, 125));
        Require(HasColorIn(midpoint, IM_COL32(57, 152, 254, 255), {379.9f, 84, .2f, 1.1f}),
            "The production panel ignored its midpoint presentation width");
        auto& completed = frame.Draw(model, 640, 480, animation.Update(500, true, true, 9, 150));
        Require(HasColorIn(completed, IM_COL32(57, 152, 254, 255), {499.9f, 84, .2f, 1.1f})
            && model.appearance.menuWidth == 500, "Completed animation changed or lost the committed width");
        CheckClips(completed, 640, 480);
    });
    checks.emplace_back("Production editor reversals sample the current edge while context changes snap", [] {
        Frame frame; WidthAnimation animation; auto model = Model(); model.widthEditor = WidthEditor{};
        animation.Update(260, true, true, 9, 0); animation.Update(500, true, true, 9, 0);
        model.appearance.menuWidth = 200;
        auto& reversal = frame.Draw(model, 640, 480, animation.Update(200, true, true, 9, 25));
        Require(HasColorIn(reversal, IM_COL32(57, 152, 254, 255), {379.9f, 84, .2f, 1.1f}),
            "Reversal jumped to a committed target instead of the displayed edge");
        auto& middle = frame.Draw(model, 640, 480, animation.Update(200, true, true, 9, 50));
        Require(HasColorIn(middle, IM_COL32(57, 152, 254, 255), {289.9f, 84, .2f, 1.1f}),
            "The reversed production animation lost its midpoint");
        model.appearance.menuWidth = 400;
        auto& context = frame.Draw(model, 640, 480, animation.Update(400, true, true, 10, 51));
        Require(HasColorIn(context, IM_COL32(57, 152, 254, 255), {399.9f, 84, .2f, 1.1f}),
            "A new editor context retained an unrelated animation");
        model.widthEditor.reset(); model.appearance.menuWidth = 500;
        auto& parent = frame.Draw(model, 640, 480, animation.Update(500, true, false, 11, 52));
        Require(HasColorIn(parent, IM_COL32(57, 152, 254, 255), {499.9f, 84, .2f, 1.1f}),
            "The normal parent inherited editor animation");
    });
    checks.emplace_back("Absent and matching presentation overrides retain ordinary panel pixels", [] {
        Frame frame; auto model = Model();
        auto& baseline = frame.Draw(model); auto pixels = frame.textures.Raster(baseline, 960, 720);
        auto& matching = frame.Draw(model, 960, 720, 260);
        Require(frame.textures.Raster(matching, 960, 720) == pixels, "A matching override changed default pixels");
        auto& invalid = frame.Draw(model, 960, 720, std::numeric_limits<float>::quiet_NaN());
        Require(frame.textures.Raster(invalid, 960, 720) == pixels, "An invalid local override changed default pixels");
    });
    checks.emplace_back("Unlock rebuild hides menu geometry while pause shows the parent with context help", [] {
        Frame frame; auto model = Model(); model.menuUnlock = MenuUnlockPhase::Rebuild;
        auto& rebuilt = frame.Draw(model, 640, 480);
        Require(frame.drawn && frame.summary.shownRows == 0 && !HasColor(rebuilt, IM_COL32(55, 55, 55, 255))
            && !HasColor(rebuilt, IM_COL32(25, 25, 25, 128)), "Rebuild retained a supposedly destroyed menu HUD");
        Require(HasColorIn(rebuilt, IM_COL32_WHITE, {255, 420, 384, 54})
            && HasColorIn(rebuilt, IM_COL32_WHITE, {275, 85, 355, 150}),
            "Rebuild removed independent context footer or IW status");
        model.menuUnlock = MenuUnlockPhase::Pause;
        auto& pause = frame.Draw(model, 640, 480);
        Require(frame.summary.shownRows > 0 && HasColor(pause, IM_COL32(55, 55, 55, 255))
            && HasColor(pause, IM_COL32(25, 25, 25, 128)), "Pause did not restore the parent HUD");
        CheckClips(pause, 640, 480);
    });
    checks.emplace_back("Unlock context help survives missing normal hints and respects the independent footer preference", [] {
        Frame frame; auto model = Model(); model.menuUnlock = MenuUnlockPhase::Rebuild;
        auto& first = frame.Draw(model, 640, 480); auto pixels = frame.textures.Raster(first, 640, 480);
        model.instructions.clear();
        auto& empty = frame.Draw(model, 640, 480);
        Require(frame.textures.Raster(empty, 640, 480) == pixels,
            "Missing normal hints removed the required unlock context help");
        model.instructions = {"Wrong Scroll", "Wrong Select"};
        auto& unrelated = frame.Draw(model, 640, 480);
        Require(frame.textures.Raster(unrelated, 640, 480) == pixels,
            "Unlock context advertised inactive normal controls");
        model.appearance.instructionsVisible = false;
        auto& hidden = frame.Draw(model, 640, 480);
        Require(!HasColorIn(hidden, IM_COL32_WHITE, {255, 420, 384, 54}),
            "Unlock context overrode the hidden footer preference");
        model.menuUnlock = MenuUnlockPhase::Pause;
        CheckClips(frame.Draw(model, 640, 480), 640, 480);
    });
    checks.emplace_back("Removing the footer background preserves actual white instruction glyphs", [] {
        Frame frame; auto model = Model(); model.menuUnlock = MenuUnlockPhase::Rebuild;
        model.context.clear(); model.notice.clear();
        model.appearance.instructionsBackground = false;
        auto& data = frame.Draw(model, 640, 480); auto pixels = frame.textures.Raster(data, 640, 480);
        Require(HasColor(data, IM_COL32_WHITE), "Background removal erased the instruction text");
        Require(!HasColor(data, IM_COL32(42, 42, 42, 255)) && !HasColor(data, IM_COL32(57, 152, 254, 255)),
            "Background removal retained a footer background or outline");
        unsigned ink = 0; for (auto value : pixels) ink += value > 100;
        Require(ink > 30, "Instruction text did not survive CPU rasterization without a background");
    });
    checks.emplace_back("Alternate content joins actual lines with the exact source separator", [] {
        auto model = Model(); model.instructions = {"RMB / LMB: Scroll", "F: Select", "V: Back"};
        model.appearance.alternateInstructions = true;
        const auto content = MakeInstructionContent(model);
        Require(content.LineCount() == 1 && content.Line(0) == "RMB / LMB: Scroll  |  F: Select  |  V: Back",
            "Alternate help changed its source separator or produced multiple rows");
        const auto moved = content;
        Require(moved.Line(0) == content.Line(0), "Moving alternate content left a dangling joined view");
    });
    checks.emplace_back("The final baked alternate font fits every saved-control glyph inside its text clip", [] {
        Frame frame; auto model = Model(); model.appearance.alternateInstructions = true;
        model.appearance.instructionsBackground = false;
        frame.Draw(model, 960, 720);
        const auto content = MakeInstructionContent(model);
        auto hud = layout::MakeHud({960, 720, model.rows.size(), model.selectedIndex,
            260, content.LineCount(), false, 0, model.appearance});
        hud = layout::MakeHud({960, 720, model.rows.size(), model.selectedIndex,
            MeasureInstructionWidth(content, hud), content.LineCount(), false, 0, model.appearance});
        const auto font = FitInstructionText(content, hud);
        const auto line = content.Line(0);
        const auto measured = ImGui::GetFont()->CalcTextSizeA(font, 10000, 0, line.data(), line.data() + line.size());
        Require(measured.x <= hud.instructionText.width - 4 * hud.scale + 0.01f,
            "The final baked font clipped the end of saved-control help");
    });
    checks.emplace_back("Alternate content bounds long Unicode lines and rejects local multiline expansion", [] {
        auto model = Model(); model.appearance.alternateInstructions = true;
        model.instructions.assign(100, std::string(255, 'K') + "\xc3\xa9\n");
        const auto content = MakeInstructionContent(model);
        Require(content.count == 6 && content.Line(0).size() == 6 * 255 + 5 * 5,
            "Alternate content exceeded its line or complete UTF-8 bound");
        model.instructions = {"A\nB", "", "C\rD"};
        Require(MakeInstructionContent(model).Line(0) == "A B  |  C D", "Alternate help contained a second physical row");
    });
    checks.emplace_back("Alternate footer drawing retains text without a background and follows left alignment", [] {
        for (bool background : {false, true}) {
            Frame frame; auto model = Model(); model.menuUnlock = MenuUnlockPhase::Rebuild;
            model.context.clear(); model.notice.clear(); model.appearance.alternateInstructions = true;
            model.appearance.instructionsBackground = background;
            auto& data = frame.Draw(model, 640, 480); auto pixels = frame.textures.Raster(data, 640, 480);
            Require(HasColor(data, IM_COL32_WHITE), "Alternate footer erased its instruction text");
            Require(HasColor(data, IM_COL32(42, 42, 42, 255)) == background,
                "Alternate footer did not honor its background preference");
            unsigned left = 0, right = 0;
            for (int y = 454; y < 466; ++y) { for (int x = 0; x < 640; ++x) {
                const auto i = (x + y * 640) * 3;
                if (pixels[i] > 180 && pixels[i + 1] > 180 && pixels[i + 2] > 180) { (x < 320 ? left : right)++; }
            } }
            Require(right > 20 && (background ? left > 20 : left == 0),
                "Alternate text did not use the source background-dependent alignment");
            CheckClips(data, 640, 480);
        }
    });
    checks.emplace_back("Instruction styles preserve editor and unlock context instead of inactive normal controls", [] {
        auto model = Model(); model.appearance.alternateInstructions = true;
        model.instructions = {"INACTIVE SCROLL", "INACTIVE SELECT"}; model.widthEditor = WidthEditor{};
        Require(MakeInstructionContent(model).Line(0).find("Menu navigation paused") != std::string_view::npos
            && MakeInstructionContent(model).Line(0).find("INACTIVE") == std::string_view::npos,
            "Editor alternate help advertised ordinary controls");
        model.menuUnlock = MenuUnlockPhase::Pause;
        Require(MakeInstructionContent(model).Line(0) == "Returning to menu  |  Menu controls paused  |  Please wait",
            "Unlock alternate help lost its context");
        Frame frame; CheckClips(frame.Draw(model), 960, 720);
        model.appearance.alternateInstructions = false; model.appearance.adaptiveInstructions = true;
        CheckClips(frame.Draw(model, 480, 960), 480, 960);
    });
    checks.emplace_back("All instruction styles respect hidden and empty help with bounded draw work", [] {
        for (unsigned style = 0; style < 3; ++style) { for (bool background : {false, true}) {
            Frame frame; auto model = Model(); model.menuUnlock = MenuUnlockPhase::Rebuild;
            model.context.clear(); model.notice.clear(); model.appearance.alternateInstructions = style == 1;
            model.appearance.adaptiveInstructions = style == 2; model.appearance.instructionsBackground = background;
            model.appearance.instructionsVisible = false;
            Require(frame.Draw(model, 640, 480).TotalVtxCount == 0, "Hidden instructions emitted triangles");
            model.menuUnlock.reset(); model.rows.clear(); model.appearance.instructionsVisible = true;
            model.instructions.clear();
            Require(!MakeInstructionContent(model).LineCount(), "Empty help fabricated a text row");
            CheckClips(frame.Draw(model, 480, 960), 480, 960);
        } }
    });
    checks.emplace_back("Long remapped help remains finite and clipped in every instruction style and aspect ratio", [] {
        for (auto viewport : std::vector<layout::Point>{{320, 240}, {960, 720}, {1920, 1080}, {480, 960}}) {
            for (unsigned style = 0; style < 3; ++style) { for (bool background : {false, true}) {
                Frame frame; auto model = Model(); model.appearance.menuWidth = 500;
                model.appearance.alternateInstructions = style == 1;
                model.appearance.adaptiveInstructions = style == 2;
                model.appearance.instructionsBackground = background;
                model.instructions.assign(100, std::string(250, 'K') + "\xc3\xa9\xe4\xb8\xad");
                model.notice.assign(1500, 'N');
                CheckClips(frame.Draw(model, viewport.x, viewport.y), viewport.x, viewport.y);
                model.widthEditor = WidthEditor{};
                model.widthEditor->footerInstructions = model.instructions;
                CheckClips(frame.Draw(model, viewport.x, viewport.y), viewport.x, viewport.y);
            } }
        }
    });
    checks.emplace_back("Position editing preserves the title and source small body while hiding every option", [] {
        Frame frame; auto model = Model(); model.title = "Menu Instructions";
        model.instructionPositionEditor = InstructionPositionEditor{};
        model.appearance.optionCounter = true; model.context.clear(); model.notice.clear();
        const auto& data = frame.Draw(model, 640, 480);
        Require(frame.summary.shownRows == 0 && HasColorIn(data, IM_COL32(57, 152, 254, 255), {0, 65, 260, 19}),
            "The position editor lost its title or retained ordinary options");
        Require(!HasColorIn(data, IM_COL32_WHITE, {0, 85, 260, 20}),
            "The position editor fabricated an option or fallback row");
        Require(!HasColorIn(data, IM_COL32(255, 255, 255, 179), {0, 105, 260, 15}),
            "The position editor retained its option counter");
        CheckClips(data, 640, 480);
    });
    checks.emplace_back("Position editor help uses only its five saved movement and exit labels", [] {
        auto model = Model(); model.instructionPositionEditor = InstructionPositionEditor{};
        model.instructions = {"INACTIVE Scroll", "INACTIVE Select"};
        model.instructionPositionEditor->instructions = {"Up: Move Up", "Down: Move Down", "Left: Move Left", "Right: Move Right", "V: Exit"};
        auto content = MakeInstructionContent(model);
        Require(content.LineCount() == 5 && content.Line(0) == "Up: Move Up" && content.Line(4) == "V: Exit",
            "Position help advertised normal menu controls or lost saved movement labels");
        model.appearance.alternateInstructions = true; content = MakeInstructionContent(model);
        Require(content.LineCount() == 1 && content.Line(0) == "Up: Move Up  |  Down: Move Down  |  Left: Move Left  |  Right: Move Right  |  V: Exit",
            "Alternate position help lost source separators or saved labels");
    });
    checks.emplace_back("Adaptive entry changes to saved instruction anchors only during editing and exiting", [] {
        Frame frame; auto model = Model(); model.instructionPositionEditor = InstructionPositionEditor{};
        model.appearance.adaptiveInstructions = true;
        model.appearance.instructionPosition = InstructionPosition{255, 472};
        auto& entering = frame.Draw(model, 640, 480);
        Require(HasColorIn(entering, IM_COL32_WHITE, {1, 123, 258, 68}), "Entering reposition prematurely bypassed adaptive positioning");
        auto enteringPixels = frame.textures.Raster(entering, 640, 480);
        model.instructionPositionEditor->phase = InstructionPositionEditorPhase::Editing;
        auto& editing = frame.Draw(model, 640, 480);
        Require(HasColorIn(editing, IM_COL32_WHITE, {255, 404, 384, 68}), "Editing reposition ignored the saved position");
        auto editingPixels = frame.textures.Raster(editing, 640, 480);
        Require(editingPixels != enteringPixels, "The editor phase did not move adaptive help");
        model.instructionPositionEditor->phase = InstructionPositionEditorPhase::Exiting;
        auto& exiting = frame.Draw(model, 640, 480);
        Require(frame.textures.Raster(exiting, 640, 480) == editingPixels, "Exiting reposition released its source flag early");
    });
    checks.emplace_back("Offscreen signed anchors emit no footer pixels and never change the applied menu model", [] {
        for (auto anchor : std::vector<InstructionPosition>{{-1000, -1000}, {1000, 1000},
            {std::numeric_limits<std::int32_t>::min(), std::numeric_limits<std::int32_t>::max()},
            {std::numeric_limits<std::int32_t>::max(), std::numeric_limits<std::int32_t>::min()}}) {
            for (bool alternate : {false, true}) { for (bool background : {false, true}) {
                Frame frame; auto model = Model(); model.menuUnlock = MenuUnlockPhase::Rebuild;
                model.context.clear(); model.notice.clear(); model.appearance.instructionPosition = anchor;
                model.appearance.alternateInstructions = alternate; model.appearance.instructionsBackground = background;
                auto& data = frame.Draw(model, 640, 480);
                Require(data.TotalVtxCount == 0, "A wholly offscreen footer emitted visible or extreme vertices");
                Require(model.appearance.instructionPosition == anchor, "Rendering relocated the saved position");
                CheckClips(data, 640, 480);
            } }
        }
    });
    checks.emplace_back("Partly offscreen help remains viewport-clipped with readable notices on all aspect ratios", [] {
        for (auto viewport : std::vector<layout::Point>{{320, 240}, {960, 720}, {1920, 1080}, {480, 960}}) {
            for (auto anchor : std::vector<InstructionPosition>{{-40, 180}, {600, 470}, {260, 200}}) {
                for (bool alternate : {false, true}) { for (bool background : {false, true}) {
                    Frame frame; auto model = Model(); model.appearance.instructionPosition = anchor;
                    model.appearance.alternateInstructions = alternate; model.appearance.instructionsBackground = background;
                    model.instructionPositionEditor = InstructionPositionEditor{};
                    model.instructionPositionEditor->phase = InstructionPositionEditorPhase::Editing;
                    model.notice.assign(1500, 'N');
                    auto& data = frame.Draw(model, viewport.x, viewport.y); CheckClips(data, viewport.x, viewport.y);
                    Require(frame.summary.noticeContinues, "A moved footer removed long status continuation");
                } }
            }
        }
    });
    checks.emplace_back("Hidden local position editors preserve hidden help without fabricating normal controls", [] {
        Frame frame; auto model = Model(); model.instructionPositionEditor = InstructionPositionEditor{};
        model.appearance.instructionsVisible = false; model.context.clear(); model.notice.clear();
        auto& data = frame.Draw(model, 640, 480);
        Require(!HasColor(data, IM_COL32_WHITE), "A defensive hidden editor forced its footer visible");
        Require(frame.summary.shownRows == 0, "A hidden editor advertised ordinary options");
        CheckClips(data, 640, 480);
    });
    checks.emplace_back("Explicit style-default anchors and reset anchors preserve the original panel pixels", [] {
        for (bool alternate : {false, true}) { for (bool background : {false, true}) {
            Frame frame; auto model = Model(); model.appearance.alternateInstructions = alternate;
            model.appearance.instructionsBackground = background;
            auto& original = frame.Draw(model, 640, 480); auto before = frame.textures.Raster(original, 640, 480);
            model.appearance.instructionPosition = DefaultInstructionPosition(alternate);
            auto& explicitDefault = frame.Draw(model, 640, 480);
            Require(frame.textures.Raster(explicitDefault, 640, 480) == before,
                "An explicit style-default anchor changed existing pixels");
            model.appearance.instructionPosition.reset(); auto& reset = frame.Draw(model, 640, 480);
            Require(frame.textures.Raster(reset, 640, 480) == before, "Reset placement retained an old position");
        } }
    });
    checks.emplace_back("Invalid local dual-editor metadata keeps the position soft-lock and never draws width controls", [] {
        Frame frame; auto model = Model(); model.instructionPositionEditor = InstructionPositionEditor{};
        model.widthEditor = WidthEditor{}; model.context.clear(); model.notice.clear();
        const auto content = MakeInstructionContent(model);
        Require(content.LineCount() == 5 && content.Line(0).find("Move Up") != std::string_view::npos,
            "Conflicting local metadata selected inactive width instructions");
        const auto& data = frame.Draw(model, 640, 480);
        Require(!HasColor(data, IM_COL32(0, 255, 0, 255)) && frame.summary.shownRows == 0,
            "Conflicting local metadata drew width controls or ordinary options");
        CheckClips(data, 640, 480);
    });
    checks.emplace_back("A saved offscreen anchor cannot shrink status text when instructions are hidden", [] {
        Frame frame; auto model = Model(); model.appearance.menuWidth = 500;
        model.appearance.instructionsVisible = false; model.notice.assign(1500, 'N');
        auto& normal = frame.Draw(model, 640, 480); auto expected = frame.textures.Raster(normal, 640, 480);
        model.appearance.instructionPosition = InstructionPosition{-100, -100};
        auto& saved = frame.Draw(model, 640, 480);
        Require(frame.textures.Raster(saved, 640, 480) == expected,
            "A hidden offscreen footer changed the session notice layout");
    });
    checks.emplace_back("Private position hints render the same saved action-slot keys used by the native router", [] {
        const runtime::Bindings bindings{{2}, {86, 6}, {2, 38}, {1, 40}, {70}, {37}, {39}, {1}, {38}, {40}};
        auto model = Model(); model.instructionPositionEditor = InstructionPositionEditor{};
        Frame frame; auto& generic = frame.Draw(model, 640, 480); auto original = frame.textures.Raster(generic, 640, 480);
        const auto labels = runtime::DescribeInstructionPositionControls(bindings);
        model.instructionPositionEditor->instructions = labels;
        const auto content = MakeInstructionContent(model);
        Require(content.LineCount() == labels.size(), "Saved position hints lost an action slot");
        for (std::size_t index = 0; index < labels.size(); ++index) {
            Require(content.Line(index) == labels[index], "Rendered movement help differs from native saved keys");
        }
        auto& saved = frame.Draw(model, 640, 480);
        Require(frame.textures.Raster(saved, 640, 480) != original, "The production panel retained generic position hints");
        CheckClips(saved, 640, 480);
    });
    checks.emplace_back("Rainbow source color reaches title lines selected text ON boxes and footer outline only", [] {
        Frame frame; auto model = Model(); model.smoothRainbowTheme = true;
        model.rows = {{"Enabled", "ON", true}, {"Disabled", "OFF", true}, {"Submenu", ">", true}};
        const auto& data = frame.Draw(model, 640, 480, std::nullopt, SampleSmoothRainbow(0));
        const auto red = IM_COL32(242, 0, 0, 255);
        Require(HasColorIn(data, red, {0, 65, 260, 20}) && HasColorIn(data, red, {0, 85, 240, 18})
            && HasColorIn(data, red, {248, 89, 8, 8}) && HasColorIn(data, red, {254, 430, 385, 44}),
            "Rainbow did not reach a source accent element");
        Require(HasColorIn(data, IM_COL32(55, 55, 55, 255), {0, 85, 260, 18})
            && HasColor(data, IM_COL32(25, 25, 25, 128))
            && HasColorIn(data, IM_COL32(42, 42, 42, 255), {255, 432, 384, 40}),
            "Rainbow recolored the gray source scroller, body or footer background");
        Require(HasColorIn(data, IM_COL32(0, 0, 0, 102), {248, 107, 8, 8})
            && HasColorIn(data, IM_COL32_WHITE, {255, 432, 384, 40}), "Rainbow recolored OFF or instruction text");
        CheckClips(data, 640, 480);
    });
    checks.emplace_back("Rainbow dark blue text retains literal source color instead of static readability fallback", [] {
        Frame frame; auto model = Model(); model.smoothRainbowTheme = true;
        const auto& data = frame.Draw(model, 640, 480, std::nullopt, SampleSmoothRainbow(6670));
        Require(HasColorIn(data, IM_COL32(0, 0, 242, 255), {0, 65, 260, 19})
            && !HasColorIn(data, IM_COL32_WHITE, {0, 65, 260, 19}),
            "Rainbow title changed a source dark blue into white");
    });
    checks.emplace_back("Static palette ignores unowned presentation colors and retains literal black title", [] {
        Frame frame; auto model = Model(); model.accent = "#000000";
        auto& ordinary = frame.Draw(model, 640, 480); auto expected = frame.textures.Raster(ordinary, 640, 480);
        auto& injected = frame.Draw(model, 640, 480, std::nullopt, SampleSmoothRainbow(0));
        Require(frame.textures.Raster(injected, 640, 480) == expected
            && !HasColorIn(injected, IM_COL32_WHITE, {0, 65, 260, 19}),
            "A rainbow presentation sample changed a fixed palette frame");
    });
    checks.emplace_back("Rainbow preserves Text Color ON green and the independent colored-cursor preference", [] {
        Frame frame; auto model = Model(); model.smoothRainbowTheme = true;
        model.rows = {{"Enabled", "ON", true}, {"Disabled", "OFF", true}};
        model.appearance.booleanDisplay = "Text Color";
        auto& colored = frame.Draw(model, 640, 480, std::nullopt, SampleSmoothRainbow(6670));
        Require(HasColorIn(colored, IM_COL32(0, 255, 0, 255), {0, 85, 240, 18}),
            "Rainbow replaced a confirmed Text Color ON with its accent");
        model.appearance.booleanDisplay = "Boxes"; model.appearance.coloredCursor = false;
        auto& plain = frame.Draw(model, 640, 480, std::nullopt, SampleSmoothRainbow(0));
        Require(HasColorIn(plain, IM_COL32_WHITE, {0, 85, 240, 18})
            && HasColorIn(plain, IM_COL32(242, 0, 0, 255), {248, 89, 8, 8}),
            "Rainbow ignored cursor color or stopped tinting an owned ON box");
    });
    checks.emplace_back("Rainbow does not add a missing footer background or erase UNKNOWN state text", [] {
        Frame frame; auto model = Model(); model.smoothRainbowTheme = true;
        model.appearance.instructionsBackground = false; model.rows = {{"Unconfirmed", "UNKNOWN", false}};
        model.context.clear(); model.notice.clear();
        const auto& data = frame.Draw(model, 640, 480, std::nullopt, SampleSmoothRainbow(0));
        Require(!HasColor(data, IM_COL32(42, 42, 42, 255)) && HasColorIn(data, IM_COL32_WHITE, {255, 432, 384, 40}),
            "Rainbow recreated a disabled footer decoration or erased white help");
        Require(HasColorIn(data, IM_COL32_WHITE, {248, 88, 10, 10}),
            "Rainbow erased the explicit UNKNOWN marker");
    });
    checks.emplace_back("Absolute rainbow phase survives navigation mode toggles and closed draw intervals", [] {
        Frame frame; auto model = Model(); model.smoothRainbowTheme = true;
        auto& first = frame.Draw(model, 640, 480, std::nullopt, SampleSmoothRainbow(2500));
        auto expected = frame.textures.Raster(first, 640, 480);
        model.isOpen = false; Require(frame.Draw(model, 640, 480, std::nullopt, SampleSmoothRainbow(8000)).TotalVtxCount == 0,
            "A closed rainbow snapshot drew a panel");
        model.isOpen = true; model.smoothRainbowTheme = false; frame.Draw(model, 640, 480);
        model.title = "Other page"; model.navigationRevision++; model.smoothRainbowTheme = true;
        frame.Draw(model, 640, 480, std::nullopt, SampleSmoothRainbow(7000));
        model.title = "Apparition";
        auto& later = frame.Draw(model, 640, 480, std::nullopt, SampleSmoothRainbow(12500));
        Require(frame.textures.Raster(later, 640, 480) == expected && model.accent == "#3998FE",
            "Navigation or a closed interval reset phase or mutated the retained accent");
    });
    checks.emplace_back("Production cursor triangles use sampled midpoint motion rather than the committed row", [] {
        Frame frame; auto model = Model(); model.rows.resize(8); model.selectedIndex = 1;
        model.menuRevision = 1; model.presentationFocusRevision = 0;
        auto initial = model; initial.selectedIndex = 0;
        auto request = MakeScrollRequest(initial, 640, 480);
        ScrollPresentation animation; animation.Update(request, 0); request.selected = 1; animation.Update(request, 100);
        const auto half = animation.Update(request, 160);
        const auto& data = frame.Draw(model, 640, 480, std::nullopt, std::nullopt, &half);
        Require(HasColorIn(data, IM_COL32(55, 55, 55, 255), {0, 93.9f, 260, 0.2f}),
            "The production cursor bar ignored the sampled half-row position");
        CheckClips(data, 640, 480);
    });
    checks.emplace_back("Production selection blends spotlight alpha and label color while keeping boolean boxes fixed", [] {
        Frame frame; auto model = Model(); model.rows = {{"First", "ON", true}, {"Second", "ON", true}};
        model.selectedIndex = 1;
        model.menuRevision = 1; model.presentationFocusRevision = 0;
        auto initial = model; initial.selectedIndex = 0;
        auto request = MakeScrollRequest(initial, 640, 480);
        ScrollPresentation animation; animation.Update(request, 0); request.selected = 1; animation.Update(request, 100);
        const auto half = animation.Update(request, 160);
        const auto& data = frame.Draw(model, 640, 480, std::nullopt, std::nullopt, &half);
        Require(HasColorIn(data, IM_COL32(156, 204, 255, 178), {0, 85, 240, 36}),
            "The production row omitted the source color and spotlight transition");
        Require(HasColorIn(data, IM_COL32(64, 64, 64, 178), {247, 88, 10, 28}),
            "Interpolated row opacity did not reach the fixed-size boolean boxes");
    });
    tests::AddScrollPanelChecks(checks);
    tests::AddSpacerPanelChecks(checks);
    tests::AddEligibilityFrameChecks(checks);
    AddBooleanPaintPanelChecks(checks);
    tests::AddCallbackFrameChecks(checks);
    tests::AddLocalOptionPanelChecks(checks);
#ifdef IW_INSTRUCTION_OPTION_FIXTURE_DIRECTORY
    tests::AddActualInstructionOptionChecks(checks, IW_INSTRUCTION_OPTION_FIXTURE_DIRECTORY);
#endif
#ifdef IW_LOCAL_OPTION_FIXTURE_DIRECTORY
    tests::AddActualLocalPaletteChecks(checks, IW_LOCAL_OPTION_FIXTURE_DIRECTORY);
#endif
    tests::AddMenuPositionPanelChecks(checks);
    tests::AddCreditsPanelChecks(checks);
    tests::AddPreferenceRefreshPanelChecks(checks);
    tests::AddDesignPanelChecks(checks);
    tests::AddOpeningPanelChecks(checks);
    tests::AddInitialOpenPanelChecks(checks);
    tests::AddDesignFooterPanelChecks(checks);
    iw::overlay::tests::AddStealthPanelChecks(checks);

    unsigned passed = 0;
    try {
        for (const auto& [name, test] : checks) { test(); ++passed; std::cout << "PASS " << name << '\n'; }
        if (count == 2 || count == 3) {
            std::filesystem::path directory(arguments[1]); std::filesystem::create_directories(directory);
            Frame frame; auto model = Model(); auto& main = frame.Draw(model);
            tests::WritePreview(directory / "reference-main.ppm", frame.textures.Raster(main, 960, 720), 960, 720);
            model.title = "Basic Scripts"; model.rows = {{"God Mode", "UNKNOWN", false}, {"Noclip", "UNKNOWN", false},
                {"Unlimited Ammo", "< Continuous >", false}, {"Unlimited Equipment", "UNKNOWN", false},
                {"Modify Score", "< 1000000 >", false}, {"Perk Menu", ">", true}, {"No Target", "OFF", false}};
            model.notice = std::string(1500, 'A'); model.selectedIndex = 2;
            auto& status = frame.Draw(model);
            tests::WritePreview(directory / "reference-status.ppm", frame.textures.Raster(status, 960, 720), 960, 720);
            if (count == 3) {
                const auto fixtures = std::filesystem::path(arguments[2]);
                if (std::filesystem::exists(fixtures / "spacer-scroll-ordinary-down-before.json"))
                    tests::CheckActualSpacerProfiles(fixtures, directory);
                if (std::filesystem::exists(fixtures / "preferences-draft.json")) {
                    auto applied = tests::ReadBackendPreview(fixtures / "preferences-default.json");
                    auto draft = tests::ReadBackendPreview(fixtures / "preferences-draft.json");
                    auto committed = tests::ReadBackendPreview(fixtures / "preferences-text-left.json");
                    Require(draft.navigationRevision != 0 && draft.navigationRevision == applied.navigationRevision,
                        "The exported draft lost its unchanged navigation epoch");
                    Require(committed.navigationRevision != draft.navigationRevision,
                        "The committed preferences retained the draft navigation epoch");
                    const auto boolDisplay = std::find_if(draft.rows.begin(), draft.rows.end(), [](const auto& row) { return row.label == "Bool Display"; });
                    Require(boolDisplay != draft.rows.end() && boolDisplay->value == "Text" && draft.appearance.booleanDisplay == "Boxes"
                        && committed.appearance.booleanDisplay == "Text",
                        "Candidate and applied display choices were conflated");
                    Require(draft.notice.find("Candidate: Text; Applied: Boxes") != std::string::npos,
                        "The draft did not explain its unapplied choice");
                    const auto instructionRow = std::find_if(draft.rows.begin(), draft.rows.end(),
                        [](const auto& row) { return row.label == "Spotlight Cursor"; });
                    Require(instructionRow != draft.rows.end(), "Actual preferences lost the Spotlight row");
                    const auto index = static_cast<std::size_t>(instructionRow - draft.rows.begin());
                    const layout::Rect instructionBox{248, 89 + 18.0f * index, 8, 8};
                    auto& draftData = frame.Draw(draft, 640, 480);
                    Require(HasColorIn(draftData, IM_COL32(152, 255, 152, 102), instructionBox),
                        "The candidate Text choice removed the still-applied boolean box");
                    auto& committedData = frame.Draw(committed, 640, 480);
                    Require(!HasColorIn(committedData, IM_COL32(152, 255, 152, 102), instructionBox),
                        "The committed Text choice retained an applied boolean box");
                    std::cout << "PASS Actual backend draft, applied display and navigation epoch checks\n";
                }
                if (std::filesystem::exists(fixtures / "stealth-rebuild-rebuild.json")) { tests::VerifyStealthBackendPreviews(fixtures); }
                const bool designFooterFixtures = std::filesystem::exists(fixtures / "preferences-design-adaptive-aio-rebuild.json");
                if (designFooterFixtures) { tests::VerifyDesignFooterBackendPreviews(fixtures, directory); }
                const bool initialOpenFixtures = std::filesystem::exists(fixtures / "menu-initial-open-rebuild.json");
                if (initialOpenFixtures) { tests::VerifyInitialOpenBackendPreviews(fixtures, directory); }
                const bool openingFixtures = std::filesystem::exists(fixtures / "menu-opening-start.json");
                if (openingFixtures) { tests::VerifyOpeningBackendPreviews(fixtures, directory); }
                const bool designFixtures = std::filesystem::exists(fixtures / "preferences-design-classic-rebuild.json");
                if (designFixtures) { tests::VerifyDesignBackendPreviews(fixtures, directory); }
                const bool refreshFixtures = std::filesystem::exists(fixtures / "preferences-refresh-spotlight-pending.json");
                if (refreshFixtures) { tests::VerifyPreferenceRefreshBackendPreviews(fixtures, directory); }
                const bool creditsFixtures = std::filesystem::exists(fixtures / "menu-credits-enter.json");
                if (creditsFixtures) { tests::VerifyCreditsBackendPreviews(fixtures, directory); }
                const bool menuPositionFixtures = std::filesystem::exists(fixtures / "menu-position-editor-default.json");
                if (menuPositionFixtures) { tests::VerifyMenuPositionBackendPreviews(fixtures, directory); }
                const bool scrollFixtures = std::filesystem::exists(fixtures / "scroll-motion-root-start.json");
                if (scrollFixtures) { tests::VerifyScrollBackendPreviews(fixtures, directory); }
                const bool widthFixtures = std::filesystem::exists(fixtures / "width-editor-default.json");
                if (widthFixtures) {
                    const auto original = tests::ReadBackendPreview(fixtures / "width-editor-default.json");
                    const auto widened = tests::ReadBackendPreview(fixtures / "width-editor-wide.json");
                    const auto finished = tests::ReadBackendPreview(fixtures / "customization-wide.json");
                    Require(original.widthEditor && original.widthEditor->step == 1
                        && original.appearance.menuWidth == 260 && original.rows.empty(),
                        "The actual default width editor lost its confirmed metadata");
                    Require(widened.widthEditor && widened.widthEditor->step == 10
                        && widened.appearance.menuWidth == 500 && widened.rows.empty(),
                        "The actual wide editor lost its confirmed width or offset");
                    Require(!finished.widthEditor && finished.appearance.menuWidth == 500 && !finished.rows.empty(),
                        "Leaving the actual editor lost the committed width or retained the editor view");
                    Require(!original.isRootMenu && !widened.isRootMenu && !finished.isRootMenu,
                        "The actual width editor fabricated a root page");
                    std::cout << "PASS Actual backend width metadata, confirmed limits and finished parent checks\n";
                }
                const bool timingFixtures = std::filesystem::exists(fixtures / "customization-unlock-rebuild.json");
                if (timingFixtures) {
                    const auto rebuilding = tests::ReadBackendPreview(fixtures / "customization-unlock-rebuild.json");
                    const auto paused = tests::ReadBackendPreview(fixtures / "customization-unlock-pause.json");
                    const auto resumed = tests::ReadBackendPreview(fixtures / "customization-wide.json");
                    Require(rebuilding.menuUnlock == MenuUnlockPhase::Rebuild && paused.menuUnlock == MenuUnlockPhase::Pause
                        && !resumed.menuUnlock, "The actual controller phase fields were discarded");
                    Require(rebuilding.rows.size() == resumed.rows.size() && paused.rows.size() == resumed.rows.size()
                        && rebuilding.rows.size() >= 4 && rebuilding.selectedIndex < rebuilding.rows.size()
                        && rebuilding.rows[rebuilding.selectedIndex].label == "Width Editor"
                        && paused.selectedIndex < paused.rows.size()
                        && paused.rows[paused.selectedIndex].label == "Width Editor" && rebuilding.appearance.menuWidth == 500
                        && paused.appearance.menuWidth == 500 && !rebuilding.widthEditor && !paused.widthEditor,
                        "Unlock snapshots changed the confirmed parent geometry or retained editor metadata");
                    Require(rebuilding.navigationRevision == paused.navigationRevision
                        && paused.navigationRevision == resumed.navigationRevision,
                        "The actual unlock interval changed its parent navigation epoch");
                    std::cout << "PASS Actual backend rebuild, pause and resumed phase checks\n";
                }
                const bool instructionFixtures = std::filesystem::exists(fixtures / "instructions-default.json");
                if (instructionFixtures) {
                    const auto normal = tests::ReadBackendPreview(fixtures / "instructions-default.json");
                    const auto alternate = tests::ReadBackendPreview(fixtures / "instructions-alternate-no-background.json");
                    const auto adaptive = tests::ReadBackendPreview(fixtures / "instructions-adaptive.json");
                    const auto hidden = tests::ReadBackendPreview(fixtures / "instructions-hidden.json");
                    Require(normal.rows.size() == 6 && normal.appearance.instructionsVisible
                        && !normal.appearance.alternateInstructions && normal.appearance.instructionsBackground
                        && !normal.appearance.adaptiveInstructions, "Actual default instruction profile changed");
                    Require(alternate.appearance.alternateInstructions && !alternate.appearance.instructionsBackground
                        && !alternate.appearance.adaptiveInstructions, "Actual alternate profile lost its confirmed preferences");
                    Require(adaptive.appearance.adaptiveInstructions && !adaptive.appearance.alternateInstructions
                        && !hidden.appearance.instructionsVisible, "Actual adaptive or hidden profile lost its preferences");
                    const bool positionsSupported = std::filesystem::exists(fixtures / "instructions-position-entering.json");
                    Require(normal.rows[4].value == "UNAVAILABLE" && !normal.rows[4].enabled
                        && (positionsSupported ? normal.rows[5].enabled && normal.rows[5].value.empty()
                            : normal.rows[5].value == "UNAVAILABLE" && !normal.rows[5].enabled),
                        "The actual instruction menu misreported reposition or reset capability");
                    std::cout << "PASS Actual backend instruction profiles and unavailable position controls\n";
                }
                const bool positionFixtures = std::filesystem::exists(fixtures / "instructions-position-entering.json");
                if (positionFixtures) {
                    const auto entering = tests::ReadBackendPreview(fixtures / "instructions-position-entering.json");
                    const auto editing = tests::ReadBackendPreview(fixtures / "instructions-position-editing.json");
                    const auto moved = tests::ReadBackendPreview(fixtures / "instructions-position-moved.json");
                    const auto exiting = tests::ReadBackendPreview(fixtures / "instructions-position-exiting.json");
                    const auto reset = tests::ReadBackendPreview(fixtures / "instructions-position-reset.json");
                    const auto hiddenReset = tests::ReadBackendPreview(fixtures / "instructions-hidden-reset.json");
                    Require(entering.instructionPositionEditor && editing.instructionPositionEditor
                        && moved.instructionPositionEditor && exiting.instructionPositionEditor,
                        "The producer lost position editor metadata");
                    Require(entering.instructionPositionEditor->phase == InstructionPositionEditorPhase::Entering
                        && editing.instructionPositionEditor->phase == InstructionPositionEditorPhase::Editing
                        && moved.instructionPositionEditor->phase == InstructionPositionEditorPhase::Editing
                        && exiting.instructionPositionEditor->phase == InstructionPositionEditorPhase::Exiting,
                        "The producer lost source position editor phases");
                    Require(moved.appearance.instructionPosition == InstructionPosition{263, 480}
                        && exiting.appearance.instructionPosition == moved.appearance.instructionPosition
                        && !reset.appearance.instructionPosition && !reset.instructionPositionEditor,
                        "The producer lost immediate movement, exit retention, or reset anchors");
                    for (const auto* editor : {&entering, &editing, &moved, &exiting}) {
                        Require(editor->rows.empty() && !editor->widthEditor && editor->title == "Menu Instructions",
                            "The producer fabricated editor rows, a width editor, or an unrelated title");
                    }
                    Require(!hiddenReset.appearance.instructionsVisible && hiddenReset.rows.at(5).enabled
                        && hiddenReset.rows.at(5).value.empty(), "Hidden instructions incorrectly disabled Reset Position");
                    std::cout << "PASS Actual backend position phases, moved anchors and reset capability checks\n";
                }
                const bool rainbowFixtures = std::filesystem::exists(fixtures / "theme-colors-rainbow.json");
                if (rainbowFixtures) {
                    const auto active = tests::ReadBackendPreview(fixtures / "theme-colors-rainbow.json");
                    const auto repeated = tests::ReadBackendPreview(fixtures / "theme-colors-rainbow-no-op.json");
                    const auto stopped = tests::ReadBackendPreview(fixtures / "theme-colors-rainbow-stopped.json");
                    Require(active.smoothRainbowTheme && repeated.smoothRainbowTheme && !stopped.smoothRainbowTheme
                        && active.accent == "#3998FE" && repeated.accent == active.accent && stopped.accent == "#98FF98",
                        "Actual theme mode and retained fixed accent were conflated");
                    Require(active.rows.size() == 37 && active.rows.back().label == "Smooth Rainbow"
                        && active.rows.back().value == "ON" && repeated.rows.back().value == "ON"
                        && stopped.rows.back().value == "OFF", "The actual source palette lost its exclusive rainbow row");
                    for (std::size_t row = 0; row < 36; ++row) {
                        Require(active.rows[row].value == "OFF" && repeated.rows[row].value == "OFF"
                            && stopped.rows[row].value == (row == 32 ? "ON" : "OFF"),
                            "An actual fixed palette row contradicted the committed theme mode");
                    }
                    Require(active.selectedIndex == 36 && repeated.selectedIndex == 36 && stopped.selectedIndex == 32
                        && active.navigationRevision == repeated.navigationRevision
                        && stopped.navigationRevision != active.navigationRevision,
                        "Reselecting or leaving rainbow changed the wrong navigation epoch");
                    auto& on = frame.Draw(active, 640, 480, std::nullopt, SampleSmoothRainbow(2500));
                    Require(HasColorIn(on, IM_COL32(121, 242, 0, 255), {0, 65, 260, 19}),
                        "An actual rainbow producer frame did not receive the explicit test clock");
                    auto& off = frame.Draw(stopped, 640, 480, std::nullopt, SampleSmoothRainbow(2500));
                    Require(HasColorIn(off, IM_COL32(152, 255, 152, 255), {0, 65, 260, 19}),
                        "An actual stopped frame retained the presentation rainbow color");
                    std::cout << "PASS Actual backend exclusive rainbow selection, no-op and fixed-color restoration\n";
                }
                // Actual backend fixtures provide the product catalog without connecting to a game
                unsigned exportedCount = 0;
                std::vector<std::filesystem::directory_entry> profiles;
                for (const auto& entry : std::filesystem::directory_iterator(arguments[2])) {
                    if (entry.path().extension() == ".json") {
                        profiles.push_back(entry);
                    }
                }
                // A fixed glyph warmup order keeps atlas sampling independent of directory creation order
                std::sort(profiles.begin(), profiles.end(), [](const auto& left, const auto& right) {
                    return left.path().filename() < right.path().filename();
                });
                for (const auto& entry : profiles) {
                    auto exported = tests::ReadBackendPreview(entry.path());
                    const auto sample = exported.smoothRainbowTheme
                        ? std::optional<layout::Color>{SampleSmoothRainbow(2500)} : std::nullopt;
                    auto& data = frame.Draw(exported, 960, 720, std::nullopt, sample);
                    CheckClips(data, 960, 720);
                    auto pixels = frame.textures.Raster(data, 960, 720);
                    Frame fresh;
                    auto& expected = fresh.Draw(exported, 960, 720, std::nullopt, sample);
                    auto reference = fresh.textures.Raster(expected, 960, 720);
                    CompareGlyphCoverage(pixels, reference);
                    tests::WritePreview(directory / (entry.path().stem().string() + ".ppm"),
                        pixels, 960, 720);
                    if (exported.smoothRainbowTheme) {
                        // The packet fields remain actual; a separate image labels its injected CPU-only clock
                        auto demo = exported;
                        demo.context = "PREVIEW / FIXED NATIVE CLOCK DEMO";
                        demo.notice = "Native uptime sample: 2500 ms. Independent phase, not synchronized with the BO3 source. No game features connected.";
                        auto& fixed = frame.Draw(demo, 960, 720, std::nullopt, sample);
                        tests::WritePreview(directory / (entry.path().stem().string() + "-fixed-clock.ppm"),
                            frame.textures.Raster(fixed, 960, 720), 960, 720);
                        demo.notice = "Native uptime sample: 6670 ms. Literal source dark blue. No game features connected.";
                        auto& blue = frame.Draw(demo, 960, 720, std::nullopt, SampleSmoothRainbow(6670));
                        tests::WritePreview(directory / (entry.path().stem().string() + "-dark-blue-clock.ppm"),
                            frame.textures.Raster(blue, 960, 720), 960, 720);
                    }
                    if (entry.path().stem() == "preferences-quick-exit" || entry.path().stem() == "root-quick-exit") {
                        const bool root = entry.path().stem() == "root-quick-exit";
                        Require(exported.appearance.quickExit && exported.isRootMenu == root
                            && !exported.appearance.instructionsVisible && exported.appearance.booleanDisplay == "Text"
                            && exported.accent == "#98FF98", "The actual quick-exit export lost confirmed display fields");
                        if (!root) {
                            const auto quickIndex = designFixtures ? 4u : scrollFixtures ? 8u : 7u;
                            Require(exported.rows.size() == 12 && exported.selectedIndex == quickIndex
                                && exported.rows[quickIndex].value == "ON", "The actual Quick Exit preference lost its confirmed selection");
                        }
                        // Separate examples enable the legend without altering the actual snapshot previews
                        auto demo = exported;
                        demo.appearance.instructionsVisible = true;
                        demo.context = "PREVIEW / HINT DEMO";
                        demo.notice = "Sample saved bindings; instructions enabled for this demo only. Game features are disabled.";
                        demo.instructions = runtime::DescribeControls({{2}, {86, 6}, {2, 38}, {1, 40}, {70}, {8, 46}, {27, 9}}, true, root);
                        auto& hints = frame.Draw(demo); CheckClips(hints, 960, 720);
                        tests::WritePreview(directory / (root ? "root-quick-exit-hint-demo.ppm" : "nested-quick-exit-hint-demo.ppm"),
                            frame.textures.Raster(hints, 960, 720), 960, 720);
                    }
                    if (entry.path().stem() == "instructions-position-editing") {
                        // Private saved-key examples remain separate from actual controller packet previews
                        auto demo = exported;
                        demo.context = "PREVIEW / SAMPLE POSITION BINDINGS";
                        demo.notice = "Declared saved action-slot labels only. No game controls or features are connected.";
                        demo.instructionPositionEditor->instructions = runtime::DescribeInstructionPositionControls(
                            {{2}, {86, 6}, {2, 38}, {1, 40}, {70}, {37}, {39}, {1}, {38}, {40}});
                        auto& bound = frame.Draw(demo); CheckClips(bound, 960, 720);
                        tests::WritePreview(directory / "instruction-position-saved-controls.ppm",
                            frame.textures.Raster(bound, 960, 720), 960, 720);
                    }
                    if (entry.path().stem() == "width-editor-wide") {
                        // This declared clock demo keeps the actual committed width and editor metadata intact
                        auto demo = exported;
                        demo.context = "PREVIEW / ANIMATION DEMO";
                        demo.notice = "25 ms of inferred linear 50 ms scaling. Committed width: 500. No game connected.";
                        WidthAnimation animation;
                        animation.Update(260, true, true, demo.navigationRevision, 0);
                        animation.Update(500, true, true, demo.navigationRevision, 100);
                        const float midpoint = animation.Update(500, true, true, demo.navigationRevision, 125);
                        auto& middle = frame.Draw(demo, 960, 720, midpoint); CheckClips(middle, 960, 720);
                        tests::WritePreview(directory / "width-animation-midpoint.ppm",
                            frame.textures.Raster(middle, 960, 720), 960, 720);
                    }
                    ++exportedCount;
                    if (entry.path().stem() == "main") {
                        exported.instructions = ExampleSavedControls();
                        exported.context = "PREVIEW / SAMPLE BINDINGS";
                        exported.notice = "Example saved key labels only. Game features are disabled.";
                        auto& bound = frame.Draw(exported);
                        tests::WritePreview(directory / "main-saved-controls.ppm",
                            frame.textures.Raster(bound, 960, 720), 960, 720);
                        exported.instructions = runtime::DescribeControls({{2}, {86, 6}, {2, 38}, {1, 40}, {70}, {8, 46}, {27, 9}});
                        exported.notice = "Example saved choice bindings only. Game features are disabled.";
                        auto& choices = frame.Draw(exported);
                        tests::WritePreview(directory / "main-four-controls.ppm",
                            frame.textures.Raster(choices, 960, 720), 960, 720);
                    }
                }
                if (widthFixtures) { Require(exportedCount == (std::filesystem::exists(fixtures / "opening-cleanup-retry.json") ? 233u : std::filesystem::exists(fixtures / "opening-closed.json") ? 232u : std::filesystem::exists(fixtures / "quick-root-unbound.json") ? 218u : std::filesystem::exists(fixtures / "navigation-shared-parent-after-back.json") ? 217u : std::filesystem::exists(fixtures / "quick-controls-default.json") ? 214u : std::filesystem::exists(fixtures / "spacer-scroll-ordinary-down-before.json") ? 192u : std::filesystem::exists(fixtures / "open-controls-default.json") ? 180u : designFooterFixtures ? 158u : initialOpenFixtures ? 128u : openingFixtures ? 110u : designFixtures ? 98u : refreshFixtures ? 72u : creditsFixtures ? 52u : menuPositionFixtures ? 45u : scrollFixtures ? 39u : rainbowFixtures ? 29u : positionFixtures ? 26u : instructionFixtures ? 20u : timingFixtures ? 16u : 14u), "The actual width preview set is incomplete"); }
                std::cout << "PASS " << exportedCount << " actual backend model previews and coverage comparisons\n";
            }
        }
    } catch (const std::exception& failure) { std::cerr << "FAIL " << failure.what() << '\n'; return 1; }
    std::cout << "PASS " << passed << " production panel CPU checks\n";
}
