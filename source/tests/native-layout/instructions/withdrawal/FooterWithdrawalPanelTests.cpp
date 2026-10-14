#include "FooterWithdrawalPanelTests.h"
#include "../InstructionFixturePanel.h"
#include "../../palette/PalettePanel.h"
#include "../../../../native/render/instructions/InstructionHudLifetime.h"
namespace iw::overlay::tests {
namespace {
MenuSnapshot Owned(bool open) {
    auto state=palette::PaletteModel(MenuDesign::Aio);state.isOpen=open;
    state.localMenuEligibility=LocalMenuEligibility{7,PlayerLifeState::Alive,true,false,false};
    state.instructionHud=InstructionHud{true,2,7,MenuDesign::Aio,false,true,
        {"Saved chord: Open Main Menu"},"#3998FE",false};
    state.instructionTheme=InstructionThemeState{2,true,
        {{1,InstructionThemeTarget::Background,{255,0,0},0,std::nullopt,false}}};
    return state;
}
bool FooterInk(const ImDrawData& data) {
    for(const auto* list:data.CmdLists) for(const auto& vertex:list->VtxBuffer)
        if(vertex.pos.y>=430) return true;
    return false;
}
bool Solid(const ImDrawData& data,ImU32 color) {
    const auto white=ImGui::GetFontTexUvWhitePixel();
    for(const auto* list:data.CmdLists) for(const auto& vertex:list->VtxBuffer)
        if(vertex.pos.y>=430 && vertex.col==color && vertex.uv.x==white.x && vertex.uv.y==white.y) return true;
    return false;
}
}
void AddFooterWithdrawalPanelChecks(std::vector<std::pair<std::string,std::function<void()>>>& checks) {
    checks.emplace_back("Qualified footer withdrawal suppresses open and closed paint without sampling or losing retained creation",[] {
        using palette::RequirePalette;
        for(bool open:{false,true}) {
            InstructionHudLifetime lifetime;instructions::InstructionFixturePanel panel;
            auto state=Owned(open);
            panel.Draw(state,lifetime.Observe(&state),std::nullopt,1000);
            // A known report advances the fixed invocation before ownership is withdrawn
            state.instructionTheme->events[0].elapsedMilliseconds=100;
            const auto& before=panel.Draw(state,lifetime.Observe(&state),std::nullopt,1100);
            RequirePalette(Solid(before,IM_COL32(51,0,0,255)),"Known fixed age did not seed the retained footer");
            auto hidden=state;hidden.localMenuEligibility->revision=8;
            hidden.localMenuEligibility->life=PlayerLifeState::Unknown;
            hidden.instructionHud=InstructionHud{};hidden.instructionTheme.reset();
            hidden.smoothRainbowTheme=true;hidden.accent="#00FF00";
            // A stale Rainbow sample cannot mutate either channel while the canonical HUD is absent
            const auto& withdrawn=panel.Draw(hidden,lifetime.Observe(&hidden),layout::Color{0,255,0,255},1200);
            RequirePalette(!FooterInk(withdrawn),"Unknown footer emitted legacy glyphs or rectangles");
            panel.ResetGraphics();
            hidden.isOpen=false;
            RequirePalette(!FooterInk(panel.Draw(hidden,lifetime.Observe(&hidden),layout::Color{255,255,0,255},1250)),
                "Graphics reset or closed withdrawal invented footer ownership");
            // Focus projection withdraws wire paint without changing the private creation
            auto focus=hidden;focus.instructionHud.reset();
            RequirePalette(!FooterInk(panel.Draw(focus,lifetime.Observe(&focus),layout::Color{255,255,255,255},1275)),
                "Focus projection invented a closed footer");
            state.localMenuEligibility->revision=9;state.instructionHud->admissionRevision=9;
            state.instructionTheme->events[0].elapsedMilliseconds=300;
            const auto& restored=panel.Draw(state,lifetime.Observe(&state),std::nullopt,1300);
            RequirePalette(Solid(restored,IM_COL32(57,152,254,255)),"Recovery lost original creation outline RGB");
            RequirePalette(Solid(restored,IM_COL32(153,0,0,255)),"Recovery reset fixed age or sampled withdrawn Rainbow");
            RequirePalette(palette::HasGlyphColor(restored,IM_COL32_WHITE,{0,430,640,50}),"Recovered footer lost its saved hints");
            // A changed life branch is represented by a new creation and starts its own color history
            state.localMenuEligibility->life=PlayerLifeState::Dead;state.localMenuEligibility->revision=10;
            state.instructionHud->creationRevision=3;state.instructionHud->admissionRevision=10;
            state.instructionHud->creationDesign=MenuDesign::Classic;state.instructionHud->creationAccent="#00FF00";
            state.instructionTheme=InstructionThemeState{3,true,{}};
            const auto& dead=panel.Draw(state,lifetime.Observe(&state),std::nullopt,1400);
            RequirePalette(Solid(dead,IM_COL32(0,255,0,255)) && Solid(dead,IM_COL32(25,25,25,217)),
                "New dead creation reused the prior AIO style or colors");
        }
    });
}
}
