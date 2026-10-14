#include "InstructionOptionPanelTests.h"
#include "../BackendPreview.h"
#include "../CpuDrawlist.h"
#include "../../../native/render/ApparitionPanel.h"
#include <array>
#include <stdexcept>

namespace iw::overlay::tests {
namespace {
void Require(bool value, const char* message) { if (!value) throw std::runtime_error(message); }
class InstructionPanelPixels final {
public:
    InstructionPanelPixels() {
        context_ = ImGui::CreateContext(); ImGui::SetCurrentContext(context_);
        auto& io = ImGui::GetIO(); io.IniFilename = nullptr; io.LogFilename = nullptr;
        io.BackendFlags |= ImGuiBackendFlags_RendererHasTextures | ImGuiBackendFlags_RendererHasVtxOffset;
        ImGui::GetPlatformIO().Renderer_TextureMaxWidth = 4096;
        ImGui::GetPlatformIO().Renderer_TextureMaxHeight = 4096;
    }
    ~InstructionPanelPixels() { ImGui::DestroyContext(context_); }
    std::vector<unsigned char> Draw(const MenuSnapshot& state) {
        ImGui::SetCurrentContext(context_);
        auto& io = ImGui::GetIO(); io.DisplaySize = {640, 480}; io.DeltaTime = 1.f / 60;
        ImGui::NewFrame(); DrawApparitionPanel(state); ImGui::Render();
        auto& data = *ImGui::GetDrawData(); textures_.Update(data);
        return textures_.Raster(data, 640, 480);
    }
private:
    ImGuiContext* context_;
    CpuTextures textures_;
};
}
void AddActualInstructionOptionChecks(std::vector<std::pair<std::string, std::function<void()>>>& checks,
    const std::filesystem::path& directory) {
    checks.emplace_back("Actual instruction checkbox paint retains current footer flags and independently sampled values", [directory] {
        InstructionPanelPixels panel;
        // These checked-row masks are independent of the production value resolver
        const std::array<std::pair<const char*, unsigned>, 16> profiles{{
            {"instructions-before.json", 0}, {"disable0.json", 1}, {"disable179.json", 1},
            {"disable180.json", 1}, {"disable430.json", 1}, {"alternate0.json", 0}, {"alternate180.json", 2},
            {"background0.json", 0}, {"background180.json", 4}, {"adaptive0.json", 0}, {"adaptive180.json", 8},
            {"alternate-clears0.json", 8}, {"alternate-clears180.json", 2}, {"adaptive-rejected0.json", 2},
            {"adaptive-rejected180.json", 2}, {"adaptive-rejected430.json", 2}}};
        for (const auto& [file, cachedMask] : profiles) {
            const auto state = ReadBackendPreview(directory / file);
            Require(state.rows.size() == 6 && state.menuBooleanPaint && state.menuBooleanPaint->count == 4,
                "Actual instruction graph is missing its four copied checkbox rows");
            auto oracle = state;
            // The independent oracle changes only displayed values and retains all current flags
            bool differs = false;
            for (std::size_t row = 0; row < 4; ++row) {
                oracle.rows[row].value = (cachedMask & (1u << row)) != 0 ? "ON" : "OFF";
                differs |= oracle.rows[row].value != state.rows[row].value;
            }
            oracle.menuBooleanPaint.reset(); panel.Draw(state); panel.Draw(oracle);
            Require(panel.Draw(state) == panel.Draw(oracle), "Instruction pixels disagree with their independent copied-value oracle");
            Require(oracle.appearance == state.appearance && oracle.accent == state.accent,
                "Instruction oracle replaced the current footer appearance");
            if (differs) {
                auto uncached = state; uncached.menuBooleanPaint.reset(); panel.Draw(uncached);
                Require(panel.Draw(state) != panel.Draw(uncached), "Instruction pixel oracle cannot detect discarded paint");
            }
        }
    });
}
}
