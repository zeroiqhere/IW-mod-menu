#include "InstructionFixturePanel.h"
namespace iw::overlay::tests::instructions {
InstructionFixturePanel::InstructionFixturePanel() { ResetGraphics(); }
void InstructionFixturePanel::ResetGraphics() {
    if(context_) ImGui::DestroyContext(context_);
    context_=ImGui::CreateContext(); ImGui::SetCurrentContext(context_);
    auto& io=ImGui::GetIO(); io.IniFilename=nullptr; io.LogFilename=nullptr;
    io.BackendFlags |= ImGuiBackendFlags_RendererHasTextures | ImGuiBackendFlags_RendererHasVtxOffset;
    ImGui::GetPlatformIO().Renderer_TextureMaxWidth=4096;
    ImGui::GetPlatformIO().Renderer_TextureMaxHeight=4096;
}
InstructionFixturePanel::~InstructionFixturePanel() { ImGui::DestroyContext(context_); }
ImDrawData& InstructionFixturePanel::Draw(const MenuSnapshot& state,bool admitted,std::optional<layout::Color> nativeSample,std::uint64_t nowMilliseconds) {
    ImGui::SetCurrentContext(context_);
    auto& io=ImGui::GetIO(); io.DisplaySize={640,480};io.DeltaTime=1.f/60;
    ImGui::NewFrame();
    // This is the production footer-only branch with the same independent lifetime admission
    const auto colors=colorHistory_.Update(state,admitted,nativeSample,nowMilliseconds);
    drawn=DrawApparitionPanel(state,&summary,std::nullopt,state.smoothRainbowTheme?nativeSample:std::nullopt,
        nullptr,std::nullopt,std::nullopt,admitted && !colorHistory_.IsQuarantined(),colors);
    ImGui::Render();auto& data=*ImGui::GetDrawData(); textures.Update(data);return data;
}
}
