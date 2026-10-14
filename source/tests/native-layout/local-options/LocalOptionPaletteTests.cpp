#include "LocalOptionPanelTests.h"
#include "../BackendPreview.h"
#include "../CpuDrawlist.h"
#include "../../../native/render/ApparitionPanel.h"
#include <array>
#include <filesystem>
#include <stdexcept>

namespace iw::overlay::tests {
namespace {
void Require(bool value, const char* message) { if (!value) throw std::runtime_error(message); }
class LocalPalettePanel final {
public:
    LocalPalettePanel() {
        context_ = ImGui::CreateContext(); ImGui::SetCurrentContext(context_);
        auto& io = ImGui::GetIO(); io.IniFilename = nullptr; io.LogFilename = nullptr;
        io.BackendFlags |= ImGuiBackendFlags_RendererHasTextures | ImGuiBackendFlags_RendererHasVtxOffset;
        ImGui::GetPlatformIO().Renderer_TextureMaxWidth = 4096;
        ImGui::GetPlatformIO().Renderer_TextureMaxHeight = 4096;
    }
    ~LocalPalettePanel() { ImGui::DestroyContext(context_); }
    std::vector<unsigned char> Draw(const MenuSnapshot& state) {
        ImGui::SetCurrentContext(context_);
        auto& io = ImGui::GetIO(); io.DisplaySize = {640, 480}; io.DeltaTime = 1.f / 60;
        ImGui::NewFrame();
        // A fixed current chrome sample removes wall-clock rainbow variation from the oracle
        DrawApparitionPanel(state, nullptr, std::nullopt, state.smoothRainbowTheme
            ? std::optional<layout::Color>{layout::Color{51, 153, 230, 255}} : std::nullopt);
        ImGui::Render(); auto& data = *ImGui::GetDrawData(); textures_.Update(data);
        return textures_.Raster(data, 640, 480);
    }
private:
    ImGuiContext* context_;
    CpuTextures textures_;
};
}
void AddActualLocalPaletteChecks(std::vector<std::pair<std::string, std::function<void()>>>& checks,
    const std::filesystem::path& directory) {
    checks.emplace_back("Actual local palette draws copied checkbox values with current accent and rainbow state", [directory] {
        LocalPalettePanel panel;
        // These samples follow the controller graph without using the production value resolver
        const std::array<std::pair<const char*, std::size_t>, 7> profiles{{
            {"palette-before.json", 6}, {"palette0.json", 6}, {"palette179.json", 6},
            {"palette180.json", 7}, {"palette430.json", 7}, {"rainbow0.json", 6}, {"rainbow180.json", 36}}};
        for (const auto& [file, cachedOn] : profiles) {
            const auto state = ReadBackendPreview(directory / file);
            Require(state.rows.size() == 37 && state.menuBooleanPaint && state.menuBooleanPaint->count == 37,
                "Actual palette graph is missing its complete copied checkbox sample");
            auto oracle = state;
            // Only the independent display oracle changes row text; actual packets stay untouched
            for (std::size_t row = 0; row < state.rows.size(); ++row)
                oracle.rows[row].value = row == cachedOn ? "ON" : "OFF";
            oracle.menuBooleanPaint.reset();
            panel.Draw(state); panel.Draw(oracle);
            Require(panel.Draw(state) == panel.Draw(oracle), "Actual palette pixels disagree with the copied-value oracle");
            Require(oracle.accent == state.accent && oracle.smoothRainbowTheme == state.smoothRainbowTheme,
                "Display oracle changed the current palette chrome");
            if (std::string_view(file) == "palette0.json" || std::string_view(file) == "palette179.json") {
                Require(state.accent == "#0064C8" && state.rows[6].value == "OFF" && state.rows[7].value == "ON"
                    && oracle.rows[6].value == "ON" && oracle.rows[7].value == "OFF",
                    "The real two-row palette mismatch was not exercised");
                auto uncached = state; uncached.menuBooleanPaint.reset(); panel.Draw(uncached);
                Require(panel.Draw(state) != panel.Draw(uncached), "Palette oracle cannot detect lost cached checkbox pixels");
            }
        }
    });
}
}
