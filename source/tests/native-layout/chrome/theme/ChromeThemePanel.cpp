#include "ChromeThemePanel.h"
namespace iw::overlay::tests::chrome {
ChromePanel::ChromePanel() {
    context_ = ImGui::CreateContext(); ImGui::SetCurrentContext(context_);
    auto& io = ImGui::GetIO(); io.IniFilename = nullptr; io.LogFilename = nullptr;
    io.BackendFlags |= ImGuiBackendFlags_RendererHasTextures | ImGuiBackendFlags_RendererHasVtxOffset;
    ImGui::GetPlatformIO().Renderer_TextureMaxWidth = 4096; ImGui::GetPlatformIO().Renderer_TextureMaxHeight = 4096;
}
ChromePanel::~ChromePanel() { ImGui::DestroyContext(context_); }
ImDrawData& ChromePanel::Draw(const MenuSnapshot& state, std::uint64_t clock, std::optional<layout::Color> sample) {
    ImGui::SetCurrentContext(context_); auto& io = ImGui::GetIO(); io.DisplaySize = {640,480}; io.DeltaTime = 1.f/60;
    colors = history_.Update(state,sample,clock); ImGui::NewFrame();
    // CPU pixels use the same owned color update and production painter as the presenting thread
    DrawApparitionPanel(state,nullptr,std::nullopt,sample,nullptr,std::nullopt,std::nullopt,true,std::nullopt,colors ? &*colors : nullptr);
    ImGui::Render(); auto& data = *ImGui::GetDrawData(); textures.Update(data); return data;
}
MenuSnapshot ChromeModel(MenuDesign design) {
    MenuSnapshot s; s.isOpen=true; s.title="Owned chrome";s.accent="#FF0000";s.appearance.design=design;
    s.appearance.spotlightCursor=false;s.rows={{"Current row",">",true}};s.menuChromeRevision=1;
    s.localMenuEligibility=LocalMenuEligibility{7,PlayerLifeState::Alive,true,false,false};
    s.chromeTheme=ChromeThemeState{true,1,7,design,ChromeRgb{0,0,255},false,ChromeCreationMask(design),true,{}};return s;
}
bool HasSolidColor(const ImDrawData& data, ImU32 color, const layout::Rect& region) {
    const auto white=ImGui::GetFontTexUvWhitePixel();
    for (const auto* list : data.CmdLists) for (const auto& vertex : list->VtxBuffer)
        if (vertex.col==color && vertex.uv.x==white.x && vertex.uv.y==white.y
            && vertex.pos.x>=region.x && vertex.pos.x<=region.Right() && vertex.pos.y>=region.y && vertex.pos.y<=region.Bottom()) return true;
    return false;
}
}
