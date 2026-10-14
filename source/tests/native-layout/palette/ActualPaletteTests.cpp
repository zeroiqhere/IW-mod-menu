#include "ActualPaletteTests.h"
#include "PalettePanel.h"
#include "SourcePalette.h"
#include "../BackendPreview.h"
#include "../../../native/runtime/RuntimeSchema.h"
#include <fstream>
#include <map>
#include <set>

namespace iw::overlay::tests {
namespace {
using namespace palette;
struct Design { const char* name; MenuDesign value; };
constexpr std::array<Design, 5> Designs{{{"apparition",MenuDesign::Apparition}, {"classic",MenuDesign::Classic},
    {"native",MenuDesign::Native}, {"aio",MenuDesign::Aio}, {"basic",MenuDesign::Basic}}};
std::string ColorFile(const Design& design, unsigned index) {
    return "palette-" + std::string(design.name) + "-" + (index < 10 ? "0" : "") + std::to_string(index) + ".json";
}
std::string Read(const std::filesystem::path& path) {
    RequirePalette(std::filesystem::symlink_status(path).type() == std::filesystem::file_type::regular,
        "An actual palette packet is not a regular file");
    const auto size = std::filesystem::file_size(path);
    RequirePalette(size > 0 && size <= runtime::MaximumFileBytes, "An actual palette packet is empty or oversized");
    std::ifstream input(path, std::ios::binary); RequirePalette(input.good(), "An actual palette packet could not be opened");
    std::string bytes(static_cast<std::size_t>(size), '\0');
    input.read(bytes.data(), static_cast<std::streamsize>(size));
    RequirePalette(input.gcount() == static_cast<std::streamsize>(size)
        && input.peek() == std::char_traits<char>::eof(), "An actual palette packet changed during its bounded read");
    return bytes;
}
void CheckPaint(const MenuSnapshot& state, unsigned on) {
    RequirePalette(state.menuBooleanPaint && state.menuBooleanPaint->count == 37,
        "The real palette lost its copied boolean owner");
    for (unsigned row = 0; row < 37; ++row)
        RequirePalette(state.menuBooleanPaint->rows[row].index == row
            && BooleanPaintText(state.menuBooleanPaint->rows[row].value) == (row == on ? "ON" : "OFF"),
            "The actual palette changed a copied checkbox sample");
}
void CheckSteadyPixels(PalettePanel& panel, const MenuSnapshot& state, unsigned index) {
    const auto& data = panel.Draw(state);
    const auto hud = layout::MakeHud({640,480,state.rows.size(),state.selectedIndex,260,3,false,0,state.appearance});
    const auto theme = SourcePalette[index].rgb;
    const bool whiteTitle = state.appearance.design == MenuDesign::Classic || state.appearance.design == MenuDesign::Native
        || state.appearance.design == MenuDesign::Aio;
    const auto title = whiteTitle ? IM_COL32(255,255,255,hud.titleAlpha)
        : IM_COL32(theme.red,theme.green,theme.blue,theme.alpha);
    RequirePalette(HasGlyphColor(data,title,hud.titleClip), "An actual settled source title changed its palette RGB");
    const auto y = hud.body.y + (hud.rowCenterOffset + 18 * (index - hud.first)) * hud.scale;
    const layout::Rect label{hud.body.x+4,y-8,190,18};
    const bool green = state.appearance.booleanDisplay == "Text Color";
    const bool whiteRow = !state.appearance.coloredCursor || state.appearance.design == MenuDesign::Classic
        || state.appearance.design == MenuDesign::Native;
    const auto selected = green ? IM_COL32(0,255,0,255) : whiteRow ? IM_COL32_WHITE
        : IM_COL32(theme.red,theme.green,theme.blue,255);
    RequirePalette(HasGlyphColor(data,selected,label), "An actual selected source row changed its declared color channel");
    if (!green) {
        // The selected source boolean has exactly two solid rectangles, with no check mark
        const auto pixels = panel.textures.Raster(data,640,480);
        const int left = static_cast<int>(hud.body.Right()-13*hud.scale);
        for (int py = static_cast<int>(y)-4; py < y+4; ++py) {
            for (int px = left+1; px < left+9; ++px) {
                const auto offset = static_cast<std::size_t>((px+py*640)*3);
                RequirePalette(pixels[offset]==theme.red && pixels[offset+1]==theme.green && pixels[offset+2]==theme.blue,
                    "An actual ON box added non-source tick ink or substituted palette RGB");
            }
        }
    }
}
}
void AddActualLiteralPaletteChecks(std::vector<std::pair<std::string, std::function<void()>>>& checks,
    const std::filesystem::path& directory) {
    checks.emplace_back("Actual five-design palette endpoints retain literal RGB and independent callback paint timing", [directory] {
        std::set<std::string> expectedNames, actualNames;
        for (const auto& design : Designs) {
            expectedNames.emplace("palette-"+std::string(design.name)+"-created.json");
            for (unsigned index=0; index<36; ++index) expectedNames.emplace(ColorFile(design,index));
        }
        for (unsigned time : {0,179,180,429,430}) expectedNames.emplace("palette-apparition-black-"+std::to_string(time)+".json");
        expectedNames.emplace("palette-apparition-cursor-off.json");
        expectedNames.emplace("palette-apparition-textcolor-on.json");
        RequirePalette(expectedNames.size()==192 && std::filesystem::is_directory(directory), "The separate actual palette graph is missing");
        for (const auto& entry : std::filesystem::directory_iterator(directory)) {
            RequirePalette(entry.symlink_status().type()==std::filesystem::file_type::regular,
                "The actual palette inventory contains a non-regular entry");
            RequirePalette(actualNames.emplace(entry.path().filename().string()).second && actualNames.size()<=192,
                "The actual palette inventory exceeds its independent fixed bound");
        }
        RequirePalette(actualNames==expectedNames,"The actual palette inventory differs from its fixed source graph");
        std::map<std::string,runtime::ParsedState> states;
        for (const auto& name : expectedNames) {
            const auto path=directory/name; const auto bytes=Read(path);
            const auto parsed=runtime::ParseState(bytes,42); const auto& state=*parsed.snapshot;
            const auto preview=ReadBackendPreview(path);
            RequirePalette(state.isOpen && state.menuSurface==MenuSurface::Main && !state.isRootMenu
                && state.title=="Main Design Color" && state.context=="CONNECTED" && state.rows.size()==37
                && !state.smoothRainbowTheme && state.menuRevision && state.menuChromeRevision,
                "A palette graph fabricated preview facts or lost its current controller context");
            RequirePalette(preview.appearance==state.appearance && preview.accent==state.accent
                && preview.menuRevision==state.menuRevision && preview.navigationRevision==state.navigationRevision
                && preview.menuChromeRevision==state.menuChromeRevision && preview.selectedIndex==state.selectedIndex
                && preview.menuOptionCallback==state.menuOptionCallback && preview.menuBooleanPaint==state.menuBooleanPaint
                && preview.localMenuEligibility==state.localMenuEligibility,
                "Production and preview readers disagree on the actual palette ownership");
            const unsigned current = state.accent=="#3998FE" ? 6u : static_cast<unsigned>(state.selectedIndex);
            for (unsigned row=0; row<37; ++row) {
                RequirePalette(state.rows[row].value==(row==current ? "ON" : "OFF") && state.rows[row].enabled
                    && preview.rows[row].value==state.rows[row].value && preview.rows[row].enabled==state.rows[row].enabled,
                    "Literal palette drawing changed a raw current value or capability");
                if (row<36) RequirePalette(state.rows[row].label==SourcePalette[row].name,
                    "The actual palette no longer matches pinned source order");
            }
            states.emplace(name,parsed);
        }
        for (const auto& design : Designs) {
            // Each design graph owns an independent renderer context; no scalar epoch crosses owners
            PalettePanel panel;
            const auto& created=*states.at("palette-"+std::string(design.name)+"-created.json").snapshot;
            RequirePalette(created.appearance.design==design.value && created.selectedIndex==0 && created.accent=="#3998FE",
                "A created palette changed source default color or style");
            CheckPaint(created,6);
            for (unsigned index=0; index<36; ++index) {
                const auto& state=*states.at(ColorFile(design,index)).snapshot;
                RequirePalette(state.appearance.design==design.value && state.selectedIndex==index
                    && state.accent==SourcePalette[index].hex && !state.menuOptionCallback && !state.menuOpening,
                    "A source palette endpoint was not a settled actual selection");
                CheckPaint(state,index); CheckSteadyPixels(panel,state,index);
            }
        }
        for (const auto* name : {"palette-apparition-cursor-off.json","palette-apparition-textcolor-on.json"}) {
            PalettePanel panel; const auto& state=*states.at(name).snapshot;
            RequirePalette(state.appearance.design==MenuDesign::Apparition && state.selectedIndex==3 && state.accent=="#000000"
                && !state.menuOptionCallback,"An appearance variant did not settle its actual Black selection");
            CheckPaint(state,3); CheckSteadyPixels(panel,state,3);
        }
        // These callback boundaries prove actual owner/paint timing, not the unimplemented engine color fade curve
        for (unsigned time : {0,179,180,429,430}) {
            const auto& parsed=states.at("palette-apparition-black-"+std::to_string(time)+".json");
            const auto& state=*parsed.snapshot;
            RequirePalette(state.accent=="#000000" && state.selectedIndex==3 && state.navigationRevision==28
                && state.menuRevision==(time<180 ? 10u : 11u) && state.menuChromeRevision==1
                && parsed.lastSequence==26 && state.menuBooleanPaint->revision==(time<180 ? 1u : 2u),
                "The actual Black callback changed independent cursor, creation or redraw ownership");
            CheckPaint(state,time<180 ? 6 : 3);
            RequirePalette(state.menuOptionCallback.has_value()==(time<430),"The actual caller wait retired at an incorrect boundary");
            if (time<430) RequirePalette(state.menuOptionCallback->revision==1
                && state.menuOptionCallback->kind==MenuOptionCallbackKind::Boolean
                && state.menuOptionCallback->phase==(time<180 ? MenuOptionCallbackPhase::Pending : MenuOptionCallbackPhase::Cooldown),
                "The actual Black callback changed its phase or source classification");
        }
    });
}
}
