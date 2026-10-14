#include "ActualChromeThemePanelTests.h"
#include "ChromeThemePanel.h"
#include "../../palette/PalettePanel.h"
#include "../../../native-runtime/chrome/theme/fixtures/ChromeThemeFixtureTests.h"
#include <algorithm>
#include <memory>
namespace iw::overlay::tests {
void AddActualChromeThemePanelChecks(std::vector<std::pair<std::string,std::function<void()>>>& checks,const std::filesystem::path& directory) {
    checks.emplace_back("Actual chrome creation journals paint independent role RGB and alpha without replacing current row authority", [directory] {
        using namespace chrome_theme;using palette::RequirePalette;CheckInventory(directory);
        std::unique_ptr<chrome::ChromePanel> panel;unsigned graph=99;std::uint64_t nativeClock=0;
        for (const auto& expected:Profiles) {
            if (graph!=expected.graph) { graph=expected.graph;panel=std::make_unique<chrome::ChromePanel>();nativeClock=0; }
            const auto state=ReadAndCheck(directory,expected);nativeClock=std::max(nativeClock,expected.clock);
            const auto& data=panel->Draw(state,nativeClock,layout::Color{80,160,240,255});CheckColors(expected,panel->colors);
            const auto hud=layout::MakeHud({640,480,state.rows.size(),state.selectedIndex,260,3,false,0,state.appearance,
                state.widthEditor.has_value(),std::nullopt,state.instructionPositionEditor.has_value(),false,state.menuPositionEditor.has_value(),state.menuCredits.has_value()});
            if (expected.creation && expected.graph<5) {
                const auto title=expected.colors[2];
                RequirePalette(palette::HasGlyphColor(data,IM_COL32(title.red,title.green,title.blue,title.alpha),hud.titleClip),"Actual title bypassed retained source RGB/alpha");
                if (state.appearance.design==MenuDesign::Classic || state.appearance.design==MenuDesign::Native || state.appearance.design==MenuDesign::Aio) {
                    const auto color=expected.colors[0];
                    RequirePalette(chrome::HasSolidColor(data,IM_COL32(color.red,color.green,color.blue,color.alpha),hud.scroller),"Actual scroller bypassed retained source RGB/alpha");
                }
                if (state.appearance.design==MenuDesign::Apparition || state.appearance.design==MenuDesign::Aio) {
                    const auto color=expected.colors[3];
                    RequirePalette(chrome::HasSolidColor(data,IM_COL32(color.red,color.green,color.blue,color.alpha),hud.separator),"Actual separator bypassed retained source RGB");
                }
                // Current selected labels still use actual current palette rather than chrome's slower color
                if (expected.name.ends_with("-red0.json") && state.appearance.coloredCursor)
                    RequirePalette(palette::HasGlyphColor(data,IM_COL32(255,0,0,255),hud.body),"Chrome fade incorrectly delayed current row ink");
            }
            if (expected.name=="chrome-theme-width-return-rollback.json")
                RequirePalette(palette::HasGlyphColor(data,IM_COL32(255,0,0,255),hud.titleClip),"Actual retained unlock rollback lost its existing owned title");
            if (expected.name=="chrome-theme-unknown-life.json")
                RequirePalette(!palette::HasGlyphColor(data,IM_COL32(128,97,163,255),hud.titleClip),"Unknown actor proof retained visible owned chrome");
            if (expected.name=="chrome-theme-closed.json" || expected.name=="chrome-theme-owner-retired.json")
                RequirePalette(!palette::HasGlyphColor(data,IM_COL32(255,0,0,255),hud.titleClip),"Closed actor scope painted ordinary chrome/title");
        }
    });
}
}
