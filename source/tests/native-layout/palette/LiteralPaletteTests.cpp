#include "LiteralPaletteTests.h"
#include "PalettePanel.h"
#include <array>

namespace iw::overlay::tests {
namespace {
using namespace palette;
constexpr std::array<const char*, 9> FixedThemes{
    "#0000FF", "#000000", "#00008B", "#191970", "#8000FF", "#4B0082",
    "#016E2D", "#004AE9", "#015A94"};
ImU32 Ink(layout::Color color) { return IM_COL32(color.red, color.green, color.blue, color.alpha); }
layout::Hud HudFor(const MenuSnapshot& state) {
    return layout::MakeHud({640, 480, state.rows.size(), state.selectedIndex, 260, 3, false, 0, state.appearance});
}
layout::Rect RowArea(const layout::Hud& hud, unsigned row, float left, float width) {
    return {left, hud.body.y + (hud.rowCenterOffset - 8 + row * 18) * hud.scale, width, 18 * hud.scale};
}
void CheckThemeGlyphs(PalettePanel& panel, MenuSnapshot state, layout::Color theme,
    std::optional<layout::Color> sample = std::nullopt) {
    const auto& data = panel.Draw(state, sample); const auto hud = HudFor(state);
    RequirePalette(HasGlyphColor(data, Ink(theme), hud.titleClip), "A source title substituted white for literal MainTheme");
    RequirePalette(HasGlyphColor(data, Ink(theme), RowArea(hud, 0, hud.body.x + 4, 150)),
        "A selected source label substituted its MainTheme RGB");
    RequirePalette(HasGlyphColor(data, Ink(theme), RowArea(hud, 0, hud.body.Right() - 20, 20)),
        "A selected source value substituted its MainTheme RGB");
}
}
void AddLiteralPaletteChecks(std::vector<std::pair<std::string, std::function<void()>>>& checks) {
    checks.emplace_back("Fixed source palette glyphs stay literal across dark colors and former contrast boundaries", [] {
        PalettePanel panel;
        for (const auto design : {MenuDesign::Apparition, MenuDesign::Basic}) {
            for (const auto* hex : FixedThemes) {
                auto state = PaletteModel(design); state.accent = hex;
                CheckThemeGlyphs(panel, state, layout::Theme(hex));
            }
        }
    });
    checks.emplace_back("Supplied Rainbow and fixed themes share literal source glyph channels", [] {
        PalettePanel panel;
        for (const auto color : {layout::Color{0,0,0,255}, layout::Color{0,0,242,255}, layout::Color{17,35,61,255}}) {
            auto state = PaletteModel(); state.smoothRainbowTheme = true;
            CheckThemeGlyphs(panel, state, color, color);
            RequirePalette(HasGlyphColor(panel.Draw(state, color), IM_COL32_WHITE, {274,85,356,18}),
                "A dark Rainbow sample hid the separate IW status header");
        }
        auto fixed = PaletteModel(); fixed.accent = "#000000";
        const auto& ordinary = panel.Draw(fixed); const auto pixels = panel.textures.Raster(ordinary,640,480);
        const auto& supplied = panel.Draw(fixed, layout::Color{242,0,0,255});
        RequirePalette(panel.textures.Raster(supplied,640,480) == pixels, "A fixed theme adopted an unowned Rainbow sample");
    });
    checks.emplace_back("Classic Native and AIO preserve their source white creation text", [] {
        PalettePanel panel;
        for (const auto design : {MenuDesign::Classic, MenuDesign::Native, MenuDesign::Aio}) {
            auto state = PaletteModel(design); state.accent = "#00008B";
            const auto& data = panel.Draw(state); const auto hud = HudFor(state);
            RequirePalette(HasGlyphColor(data, IM_COL32(255,255,255,hud.titleAlpha), hud.titleClip),
                "A white source title adopted a fixed palette color");
            if (design != MenuDesign::Aio)
                RequirePalette(HasGlyphColor(data, IM_COL32_WHITE, RowArea(hud,0,hud.body.x+4,150)),
                    "A design without Colored Cursor changed source white row text");
        }
    });
    checks.emplace_back("Unselected and cursor-disabled rows stay white independently of dark palette selection", [] {
        PalettePanel panel; auto state = PaletteModel(); state.accent = "#00008B";
        const auto hud = HudFor(state); const auto& selected = panel.Draw(state);
        RequirePalette(HasGlyphColor(selected, IM_COL32_WHITE, RowArea(hud,1,4,150)),
            "Unselected source text was recolored by the palette");
        state.appearance.coloredCursor = false;
        const auto& disabled = panel.Draw(state);
        RequirePalette(HasGlyphColor(disabled, IM_COL32_WHITE, RowArea(hud,0,4,150))
            && HasGlyphColor(disabled, IM_COL32_WHITE, RowArea(hud,0,240,20)),
            "Disabling Colored Cursor failed to preserve white labels and values");
    });
    checks.emplace_back("Text Color ON stays green and confirmed OFF stays literal while UNKNOWN remains diagnostic", [] {
        PalettePanel panel; auto state = PaletteModel(); state.accent = "#00008B";
        state.appearance.booleanDisplay = "Text Color"; state.rows[0].value = "ON";
        const auto hud = HudFor(state);
        RequirePalette(HasGlyphColor(panel.Draw(state), IM_COL32(0,255,0,255), RowArea(hud,0,4,150)),
            "Text Color ON lost its explicit green source override");
        state.rows[0].value = "OFF";
        RequirePalette(HasGlyphColor(panel.Draw(state), IM_COL32(0,0,139,255), RowArea(hud,0,4,150)),
            "Text Color OFF substituted a readable palette color");
        state.rows[0].value = "UNKNOWN";
        const auto& unknown = panel.Draw(state);
        RequirePalette(HasGlyphColor(unknown, IM_COL32_WHITE, RowArea(hud,0,160,100)),
            "Text Color UNKNOWN lost its explicit value glyphs");
        state.accent = "#000000";
        for (const auto* mode : {"Boxes", "Text", "Text Color"}) {
            state.appearance.booleanDisplay = mode;
            const auto& black = panel.Draw(state);
            RequirePalette(HasGlyphColor(black, IM_COL32_WHITE, RowArea(hud,0,160,100)),
                "Black palette hid the separate UNKNOWN diagnostic");
            RequirePalette(HasGlyphColor(black, IM_COL32_WHITE, {274,85,356,18}),
                "The IW status header lost its isolated contrast aid");
        }
    });
    checks.emplace_back("Source ON boxes contain only gray outer and literal theme inner rectangles", [] {
        PalettePanel panel;
        for (const auto* hex : FixedThemes) for (const auto* side : {"Right", "Left"}) {
            auto state = PaletteModel(); state.accent = hex; state.rows[0].value = "ON";
            state.appearance.booleanDisplay = "Boxes"; state.appearance.boolBoxLocation = side;
            const auto hud = HudFor(state); const auto center = hud.body.y + hud.rowCenterOffset * hud.scale;
            const float left = hud.boxesLeft ? hud.body.x + 4 * hud.scale : hud.body.Right() - 13 * hud.scale;
            const auto& data = panel.Draw(state); const auto pixels = panel.textures.Raster(data,640,480);
            const auto expected = layout::Theme(hex);
            const auto border = static_cast<std::size_t>((static_cast<int>(left) + (static_cast<int>(center)-5)*640)*3);
            RequirePalette(pixels[border] == 64 && pixels[border+1] == 64 && pixels[border+2] == 64,
                "The source boolean outer rectangle changed its gray RGB");
            // Interior pixels exclude the outer edge and can expose either stroke of an invented tick
            for (int y = static_cast<int>(center)-4; y < center+4; ++y) {
                for (int x = static_cast<int>(left)+1; x < left+9; ++x) {
                    const auto offset = static_cast<std::size_t>((x+y*640)*3);
                    RequirePalette(pixels[offset] == expected.red && pixels[offset+1] == expected.green
                        && pixels[offset+2] == expected.blue, "An ON box contains extra tick ink or substituted RGB");
                }
            }
            state.rows[0].value = "OFF";
            const auto& offData = panel.Draw(state); const auto off = panel.textures.Raster(offData,640,480);
            const auto interior = static_cast<std::size_t>((static_cast<int>(left)+4 + static_cast<int>(center)*640)*3);
            RequirePalette(off[interior] == 0 && off[interior+1] == 0 && off[interior+2] == 0,
                "A confirmed OFF fill adopted palette RGB");
            if (expected.red == 0 && expected.green == 0 && expected.blue == 0)
                RequirePalette(SameRegion(pixels,off,{left,center-5,10,10}), "Black source ON fabricated a visible OFF distinction");
            state.rows[0].value = "UNKNOWN";
            const auto& unknown = panel.Draw(state);
            RequirePalette(!SameRegion(panel.textures.Raster(unknown,640,480), off, {left,center-5,10,10}),
                "The separate UNKNOWN diagnostic disappeared like confirmed OFF");
        }
    });
}
}
