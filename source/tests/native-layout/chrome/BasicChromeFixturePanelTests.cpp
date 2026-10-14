#include "BasicChromeFixturePanelTests.h"
#include "../CpuDrawlist.h"
#include "../BackendPreview.h"
#include "../../../native/render/ApparitionPanel.h"
#include "../../../native/render/chrome/BasicScrollerHistory.h"
#include "../../../native/render/chrome/BasicScrollerPresence.h"
#include "../../../native/render/chrome/BasicScrollerClosedObservation.h"
#include "../../../native/render/SmoothRainbow.h"
#include "../../native-runtime/chrome/fixtures/BasicChromeFixtureFiles.h"

namespace iw::overlay::tests {
namespace {
class ChromePanel final {
public:
    ChromePanel() {
        context_ = ImGui::CreateContext();
        ImGui::SetCurrentContext(context_);
        auto& io = ImGui::GetIO();
        io.IniFilename = nullptr; io.LogFilename = nullptr;
        io.BackendFlags |= ImGuiBackendFlags_RendererHasTextures | ImGuiBackendFlags_RendererHasVtxOffset;
        ImGui::GetPlatformIO().Renderer_TextureMaxWidth = 4096;
        ImGui::GetPlatformIO().Renderer_TextureMaxHeight = 4096;
    }
    ~ChromePanel() { ImGui::DestroyContext(context_); }
    std::vector<unsigned char> Draw(const MenuSnapshot& state, std::optional<layout::Color> history,
        std::optional<layout::Color> rainbow) {
        ImGui::SetCurrentContext(context_);
        auto& io = ImGui::GetIO(); io.DisplaySize = {640,480}; io.DeltaTime = 1.f/60;
        ImGui::NewFrame();
        DrawApparitionPanel(state, nullptr, std::nullopt, rainbow, nullptr, std::nullopt, history);
        ImGui::Render();
        auto& draw = *ImGui::GetDrawData(); textures_.Update(draw);
        return textures_.Raster(draw,640,480);
    }
private:
    ImGuiContext* context_;
    CpuTextures textures_;
};
}
void AddActualBasicChromeChecks(std::vector<std::pair<std::string, std::function<void()>>>& checks,
    const std::filesystem::path& directory) {
    checks.emplace_back("Actual Basic chrome retains sampled RGB until a new source widget is created", [directory] {
        chrome::CheckChromeInventory(directory);
        BasicScrollerHistory history;
        ChromePanel panel;
        bool checkedHistoricalFixedPixels = false;
        for (const auto& expected : chrome::ChromeProfiles) {
            const auto state = ReadBackendPreview(directory / expected.file);
            chrome::RequireChrome(state.menuChromeRevision == expected.chrome
                && state.navigationRevision == expected.navigation && state.menuRevision == expected.menu,
                "Actual CPU chrome lost its independent owner epochs");
            if (!state.isOpen) ObserveClosedBasicScroller(history, &state);
            const auto rainbow = state.smoothRainbowTheme
                ? std::optional<layout::Color>{SampleSmoothRainbow(expected.clock)} : std::nullopt;
            const auto color = history.Update(state.menuChromeRevision, HasExistingBasicScroller(state), rainbow);
            chrome::RequireChrome(color.has_value() == expected.exists
                && (!color || chrome::SameChromeColor(*color, expected.color)),
                "Actual CPU history changed a source creation or sampled RGB boundary");
            // The image oracle keeps current accent, palette rows and copied checkbox values untouched
            const auto pixels = panel.Draw(state, color, rainbow);
            const auto oracle = panel.Draw(state, expected.exists
                ? std::optional<layout::Color>{expected.color} : std::nullopt, rainbow);
            chrome::RequireChrome(pixels == oracle, "Production Basic pixels disagree with the independent RGB history oracle");
            if (expected.exists && expected.color.red != 0 && !state.smoothRainbowTheme) {
                chrome::RequireChrome(pixels != panel.Draw(state, chrome::Black, rainbow),
                    "The actual fixed frame failed to exercise visible historical scroller ink");
                checkedHistoricalFixedPixels = true;
            }
        }
        chrome::RequireChrome(checkedHistoricalFixedPixels, "The actual graph did not exercise fixed historical RGB");
    });
}
}
