#include "InstructionColorPanelTests.h"
#include "../../palette/PalettePanel.h"
#include "../../../../native/render/instructions/InstructionHudLifetime.h"
#include "../../../../native/render/instructions/color/InstructionHudColorHistory.h"
namespace iw::overlay::tests {
namespace {
MenuSnapshot Owned(bool open = false) {
    auto state=palette::PaletteModel(MenuDesign::Aio);state.isOpen=open;
    state.localMenuEligibility=LocalMenuEligibility{7,PlayerLifeState::Alive,true,false,false};
    state.instructionHud=InstructionHud{true,2,7,MenuDesign::Aio,false,true,{"Saved chord: Open Main Menu"},"#3998FE",false};
    return state;
}
void CheckColor(layout::Color color, unsigned red, unsigned green, unsigned blue, unsigned alpha=255) {
    palette::RequirePalette(color.red==red && color.green==green && color.blue==blue && color.alpha==alpha,
        "Retained instruction color used the wrong channel or alpha");
}
class ColorPanel final {
public:
    ColorPanel() { ResetGraphics(); }
    ~ColorPanel() { ImGui::DestroyContext(context_); }
    void ResetGraphics() {
        if(context_) ImGui::DestroyContext(context_);
        context_=ImGui::CreateContext();ImGui::SetCurrentContext(context_);
        auto& io=ImGui::GetIO();io.IniFilename=nullptr;io.LogFilename=nullptr;
        io.BackendFlags |= ImGuiBackendFlags_RendererHasTextures | ImGuiBackendFlags_RendererHasVtxOffset;
        ImGui::GetPlatformIO().Renderer_TextureMaxWidth=4096;ImGui::GetPlatformIO().Renderer_TextureMaxHeight=4096;
    }
    ImDrawData& Draw(const MenuSnapshot& state, std::optional<layout::Color> sample=std::nullopt) {
        const bool admitted=lifetime.Observe(&state);
        const auto colors=history.Update(state,admitted,sample);
        ImGui::SetCurrentContext(context_);auto& io=ImGui::GetIO();io.DisplaySize={640,480};io.DeltaTime=1.f/60;
        ImGui::NewFrame();
        DrawApparitionPanel(state,nullptr,std::nullopt,state.smoothRainbowTheme?sample:std::nullopt,
            nullptr,std::nullopt,std::nullopt,admitted,colors);
        ImGui::Render();auto& data=*ImGui::GetDrawData();textures.Update(data);return data;
    }
    CpuTextures textures;
    InstructionHudLifetime lifetime;
    InstructionHudColorHistory history;
private:
    ImGuiContext* context_=nullptr;
};
bool Solid(const ImDrawData& data, ImU32 color) {
    const auto white=ImGui::GetFontTexUvWhitePixel();
    for(const auto* list:data.CmdLists) for(const auto& vertex:list->VtxBuffer)
        if(vertex.col==color && vertex.uv.x==white.x && vertex.uv.y==white.y && vertex.pos.y>=430) return true;
    return false;
}
}
void AddInstructionColorPanelChecks(std::vector<std::pair<std::string,std::function<void()>>>& checks) {
    checks.emplace_back("Instruction color history routes current-design callbacks to retained original RGB channels",[] {
        InstructionHudLifetime lifetime;InstructionHudColorHistory history;auto state=Owned();
        auto colors=history.Update(state,lifetime.Observe(&state));
        palette::RequirePalette(colors.has_value(),"Fixed creation color was not owned");
        CheckColor(colors->outline,57,152,254);CheckColor(colors->background,0,0,0);
        state.accent="#FF0000";colors=history.Update(state,lifetime.Observe(&state));
        CheckColor(colors->outline,57,152,254);CheckColor(colors->background,255,0,0);
        state.smoothRainbowTheme=true;state.accent="#00FF00";
        colors=history.Update(state,lifetime.Observe(&state));
        CheckColor(colors->outline,57,152,254);CheckColor(colors->background,255,0,0);
        state.smoothRainbowTheme=true;colors=history.Update(state,lifetime.Observe(&state),layout::Color{242,0,0,255});
        CheckColor(colors->outline,57,152,254);CheckColor(colors->background,242,0,0);
        state.menuDesignFooterDesign=MenuDesign::Classic;
        colors=history.Update(state,lifetime.Observe(&state),layout::Color{121,242,0,255});
        CheckColor(colors->outline,121,242,0);CheckColor(colors->background,242,0,0);
        state.menuDesignFooterDesign=MenuDesign::Aio;
        colors=history.Update(state,lifetime.Observe(&state),layout::Color{0,121,242,255});
        CheckColor(colors->outline,121,242,0);CheckColor(colors->background,0,121,242);
        auto hidden=state;hidden.instructionHud=InstructionHud{};
        palette::RequirePalette(!history.Update(hidden,lifetime.Observe(&hidden)),"Withdrawal retained drawable color authority");
        colors=history.Update(state,lifetime.Observe(&state),layout::Color{121,0,242,255});
        CheckColor(colors->outline,121,242,0);
        // A missing first sample cannot silently turn captured fallback accent into Rainbow RGB
        state=Owned();state.instructionHud->creationRevision=3;state.instructionHud->creationRainbow=true;
        state.accent="#00FF00";state.smoothRainbowTheme=false;
        palette::RequirePalette(!history.Update(state,lifetime.Observe(&state)),"Rainbow creation guessed a sample");
        colors=history.Update(state,lifetime.Observe(&state),layout::Color{10,20,30,255});
        CheckColor(colors->outline,10,20,30);
        state.menuDesignFooterDesign=MenuDesign::Classic;
        colors=history.Update(state,lifetime.Observe(&state),layout::Color{90,80,70,255});
        CheckColor(colors->outline,10,20,30);
    });
    checks.emplace_back("Owned instruction colors paint open and closed AIO outlines without changing text or current theme",[] {
        ColorPanel panel;
        for(bool open:{false,true}) {
            auto state=Owned(open);panel.Draw(state);state.accent="#FF0000";
            auto& data=panel.Draw(state);
            palette::RequirePalette(Solid(data,IM_COL32(57,152,254,255)),"AIO outline followed a later fixed accent");
            palette::RequirePalette(palette::HasGlyphColor(data,IM_COL32_WHITE,{0,440,640,40}),"Instruction text lost white source ink");
            palette::RequirePalette(state.accent=="#FF0000","Historical paint changed current accent");
            panel.ResetGraphics();const auto& resetData=panel.Draw(state);
            palette::RequirePalette(Solid(resetData,IM_COL32(57,152,254,255)),"Graphics-style reset erased retained creation RGB");
        }
        auto state=Owned();state.instructionHud->creationRevision=3;state.instructionHud->creationDesign=MenuDesign::Classic;
        state.appearance.design=MenuDesign::Classic;state.appearance.coloredCursor=false;
        panel.Draw(state);state.smoothRainbowTheme=true;state.menuDesignFooterDesign=MenuDesign::Aio;
        auto& data=panel.Draw(state,layout::Color{121,242,0,255});
        palette::RequirePalette(Solid(data,IM_COL32(121,242,0,217)),"Rainbow background assignment lost Classic creation alpha");
        palette::RequirePalette(Solid(data,IM_COL32(57,152,254,255)),"Current AIO callback changed the old outline");
    });
    checks.emplace_back("Instruction creation color metadata stays immutable with semantic RGB equality and legacy compatibility",[] {
        InstructionHudLifetime lifetime;auto state=Owned();
        palette::RequirePalette(lifetime.Observe(&state),"Initial captured color was not admitted");
        state.instructionHud->creationAccent="#3998fe";
        palette::RequirePalette(lifetime.Observe(&state),"Equivalent hexadecimal case changed RGB ownership");
        state.instructionHud->creationAccent="#FF0000";
        palette::RequirePalette(!lifetime.Observe(&state),"Same-ID creation RGB mutation was admitted");
        state.instructionHud->creationAccent="#3998FE";
        palette::RequirePalette(!lifetime.Observe(&state),"Quarantined color recovered without a new creation");
        state.instructionHud->creationRevision=3;
        palette::RequirePalette(lifetime.Observe(&state),"New creation could not replace quarantined color");
        state.instructionHud->creationRainbow=true;
        palette::RequirePalette(!lifetime.Observe(&state),"Same-ID creation mode mutation was admitted");
        state.instructionHud->creationRevision=4;state.instructionHud->creationAccent.reset();state.instructionHud->creationRainbow.reset();
        palette::RequirePalette(lifetime.Observe(&state),"Six-field legacy creation was rejected");
        InstructionHudColorHistory history;
        palette::RequirePalette(!history.Update(state,true,layout::Color{1,2,3,255}),"Legacy metadata fabricated historical color");
        state.instructionHud->creationAccent="#3998FE";state.instructionHud->creationRainbow=false;
        palette::RequirePalette(!lifetime.Observe(&state),"Same-ID legacy authority gained captured facts");
    });
}
}
