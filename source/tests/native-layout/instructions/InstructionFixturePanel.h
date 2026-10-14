#pragma once
#include "../CpuDrawlist.h"
#include "../../../native/render/ApparitionPanel.h"
namespace iw::overlay::tests::instructions {
class InstructionFixturePanel final {
public:
    InstructionFixturePanel();
    ~InstructionFixturePanel();
    ImDrawData& Draw(const MenuSnapshot& state, bool admitted,
        std::optional<layout::Color> nativeSample = std::nullopt, std::uint64_t nowMilliseconds = 0);
    void ResetGraphics();
    CpuTextures textures;
    bool drawn = false;
    PanelDrawSummary summary;
private:
    ImGuiContext* context_=nullptr;
    InstructionHudColorHistory colorHistory_;
};
}
