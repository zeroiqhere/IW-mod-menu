#include "ChromeThemePanelTests.h"
#include "ChromeThemePanel.h"
#include "../../palette/PalettePanel.h"
namespace iw::overlay::tests {
void AddChromeThemePanelChecks(std::vector<std::pair<std::string,std::function<void()>>>& checks) {
    checks.emplace_back("Owned chrome rectangles and titles retain creation and interrupted RGB in all five painters", [] {
        using namespace chrome;using palette::RequirePalette;
        for (auto design : {MenuDesign::Apparition,MenuDesign::Classic,MenuDesign::Native,MenuDesign::Aio,MenuDesign::Basic}) {
            ChromePanel panel;auto s=ChromeModel(design);
            const auto hud=layout::MakeHud({640,480,1,0,260,3,false,0,s.appearance});auto& data=panel.Draw(s,1000);
            const bool white=design==MenuDesign::Classic||design==MenuDesign::Native||design==MenuDesign::Aio;
            const auto title=white ? IM_COL32(255,255,255,hud.titleAlpha) : IM_COL32(0,0,255,hud.titleAlpha);
            RequirePalette(palette::HasGlyphColor(data,title,hud.titleClip),"Owned title adopted immediate current accent");
            if (design==MenuDesign::Classic||design==MenuDesign::Native||design==MenuDesign::Aio)
                RequirePalette(HasSolidColor(data,IM_COL32(0,0,255,design==MenuDesign::Native?179:255),hud.scroller),"Owned scroller adopted immediate current accent");
            s.chromeTheme->events={{1,ChromeFixedMask(design),{255,0,0},250,std::nullopt,false}};
            auto& faded=panel.Draw(s,1250);const auto alpha=design==MenuDesign::Native?179:255;
            if (!white) RequirePalette(palette::HasGlyphColor(faded,IM_COL32(128,0,128,alpha),hud.titleClip),"Title bypassed owned half-fade RGB");
            else RequirePalette(HasSolidColor(faded,IM_COL32(128,0,128,alpha),hud.scroller),"Scroller bypassed owned half-fade RGB");
        }
    });
    checks.emplace_back("Owned presence survives unlock rollback and destroyed title never returns on another page", [] {
        using namespace chrome;using palette::RequirePalette;
        ChromePanel panel;auto s=ChromeModel(MenuDesign::Basic);s.menuUnlock=MenuUnlockPhase::Rebuild;s.isRootMenu=false;
        auto& data=panel.Draw(s,1000);const auto hud=layout::MakeHud({640,480,1,0,260,3,false,0,s.appearance});
        RequirePalette(palette::HasGlyphColor(data,IM_COL32(0,0,255,255),hud.titleClip),"Legacy rebuild gate hid explicitly existing chrome");
        s.menuUnlock.reset();s.chromeTheme->currentRoleMask=3;data=panel.Draw(s,1100);
        RequirePalette(!palette::HasGlyphColor(data,IM_COL32(0,0,255,255),hud.titleClip),"Destroyed source title reappeared from page geometry");
        s.chromeTheme=ChromeThemeState{};data=panel.Draw(s,1200);
        RequirePalette(!palette::HasGlyphColor(data,IM_COL32(255,0,0,255),hud.titleClip),"Explicit owned absence fell back to current accent title");
    });
}
}
