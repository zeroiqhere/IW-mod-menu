#include "BooleanPaintPanelTests.h"
#include "../../../native/render/ApparitionPanel.h"
#include "../../../native/render/ScrollRows.h"
#include "../CpuDrawlist.h"
#include <stdexcept>

namespace {
using namespace iw::overlay;
void Require(bool value, const char* reason) { if (!value) throw std::runtime_error(reason); }
class CpuPanel final {
public:
    CpuPanel() {
        context_ = ImGui::CreateContext(); ImGui::SetCurrentContext(context_);
        auto& io = ImGui::GetIO(); io.IniFilename = nullptr; io.LogFilename = nullptr;
        io.BackendFlags |= ImGuiBackendFlags_RendererHasTextures | ImGuiBackendFlags_RendererHasVtxOffset;
        ImGui::GetPlatformIO().Renderer_TextureMaxWidth = 4096;
        ImGui::GetPlatformIO().Renderer_TextureMaxHeight = 4096;
    }
    ~CpuPanel() { ImGui::DestroyContext(context_); }
    std::vector<unsigned char> Draw(const MenuSnapshot& state) {
        ImGui::SetCurrentContext(context_); auto& io = ImGui::GetIO();
        io.DisplaySize = {640, 480}; io.DeltaTime = 1.f / 60;
        ImGui::NewFrame(); DrawApparitionPanel(state); ImGui::Render();
        auto& data = *ImGui::GetDrawData(); textures_.Update(data);
        return textures_.Raster(data, 640, 480);
    }
private:
    ImGuiContext* context_;
    tests::CpuTextures textures_;
};
MenuSnapshot Source() {
    MenuSnapshot state; state.isOpen = true; state.menuRevision = 6; state.presentationFocusRevision = 8;
    state.rows = {{"God Mode", "OFF", true}, {"Unknown current result", "UNKNOWN", false}};
    MenuBooleanPaint paint; paint.revision = 7; paint.count = 2;
    paint.rows[0] = {0, MenuBooleanPaintValue::On}; paint.rows[1] = {1, MenuBooleanPaintValue::Off};
    state.menuBooleanPaint = paint; return state;
}
}
void AddBooleanPaintPanelChecks(std::vector<std::pair<std::string, std::function<void()>>>& checks) {
    checks.emplace_back("Cached boolean paint uses one value for boxes text color glyphs and row ink while UNKNOWN wins", [] {
        CpuPanel panel;
        for (auto design : {MenuDesign::Apparition, MenuDesign::Classic, MenuDesign::Native, MenuDesign::Aio, MenuDesign::Basic}) {
            for (const char* display : {"Boxes", "Text", "Text Color"}) for (bool large : {false, true}) {
                auto state = Source(); state.appearance.design = design; state.appearance.booleanDisplay = display;
                state.appearance.largeCursor = large;
                if (design == MenuDesign::Classic || design == MenuDesign::Native) state.appearance.coloredCursor = false;
                auto oracle = state; oracle.menuBooleanPaint.reset(); oracle.rows[0].value = "ON";
                // Warm both glyph sets before comparing the actual production drawlists
                panel.Draw(state); panel.Draw(oracle);
                Require(panel.Draw(state) == panel.Draw(oracle), "Cached boolean pixels differ from the exact display oracle");
                Require(state.rows[0].value == "OFF" && state.rows[0].enabled && state.rows[1].value == "UNKNOWN",
                    "Rendering changed a current value or action capability");
            }
        }
    });
    checks.emplace_back("Boolean paint identity rejects stale frames without locking ordinary menu input", [] {
        auto state = Source(); const auto request = MakeScrollRequest(state, 640, 480);
        const auto hud = layout::MakeHud({640, 480, 2, 0, 260, 3, false, 0, state.appearance});
        const auto frame = StaticScrollFrame(request);
        Require(request.active && request.menuBooleanPaint == state.menuBooleanPaint && MatchesScrollFrame(frame, hud, request),
            "Cached paint became input authority or lost frame identity");
        for (int change = 0; change < 3; ++change) {
            auto stale = frame;
            if (change == 0) ++stale.menuBooleanPaint->revision;
            if (change == 1) stale.menuBooleanPaint->rows[0].value = MenuBooleanPaintValue::Off;
            if (change == 2) stale.menuBooleanPaint.reset();
            Require(!MatchesScrollFrame(stale, hud, request), "Foreign cached paint borrowed a current frame");
        }
    });
}
