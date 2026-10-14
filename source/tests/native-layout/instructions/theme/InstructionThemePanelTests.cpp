#include "InstructionThemePanelTests.h"
#include "../InstructionFixturePanel.h"
#include "../../palette/PalettePanel.h"
#include "../../../../native/render/instructions/InstructionHudLifetime.h"
namespace iw::overlay::tests {
namespace {
MenuSnapshot Owned(bool open=false) {
    auto state=palette::PaletteModel(MenuDesign::Aio);state.isOpen=open;
    state.localMenuEligibility=LocalMenuEligibility{7,PlayerLifeState::Alive,true,false,false};
    state.instructionHud=InstructionHud{true,2,7,MenuDesign::Aio,false,true,{"Saved chord: Open Main Menu"},"#3998FE",false};
    state.instructionTheme=InstructionThemeState{2,true,{}};return state;
}
InstructionThemeEvent Event(std::uint64_t id,InstructionThemeTarget target,InstructionRgb rgb,std::uint64_t age,
    std::optional<unsigned> gap=std::nullopt,bool stopped=false){return {id,target,rgb,age,gap,stopped};}
bool Solid(const ImDrawData& data,layout::Color color) {
    const auto white=ImGui::GetFontTexUvWhitePixel();const auto packed=IM_COL32(color.red,color.green,color.blue,color.alpha);
    for(const auto* list:data.CmdLists)for(const auto& vertex:list->VtxBuffer)
        if(vertex.pos.y>=430&&vertex.uv.x==white.x&&vertex.uv.y==white.y&&vertex.col==packed)return true;
    return false;
}
bool Pixel(const std::vector<unsigned char>& pixels,layout::Color color) {
    for(unsigned y=430;y<480;++y)for(unsigned x=0;x<640;++x){const auto i=(y*640+x)*3;
        if(pixels[i]==color.red&&pixels[i+1]==color.green&&pixels[i+2]==color.blue)return true;}
    return false;
}
void Paint(instructions::InstructionFixturePanel& panel,const MenuSnapshot& state,InstructionHudLifetime& lifetime,
    std::uint64_t now,layout::Color outline,layout::Color background,std::optional<layout::Color> sample=std::nullopt) {
    const auto& data=panel.Draw(state,lifetime.Observe(&state),sample,now);
    const auto pixels=panel.textures.Raster(data,640,480);
    palette::RequirePalette(Solid(data,outline)&&Pixel(pixels,outline),"Captured outline primitive or independent RGB pixels are missing");
    palette::RequirePalette(Solid(data,background),"Background primitive used the wrong independent RGB or alpha");
    if(background.alpha==255)palette::RequirePalette(Pixel(pixels,background),"Background pixel oracle disagrees with the retained channel");
    palette::RequirePalette(palette::HasGlyphColor(data,IM_COL32_WHITE,{0,430,640,50}),"Footer text lost white source ink");
}
}
void AddInstructionThemePanelChecks(std::vector<std::pair<std::string,std::function<void()>>>& checks) {
    checks.emplace_back("Owned callback receipts paint captured targets and independent500ms colors in open and closed footers",[] {
        for(bool open:{false,true}) {
            InstructionHudLifetime lifetime;instructions::InstructionFixturePanel panel;auto state=Owned(open);
            Paint(panel,state,lifetime,1000,{57,152,254,255},{0,0,0,255});
            state.accent="#FF0000";
            // Explicit empty authority prevents an accent change from inventing a callback
            Paint(panel,state,lifetime,1100,{57,152,254,255},{0,0,0,255});
            state.instructionTheme->events={Event(1,InstructionThemeTarget::Background,{255,0,0},0)};
            Paint(panel,state,lifetime,1200,{57,152,254,255},{0,0,0,255});
            state.menuDesignFooterDesign=MenuDesign::Classic;state.instructionTheme->events[0].elapsedMilliseconds=250;
            Paint(panel,state,lifetime,1450,{57,152,254,255},{128,0,0,255});
            state.instructionTheme->events[0].elapsedMilliseconds=500;
            Paint(panel,state,lifetime,1700,{57,152,254,255},{255,0,0,255});
            // Later current Design cannot redirect the immutable outline invocation into the fill
            state.menuDesignFooterDesign=MenuDesign::Aio;
            state.instructionTheme->events={Event(1,InstructionThemeTarget::Background,{255,0,0},1000),
                Event(2,InstructionThemeTarget::Outline,{0,255,0},250)};
            Paint(panel,state,lifetime,2200,{29,204,127,255},{255,0,0,255});
            panel.ResetGraphics();Paint(panel,state,lifetime,2200,{29,204,127,255},{255,0,0,255});
            if(!open)for(const auto* list:ImGui::GetDrawData()->CmdLists)for(const auto& vertex:list->VtxBuffer)
                palette::RequirePalette(vertex.pos.y>=430,"Closed theme receipts drew body or status chrome");
        }
    });
    checks.emplace_back("Observed Rainbow seeds survive focus while unavailable history and mutated receipts never fabricate colors",[] {
        InstructionHudLifetime lifetime;instructions::InstructionFixturePanel panel;auto state=Owned();
        state.smoothRainbowTheme=true;Paint(panel,state,lifetime,1000,{57,152,254,255},{80,160,240,255},layout::Color{80,160,240,255});
        auto hidden=state;hidden.instructionHud=InstructionHud{};hidden.instructionTheme.reset();
        panel.Draw(hidden,lifetime.Observe(&hidden),std::nullopt,1100);
        state.smoothRainbowTheme=false;state.instructionTheme->events={Event(1,InstructionThemeTarget::Background,{255,0,0},250,std::nullopt,true)};
        Paint(panel,state,lifetime,1250,{57,152,254,255},{168,80,120,255});
        // A new native observer cannot recover a Rainbow sample that was never observed
        InstructionHudLifetime lateLifetime;instructions::InstructionFixturePanel late;
        Paint(late,state,lateLifetime,5000,{57,152,254,255},{0,0,0,255});
        state.instructionTheme->events[0].elapsedMilliseconds=500;
        Paint(late,state,lateLifetime,5250,{57,152,254,255},{255,0,0,255});
        state.instructionTheme->events[0].accent={0,255,0};
        const auto& corrupt=late.Draw(state,lateLifetime.Observe(&state),std::nullopt,5300);
        palette::RequirePalette(!palette::HasGlyphColor(corrupt,IM_COL32_WHITE,{0,430,640,50}),"Mutated event retained drawable footer authority");
        InstructionHudLifetime changedLifetime;instructions::InstructionFixturePanel changed;
        auto changedState=Owned();changedState.menuDesignFooterDesign=MenuDesign::Classic;changedState.smoothRainbowTheme=true;
        Paint(changed,changedState,changedLifetime,7000,{80,160,240,255},{0,0,0,255},layout::Color{80,160,240,255});
        changedState.menuDesignFooterDesign=MenuDesign::Aio;changedState.smoothRainbowTheme=false;
        changedState.instructionTheme->events={Event(1,InstructionThemeTarget::Background,{255,0,0},250,std::nullopt,true)};
        Paint(changed,changedState,changedLifetime,7250,{80,160,240,255},{0,0,0,255});
        changedState.instructionTheme->events[0].elapsedMilliseconds=500;
        Paint(changed,changedState,changedLifetime,7500,{80,160,240,255},{255,0,0,255});
        state=Owned();state.instructionHud->creationRevision=3;state.instructionTheme->creationRevision=3;
        state.instructionHud->creationBackground=false;
        const auto& noBackground=panel.Draw(state,lifetime.Observe(&state),std::nullopt,6000);
        palette::RequirePalette(palette::HasGlyphColor(noBackground,IM_COL32_WHITE,{0,430,640,50}),"No-background empty journal suppressed valid source text");
        palette::RequirePalette(!Solid(noBackground,{57,152,254,255}),"Absent created rectangle received an outline command");
    });
}
}
