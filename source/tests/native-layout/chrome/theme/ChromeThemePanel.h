#pragma once
#include "../../CpuDrawlist.h"
#include "../../../../native/render/chrome/theme/ChromeColorHistory.h"
#include "../../../../native/render/ApparitionPanel.h"
namespace iw::overlay::tests::chrome {
class ChromePanel final {
public:
    ChromePanel();
    ~ChromePanel();
    ImDrawData& Draw(const MenuSnapshot& state, std::uint64_t clock, std::optional<layout::Color> sample = std::nullopt);
    CpuTextures textures;
    std::optional<ChromeColorFrame> colors;
private:
    ImGuiContext* context_;
    ChromeColorHistory history_;
};
MenuSnapshot ChromeModel(MenuDesign design);
bool HasSolidColor(const ImDrawData& data, ImU32 color, const layout::Rect& region);
}
