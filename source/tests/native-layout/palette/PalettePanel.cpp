#include "PalettePanel.h"
#include <cmath>
#include <stdexcept>

namespace iw::overlay::tests::palette {
PalettePanel::PalettePanel() {
    context_ = ImGui::CreateContext();
    ImGui::SetCurrentContext(context_);
    auto& io = ImGui::GetIO(); io.IniFilename = nullptr; io.LogFilename = nullptr;
    io.BackendFlags |= ImGuiBackendFlags_RendererHasTextures | ImGuiBackendFlags_RendererHasVtxOffset;
    ImGui::GetPlatformIO().Renderer_TextureMaxWidth = 4096;
    ImGui::GetPlatformIO().Renderer_TextureMaxHeight = 4096;
}
PalettePanel::~PalettePanel() { ImGui::DestroyContext(context_); }
ImDrawData& PalettePanel::Draw(const MenuSnapshot& state, std::optional<layout::Color> sample) {
    ImGui::SetCurrentContext(context_);
    auto& io = ImGui::GetIO(); io.DisplaySize = {640, 480}; io.DeltaTime = 1.f / 60;
    ImGui::NewFrame();
    DrawApparitionPanel(state, nullptr, std::nullopt, sample);
    ImGui::Render();
    auto& data = *ImGui::GetDrawData(); textures.Update(data);
    return data;
}
MenuSnapshot PaletteModel(MenuDesign design) {
    MenuSnapshot state; state.isOpen = true; state.title = "Palette";
    state.context = "OFFLINE CPU TEST"; state.appearance.design = design;
    state.appearance.spotlightCursor = false;
    state.appearance.booleanDisplay = "Text";
    state.rows = {{"Selected label", ">", true}, {"Ordinary label", ">", true}};
    return state;
}
void RequirePalette(bool value, const char* message) {
    if (!value) throw std::runtime_error(message);
}
bool HasGlyphColor(const ImDrawData& data, ImU32 color, const layout::Rect& region) {
    // Solid fills use the white texture texel, so black backgrounds cannot pass a glyph oracle
    const auto white = ImGui::GetFontTexUvWhitePixel();
    for (const auto* list : data.CmdLists) {
        for (const auto& vertex : list->VtxBuffer) {
            if (vertex.col == color && (vertex.uv.x != white.x || vertex.uv.y != white.y)
                && vertex.pos.x >= region.x && vertex.pos.x < region.Right()
                && vertex.pos.y >= region.y && vertex.pos.y < region.Bottom()) return true;
        }
    }
    return false;
}
bool SameRegion(const std::vector<unsigned char>& a, const std::vector<unsigned char>& b, const layout::Rect& region) {
    RequirePalette(a.size() == 640 * 480 * 3 && b.size() == a.size(), "Palette pixel buffers have an unexpected shape");
    for (int y = static_cast<int>(std::ceil(region.y)); y < region.Bottom(); ++y) {
        for (int x = static_cast<int>(std::ceil(region.x)); x < region.Right(); ++x) {
            if (x < 0 || x >= 640 || y < 0 || y >= 480) continue;
            const auto index = static_cast<std::size_t>((x + y * 640) * 3);
            for (unsigned channel = 0; channel < 3; ++channel)
                if (a[index + channel] != b[index + channel]) return false;
        }
    }
    return true;
}
}
