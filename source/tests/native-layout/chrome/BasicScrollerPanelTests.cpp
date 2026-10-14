#include "BasicScrollerPanelTests.h"
#include "../CpuDrawlist.h"
#include "../../../native/render/chrome/BasicScrollerHistory.h"
#include "../../../native/render/chrome/BasicScrollerPresence.h"
#include "../../../native/render/chrome/BasicScrollerClosedObservation.h"
#include "../../../native/render/ApparitionPanel.h"
#include <stdexcept>
namespace iw::overlay::tests {
namespace {
class Panel final {
public:
    Panel() {
        context_ = ImGui::CreateContext(); ImGui::SetCurrentContext(context_);
        auto& io = ImGui::GetIO(); io.IniFilename = nullptr; io.LogFilename = nullptr;
        io.BackendFlags |= ImGuiBackendFlags_RendererHasTextures | ImGuiBackendFlags_RendererHasVtxOffset;
        ImGui::GetPlatformIO().Renderer_TextureMaxWidth = 4096;
        ImGui::GetPlatformIO().Renderer_TextureMaxHeight = 4096;
    }
    ~Panel() { ImGui::DestroyContext(context_); }
    std::vector<unsigned char> Draw(const MenuSnapshot& state, std::optional<layout::Color> color) {
        ImGui::SetCurrentContext(context_); auto& io = ImGui::GetIO(); io.DisplaySize = {640,480}; io.DeltaTime = 1.f/60;
        ImGui::NewFrame(); DrawApparitionPanel(state,nullptr,std::nullopt,std::nullopt,nullptr,std::nullopt,color);
        ImGui::Render(); auto& data = *ImGui::GetDrawData(); textures_.Update(data); return textures_.Raster(data,640,480);
    }
private:
    ImGuiContext* context_;
    CpuTextures textures_;
};
}
void AddBasicScrollerPanelChecks(std::vector<std::pair<std::string, std::function<void()>>>& checks) {
    checks.emplace_back("Production closed observer retains valid focus epochs and retires absent or regressing ownership", [] {
        BasicScrollerHistory history; const layout::Color blue{17,67,211,255};
        MenuSnapshot closed; closed.menuChromeRevision = 2;
        history.Update(2, true, blue); ObserveClosedBasicScroller(history, &closed);
        auto color = history.Update(2, true, std::nullopt);
        if (!color || color->blue != blue.blue) throw std::runtime_error("Normal or focus-projected close erased RGB");
        // Explicit null legacy data must not revive the previous sampled color
        closed.menuChromeRevision.reset(); ObserveClosedBasicScroller(history, &closed);
        color = history.Update(2, true, std::nullopt);
        if (!color || color->blue != 0) throw std::runtime_error("Null closed owner recovered stale RGB");
        history.Update(2, true, blue); closed.menuChromeRevision = 1;
        ObserveClosedBasicScroller(history, &closed);
        if (history.Update(2, true, std::nullopt)) throw std::runtime_error("Regressing closed owner revived RGB");
        history.Update(3, true, blue); ObserveClosedBasicScroller(history, nullptr);
        color = history.Update(3, true, std::nullopt);
        if (!color || color->blue != 0) throw std::runtime_error("Absent closed snapshot recovered stale RGB");
    });
    checks.emplace_back("Existing Basic scroller observes hidden soft locks but not precreation rebuilds", [] {
        MenuSnapshot state; state.isOpen = true; state.appearance.design = MenuDesign::Basic;
        state.menuChromeRevision = 1; BasicScrollerHistory history;
        const layout::Color observed{17,67,211,255};
        // Empty rows and modal row replacement do not destroy the source scroller
        for (unsigned modal = 0; modal < 5; ++modal) {
            auto hidden = state;
            if (modal == 1) hidden.widthEditor = WidthEditor{};
            if (modal == 2) hidden.instructionPositionEditor = InstructionPositionEditor{};
            if (modal == 3) hidden.menuPositionEditor = MenuPositionEditor{};
            if (modal == 4) hidden.menuCredits = MenuCredits{};
            if (!HasExistingBasicScroller(hidden)) throw std::runtime_error("Soft lock lost existing chrome");
            history.Update(hidden.menuChromeRevision, HasExistingBasicScroller(hidden), observed);
        }
        const auto fixed = history.Update(state.menuChromeRevision, HasExistingBasicScroller(state), std::nullopt);
        if (!fixed || fixed->blue != observed.blue) throw std::runtime_error("Hidden source Rainbow sample was discarded");
        for (unsigned gap = 0; gap < 5; ++gap) {
            auto absent = state;
            if (gap == 0) absent.isOpen = false;
            if (gap == 1) absent.menuUnlock = MenuUnlockPhase::Rebuild;
            if (gap == 2) absent.menuDesignTransition = MenuDesignTransitionPhase::Rebuild;
            if (gap == 3) absent.menuOpenTransition = MenuOpenTransitionPhase::Rebuild;
            if (gap == 4) absent.menuPreferenceRebuild = MenuPreferenceRebuildPhase::Rebuild;
            if (HasExistingBasicScroller(absent)) throw std::runtime_error("Absent HUD fabricated a source scroller");
            history.Update(absent.menuChromeRevision, false, layout::Color{220,15,30,255});
        }
        const auto retained = history.Update(state.menuChromeRevision, true, std::nullopt);
        if (!retained || retained->blue != observed.blue) throw std::runtime_error("Closed or rebuild frame erased retained RGB");
    });
    checks.emplace_back("Basic fixed panel preserves supplied historical scroller RGB", [] {
        MenuSnapshot state; state.isOpen = true; state.appearance.design = MenuDesign::Basic;
        state.appearance.instructionsVisible = false; state.rows = {{"Row", "", true}};
        const layout::Color stored{17,67,211,255}; Panel panel;
        // The oracle samples clear scroller ink beyond the short row label
        auto pixels = panel.Draw(state,stored);
        const auto hud = layout::MakeHud({640,480,1,0,260,3,false,5,state.appearance});
        const int x = static_cast<int>(hud.scroller.Right()-4), y = static_cast<int>(hud.scroller.y+hud.scroller.height/2);
        const auto index = static_cast<std::size_t>((x+y*640)*3);
        if (pixels[index]!=stored.red || pixels[index+1]!=stored.green || pixels[index+2]!=stored.blue)
            throw std::runtime_error("Production Basic fixed frame reset the historical RGB to black");
        // A retained color cannot recolor an unrelated style or change current accent
        state.appearance.design = MenuDesign::Apparition;
        if (panel.Draw(state,stored)!=panel.Draw(state,std::nullopt)) throw std::runtime_error("History changed another design");
        if (state.accent!="#3998FE") throw std::runtime_error("History rewrote current accent");
    });
}
}
