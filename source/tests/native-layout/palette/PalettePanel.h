#pragma once
#include "../CpuDrawlist.h"
#include "../../../native/render/ApparitionPanel.h"
#include <optional>

namespace iw::overlay::tests::palette {
// The same bundled fonts and production primitives are inspected without a graphics backend
class PalettePanel final {
public:
    PalettePanel();
    ~PalettePanel();
    ImDrawData& Draw(const MenuSnapshot& state, std::optional<layout::Color> sample = std::nullopt);
    CpuTextures textures;
private:
    ImGuiContext* context_;
};
MenuSnapshot PaletteModel(MenuDesign design = MenuDesign::Apparition);
bool HasGlyphColor(const ImDrawData& data, ImU32 color, const layout::Rect& region);
bool SameRegion(const std::vector<unsigned char>& a, const std::vector<unsigned char>& b, const layout::Rect& region);
void RequirePalette(bool value, const char* message);
}
