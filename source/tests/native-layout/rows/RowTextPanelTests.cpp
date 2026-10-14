#include "RowTextPanelTests.h"
#include "../../../native/render/ApparitionPanel.h"
#include "../../../native/render/PanelText.h"
#include "../../../native/render/ScrollRows.h"
#include "../BackendPreview.h"
#include "../CpuDrawlist.h"
#include <algorithm>
#include <cmath>
#include <filesystem>
#include <imgui.h>
#include <stdexcept>

namespace iw::overlay::tests {
namespace {
void Require(bool value, const char *reason) {
    if (!value) {
        throw std::runtime_error(reason);
    }
}
class CpuPanel final {
  public:
    CpuPanel() {
        context_ = ImGui::CreateContext();
        ImGui::SetCurrentContext(context_);
        auto &io = ImGui::GetIO();
        io.IniFilename = nullptr;
        io.LogFilename = nullptr;
        io.BackendFlags |= ImGuiBackendFlags_RendererHasTextures | ImGuiBackendFlags_RendererHasVtxOffset;
        ImGui::GetPlatformIO().Renderer_TextureMaxWidth = 4096;
        ImGui::GetPlatformIO().Renderer_TextureMaxHeight = 4096;
    }
    ~CpuPanel() { ImGui::DestroyContext(context_); }
    std::vector<unsigned char> Draw(const MenuSnapshot &model, float width = 960, float height = 720,
                                    bool fullValueOracle = false, const ScrollFrame *suppliedRows = nullptr) {
        ImGui::SetCurrentContext(context_);
        auto &io = ImGui::GetIO();
        io.DisplaySize = {width, height};
        io.DeltaTime = 1.0f / 60;
        ImGui::NewFrame();
        auto displayed = model;
        if (fullValueOracle) {
            displayed.rows[11].label.clear();
            displayed.rows[11].value.clear();
        }
        DrawApparitionPanel(displayed, nullptr, std::nullopt, std::nullopt, suppliedRows);
        if (fullValueOracle) {
            // The oracle draws the unchanged value at the source right anchor with
            // the whole body clip
            const auto hud = Hud(model, width, height);
            const auto frame =
                suppliedRows ? *suppliedRows : StaticScrollFrame(MakeScrollRequest(model, width, height));
            const auto found = std::find_if(frame.rows.begin(), frame.rows.begin() + frame.count,
                                            [](const auto &row) { return row.index == 11; });
            Require(found != frame.rows.begin() + frame.count, "Actual unavailable row is not visible");
            const float font = hud.rowFont * (model.appearance.largeCursor ? 1 + .2f * found->selection : 1);
            const float centerY = hud.body.y + (hud.rowCenterOffset + 18 * found->position) * hud.scale;
            const auto &value = model.rows[11].value;
            const float measured = Width(font, value);
            const float opacity = found->opacity.value_or(
                (model.appearance.spotlightCursor ? .4f + .6f * found->selection : 1) * found->visibility);
            const auto alpha = static_cast<unsigned>(255 * opacity);
            auto &draw = *ImGui::GetForegroundDrawList();
            const float top = hud.body.y + (hud.rowCenterOffset - 8 + 18 * found->position) * hud.scale;
            draw.PushClipRect({hud.body.x, std::max(hud.body.y, top)},
                              {hud.body.Right(), std::min(hud.body.Bottom(), top + 18 * hud.scale)}, true);
            draw.AddText(ImGui::GetFont(), font,
                         {hud.body.Right() - 4 * hud.scale - measured, centerY - font / 2},
                         IM_COL32(255, 255, 255, alpha), value.data(), value.data() + value.size());
            draw.PopClipRect();
        }
        ImGui::Render();
        auto &data = *ImGui::GetDrawData();
        textures_.Update(data);
        return textures_.Raster(data, static_cast<int>(width), static_cast<int>(height));
    }
    static layout::Hud Hud(const MenuSnapshot &model, float width = 960, float height = 720) {
        return layout::MakeHud(
            {width, height, model.rows.size(), model.selectedIndex, 260, 3, false, 0, model.appearance});
    }
    static float Width(float font, std::string_view text) {
        return ImGui::GetFont()->CalcTextSizeA(font, 10000, 0, text.data(), text.data() + text.size()).x;
    }
    std::pair<unsigned, unsigned> TextureWork() const noexcept {
        return {textures_.Creates(), textures_.Updates()};
    }

  private:
    ImGuiContext *context_;
    CpuTextures textures_;
};
MenuSnapshot Fixture(const char *name) {
    return ReadBackendPreview(std::filesystem::path(IW_ROW_TEXT_FIXTURE_DIRECTORY) / name);
}
std::string OracleLabel(const MenuSnapshot &model, const layout::Hud &hud, std::size_t index,
                        float selection = -1) {
    const float sampledSelection = selection < 0 ? (model.selectedIndex == index ? 1.f : 0.f) : selection;
    const float font = hud.rowFont * (model.appearance.largeCursor ? 1 + .2f * sampledSelection : 1);
    const float valueWidth = CpuPanel::Width(font, model.rows[index].value);
    const float available = hud.body.width - 12 * hud.scale - valueWidth;
    const auto &label = model.rows[index].label;
    if (CpuPanel::Width(font, label) <= available) {
        return label;
    }
    // Brute-force complete prefixes form an independent oracle for the bounded
    // production fitter
    std::string result;
    for (std::size_t bytes = 0; bytes <= label.size(); ++bytes) {
        const auto candidate = std::string(BoundedPanelText(label, bytes)) + "...";
        if (CpuPanel::Width(font, candidate) <= available) {
            result = candidate;
        }
    }
    return result;
}
void SamePixels(const std::vector<unsigned char> &actual, const std::vector<unsigned char> &expected,
                const layout::Rect &area, int width, const char *message, float excludedY = -1000,
                float excludedBand = 0) {
    int maximumDifference = 0;
    const int height = static_cast<int>(actual.size() / (width * 3));
    Require(actual.size() == expected.size(), "Pixel oracle dimensions differ");
    for (int y = std::max(0, static_cast<int>(std::ceil(area.y)));
         y < std::min(height, static_cast<int>(std::floor(area.Bottom()))); ++y) {
        if (std::abs(y - excludedY) <= excludedBand) {
            continue;
        }
        for (int x = std::max(0, static_cast<int>(std::ceil(area.x)));
             x < std::min(width, static_cast<int>(std::floor(area.Right()))); ++x) {
            for (int channel = 0; channel < 3; ++channel) {
                maximumDifference = std::max(
                    maximumDifference, std::abs(static_cast<int>(actual[(x + y * width) * 3 + channel]) -
                                                expected[(x + y * width) * 3 + channel]));
            }
        }
    }
    if (maximumDifference) {
        throw std::runtime_error(std::string(message) +
                                 " maximum channel difference=" + std::to_string(maximumDifference));
    }
}
} // namespace
void AddRowTextPanelChecks(std::vector<std::pair<std::string, std::function<void()>>> &checks) {
    checks.emplace_back(
        "Wide bounded scalar values keep full source-anchor ink instead of a "
        "half-body clip",
        [] {
            CpuPanel panel;
            auto model = Fixture("stealth-refresh-unlocked.json");
            model.appearance.menuWidth = 200;
            model.rows[11].label.clear();
            model.rows[11].value = "123456789012345678901234";
            model.context = "CPU TEST / WIDE SCALAR PRESENTATION ONLY";
            const auto actual = panel.Draw(model);
            const auto hud = CpuPanel::Hud(model);
            const auto width = CpuPanel::Width(hud.rowFont, model.rows[11].value);
            Require(width > hud.body.width / 2 && width < hud.body.width - 8 * hud.scale,
                    "Wide scalar fixture does not exercise the half-body cap");
            const auto expected = panel.Draw(model, 960, 720, true);
            const float centerY = hud.body.y + (hud.rowCenterOffset + 18 * (11 - hud.first)) * hud.scale;
            const float start = hud.body.Right() - 4 * hud.scale - width;
            SamePixels(actual, expected,
                       {start, centerY - hud.rowFont / 2, hud.body.Right() - start, hud.rowFont}, 960,
                       "Wide bounded scalar lost glyphs inside the row body", centerY, 2 * hud.scale);
        });
    checks.emplace_back(
        "Actual row data in narrow large-cursor views matches a complete-prefix "
        "ellipsis pixel oracle",
        [] {
            for (const auto *name : {"stealth-refresh-unlocked.json", "counter-on-unlocked.json"}) {
                CpuPanel panel;
                const auto original = Fixture(name);
                panel.Draw(original);
                const auto baselineHud = CpuPanel::Hud(original);
                Require(OracleLabel(original, baselineHud, 11) == original.rows[11].label,
                        "Default actual packet unexpectedly stopped fitting at its "
                        "mapped font");
                auto actual = original;
                // These owned presentation settings exercise supported narrow-width
                // and large-cursor choices
                actual.appearance.menuWidth = 200;
                actual.appearance.largeCursor = true;
                actual.selectedIndex = 11;
                actual.context = "CPU TEST / NARROW PRESENTATION ONLY";
                const auto pixels = panel.Draw(actual);
                const auto hud = CpuPanel::Hud(actual);
                auto expected = actual;
                expected.rows[11].label = OracleLabel(actual, hud, 11);
                Require(expected.rows[11].label != actual.rows[11].label &&
                            expected.rows[11].label.ends_with("..."),
                        "Actual label oracle does not exercise the reported overlap");
                const auto reference = panel.Draw(expected);
                const float y = hud.body.y + (hud.rowCenterOffset - 8 + 18 * (11 - hud.first)) * hud.scale;
                const float right = hud.body.Right() - 8 * hud.scale -
                                    CpuPanel::Width(hud.rowFont * 1.2f, actual.rows[11].value);
                SamePixels(pixels, reference, {hud.body.x, y, right - hud.body.x, 18 * hud.scale}, 960,
                           "Long unavailable label did not match the UTF-8 ellipsis "
                           "pixel oracle");
                Require(actual.rows[11].label == "No Target While In Menu" &&
                            actual.rows[11].value == "UNAVAILABLE",
                        "The displayed fit changed the immutable source row");
            }
        });
    checks.emplace_back(
        "Actual unavailable values retain every glyph at the unchanged source "
        "anchor",
        [] {
            for (const auto *name : {"stealth-refresh-unlocked.json", "counter-on-unlocked.json"}) {
                for (int width : {200, 260, 500}) {
                    CpuPanel panel;
                    auto model = Fixture(name);
                    model.appearance.menuWidth = width;
                    model.rows[11].label.clear();
                    const auto actual = panel.Draw(model);
                    const auto expected = panel.Draw(model, 960, 720, true);
                    const auto hud = CpuPanel::Hud(model);
                    const float centerY =
                        hud.body.y + (hud.rowCenterOffset + 18 * (11 - hud.first)) * hud.scale;
                    const float start =
                        hud.body.Right() - 4 * hud.scale - CpuPanel::Width(hud.rowFont, model.rows[11].value);
                    // The capability strike is independent decoration, so only its
                    // narrow horizontal band is excluded
                    SamePixels(actual, expected,
                               {start, centerY - hud.rowFont / 2, hud.body.Right() - start, hud.rowFont}, 960,
                               (std::string(name) + " width=" + std::to_string(width) +
                                " center=" + std::to_string(centerY) +
                                " Unavailable value lost glyphs inside the row body")
                                   .c_str(),
                               centerY, 2 * hud.scale);
                }
            }
        });
    checks.emplace_back("Long row reservation matches the pixel oracle in all five menu designs", [] {
        for (const auto design : {MenuDesign::Apparition, MenuDesign::Classic, MenuDesign::Native,
                                  MenuDesign::Aio, MenuDesign::Basic}) {
            CpuPanel panel;
            auto model = Fixture("stealth-refresh-unlocked.json");
            model.appearance.design = design;
            model.appearance.coloredCursor = false;
            model.appearance.menuWidth = 200;
            model.appearance.largeCursor = true;
            model.selectedIndex = 11;
            const auto actual = panel.Draw(model);
            const auto hud = CpuPanel::Hud(model);
            auto expected = model;
            expected.rows[11].label = OracleLabel(model, hud, 11);
            Require(expected.rows[11].label.ends_with("..."), "Design fixture does not need fitting");
            const auto reference = panel.Draw(expected);
            SamePixels(actual, reference, hud.body, 960, "Design changed the label or value oracle");
        }
    });
    checks.emplace_back(
        "UTF-8 row labels match complete-prefix glyphs without "
        "changing source text",
        [] {
            CpuPanel panel;
            auto model = Fixture("stealth-refresh-unlocked.json");
            model.appearance.menuWidth = 200;
            model.selectedIndex = 11;
            model.appearance.coloredCursor = false;
            model.rows[11].label = "Préférences 界 🙂 paramètres très longs";
            const auto source = model.rows[11].label;
            const auto actual = panel.Draw(model);
            const auto hud = CpuPanel::Hud(model);
            auto expected = model;
            expected.rows[11].label = OracleLabel(model, hud, 11);
            Require(expected.rows[11].label.ends_with("...") && expected.rows[11].label != source,
                    "UTF-8 fixture did not require an ellipsis");
            SamePixels(actual, panel.Draw(expected), hud.body, 960, "UTF-8 glyph oracle differs");
            Require(model.rows[11].label == source, "UTF-8 fit altered owned snapshot data");
        });
    checks.emplace_back("Sampled large-cursor growth recomputes fitting from the current row "
                        "value",
                        [] {
                            CpuPanel panel;
                            auto model = Fixture("stealth-refresh-unlocked.json");
                            model.appearance.menuWidth = 200;
                            model.appearance.largeCursor = true;
                            model.appearance.coloredCursor = false;
                            model.selectedIndex = 11;
                            for (const float selection : {0.f, .5f, 1.f}) {
                                auto frame = StaticScrollFrame(MakeScrollRequest(model, 960, 720));
                                for (std::size_t index = 0; index < frame.count; ++index) {
                                    if (frame.rows[index].index == 11) {
                                        frame.rows[index].selection = selection;
                                    }
                                }
                                const auto actual = panel.Draw(model, 960, 720, false, &frame);
                                const auto hud = CpuPanel::Hud(model);
                                auto expected = model;
                                expected.rows[11].label = OracleLabel(model, hud, 11, selection);
                                SamePixels(actual, panel.Draw(expected, 960, 720, false, &frame), hud.body,
                                           960, "Sampled font used stale fitted row text");
                                // A changed scalar is read from this owned snapshot on the very next
                                // draw
                                model.rows[11].value = selection == 0 ? "130 ms" : "UNAVAILABLE";
                            }
                        });
    checks.emplace_back("Offscreen and small supported views clip fitted rows without invalid "
                        "pixel access",
                        [] {
                            for (const auto position :
                                 {MenuPosition{-100, 85}, MenuPosition{0, 85}, MenuPosition{150, 85}}) {
                                CpuPanel panel;
                                auto model = Fixture("stealth-refresh-unlocked.json");
                                model.appearance.menuWidth = 200;
                                model.appearance.menuPosition = position;
                                model.appearance.largeCursor = true;
                                model.appearance.coloredCursor = false;
                                model.selectedIndex = 11;
                                const auto actual = panel.Draw(model, 320, 240);
                                const auto hud = CpuPanel::Hud(model, 320, 240);
                                auto expected = model;
                                expected.rows[11].label = OracleLabel(model, hud, 11);
                                SamePixels(actual, panel.Draw(expected, 320, 240), hud.body, 320,
                                           "Clipped row glyph oracle differs");
                            }
                        });
    checks.emplace_back(
        "Values larger than the body stay honestly clipped at their fixed right "
        "anchor",
        [] {
            CpuPanel panel;
            auto model = Fixture("stealth-refresh-unlocked.json");
            model.appearance.menuWidth = 200;
            model.rows[11].label.clear();
            model.rows[11].value = std::string(100, '8');
            const auto actual = panel.Draw(model);
            const auto hud = CpuPanel::Hud(model);
            Require(CpuPanel::Width(hud.rowFont, model.rows[11].value) > hud.body.width,
                    "Oversized value fixture still fits");
            const auto reference = panel.Draw(model, 960, 720, true);
            const float center = hud.body.y + (hud.rowCenterOffset + 18 * (11 - hud.first)) * hud.scale;
            SamePixels(actual, reference, hud.body, 960, "Oversized value was silently shifted or shortened",
                       center, 2 * hud.scale);
        });
    checks.emplace_back("Repeated fitting warms stable glyphs without growing "
                        "the atlas each frame",
                        [] {
                            CpuPanel panel;
                            auto model = Fixture("stealth-refresh-unlocked.json");
                            model.appearance.menuWidth = 200;
                            model.appearance.largeCursor = true;
                            model.selectedIndex = 11;
                            const auto first = panel.Draw(model);
                            const auto warm = panel.TextureWork();
                            for (unsigned frame = 0; frame < 20; ++frame) {
                                Require(panel.Draw(model) == first, "Repeated fitting changed stable pixels");
                            }
                            Require(panel.TextureWork() == warm, "Fitting kept mutating a warmed font atlas");
                        });
}
} // namespace iw::overlay::tests
