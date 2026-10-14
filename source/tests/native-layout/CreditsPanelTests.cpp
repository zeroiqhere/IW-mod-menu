#include "CreditsPanelTests.h"
#include "CpuDrawlist.h"
#include "BackendPreview.h"
#include "../../native/render/ApparitionPanel.h"
#include "../../native/render/InstructionPanel.h"
#include "../../native/render/ScrollRows.h"
#include "../../native/render/credits/CreditsPresentationClock.h"
#include "../../native/runtime/ControlLabels.h"
#include <imgui.h>
#include <cmath>
#include <iostream>
#include <limits>
#include <stdexcept>

namespace iw::overlay::tests {
namespace {
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
    ImDrawData& Draw(const MenuSnapshot& model, float width = 640, float height = 480,
        std::optional<double> elapsed = std::nullopt) {
        // The production panel and official texture-update contract run without a graphics backend
        ImGui::SetCurrentContext(context_); auto& io = ImGui::GetIO(); io.DisplaySize = {width, height}; io.DeltaTime = 1.0f / 60;
        ImGui::NewFrame(); DrawApparitionPanel(model, nullptr, std::nullopt, std::nullopt, nullptr, elapsed); ImGui::Render();
        auto& data = *ImGui::GetDrawData(); textures.Update(data); return data;
    }
    CpuTextures textures;
private:
    ImGuiContext* context_;
};
MenuSnapshot Model(int elapsed = 500) {
    MenuSnapshot model; model.isOpen = true; model.title = "Menu Customization";
    model.context = "CPU TEST / DISCONNECTED"; model.notice = "Original BO3 contributor credits; no game is connected";
    model.menuRevision = 3; model.presentationFocusRevision = 0; model.menuCredits = MenuCredits{elapsed}; return model;
}
bool HasColorIn(const ImDrawData& data, ImU32 color, layout::Rect area) {
    for (auto* list : data.CmdLists) for (const auto& vertex : list->VtxBuffer)
        if (vertex.col == color && vertex.pos.x >= area.x && vertex.pos.x < area.Right()
            && vertex.pos.y >= area.y && vertex.pos.y < area.Bottom()) return true;
    return false;
}
void FiniteClips(const ImDrawData& data, float width, float height) {
    Require(data.TotalVtxCount < 100000 && data.TotalIdxCount < 150000, "Credits exceeded bounded drawing work");
    for (auto* list : data.CmdLists) {
        for (const auto& vertex : list->VtxBuffer)
            Require(std::isfinite(vertex.pos.x) && std::isfinite(vertex.pos.y) && std::isfinite(vertex.uv.x)
                && std::isfinite(vertex.uv.y), "Credits emitted nonfinite vertices");
        for (const auto& command : list->CmdBuffer)
            Require(command.ClipRect.x >= 0 && command.ClipRect.y >= 0 && command.ClipRect.z <= width
                && command.ClipRect.w <= height && command.GetTexID() != ImTextureID_Invalid,
                "Credits escaped a viewport or ignored a texture request");
    }
}
}
void AddCreditsPanelChecks(std::vector<std::pair<std::string, std::function<void()>>>& checks) {
    checks.emplace_back("Credits paint source red headings and preserve the customization title", [] {
        CpuPanel panel; const auto& data = panel.Draw(Model());
        Require(HasColorIn(data, IM_COL32(255, 0, 0, 255), {0, 270, 260, 35}), "The live credits heading did not draw");
        Require(HasColorIn(data, IM_COL32(57, 152, 254, 255), {0, 65, 260, 19}), "The retained title was replaced");
        FiniteClips(data, 640, 480);
    });
    checks.emplace_back("Credits inline white and red runs inherit each line fade without theme recoloring", [] {
        CpuPanel panel; auto model = Model(1000); model.accent = "#98FF98";
        const auto& data = panel.Draw(model);
        Require(HasColorIn(data, IM_COL32(255, 255, 255, 85), {0, 280, 260, 25})
            && HasColorIn(data, IM_COL32(255, 0, 0, 85), {0, 280, 260, 25}),
            "A mixed credit line lost its white and red fading runs");
    });
    checks.emplace_back("Credits fade-in midpoint and fade-out midpoint produce half-opacity glyphs", [] {
        for (int elapsed : {250, 9750}) {
            CpuPanel panel; const auto& data = panel.Draw(Model(elapsed));
            Require(HasColorIn(data, IM_COL32(255, 0, 0, 127), {0, 85, 260, 220}),
                "The source500ms fade did not reach half opacity");
        }
        CpuPanel panel; Require(!HasColorIn(panel.Draw(Model(0)), IM_COL32(255, 0, 0, 255), {0, 85, 260, 220}),
            "Invisible entry credits painted fully opaque text");
    });
    checks.emplace_back("Production credits consume bounded fractional local-clock presentation", [] {
        CpuPanel panel; auto model = Model(250); const auto& sampled = panel.Draw(model, 640, 480, 275.5);
        Require(HasColorIn(sampled, IM_COL32(255, 0, 0, 140), {0, 85, 260, 220}),
            "Fractional credits presentation was ignored or rounded to a heartbeat");
        for (double invalid : {-1.0, 351.0, 40000.0, std::numeric_limits<double>::quiet_NaN()}) {
            const auto& fallback = panel.Draw(model, 640, 480, invalid);
            Require(HasColorIn(fallback, IM_COL32(255, 0, 0, 127), {0, 85, 260, 220}),
                "Invalid local interpolation replaced the authoritative phase");
        }
    });
    checks.emplace_back("Credits footer advertises only saved melee and retains ordinary help for resume", [] {
        auto model = Model(); const auto normal = model.instructions;
        model.menuCredits->instructions = {"V / Mouse 5: Exit Menu Credits", "Inactive extra action"};
        const auto content = MakeInstructionContent(model);
        Require(content.LineCount() == 1 && content.Line(0) == "V / Mouse 5: Exit Menu Credits"
            && model.instructions == normal, "Credits exposed inactive controls or overwrote normal help");
        model.menuCredits.reset(); Require(MakeInstructionContent(model).LineCount() == normal.size(),
            "Leaving credits did not restore ordinary footer content");
    });
    checks.emplace_back("Hidden empty alternate and background-free credits hints preserve the same credits body", [] {
        for (unsigned variant = 0; variant < 4; ++variant) {
            CpuPanel panel; auto model = Model();
            if (variant == 0) model.appearance.instructionsVisible = false;
            if (variant == 1) model.menuCredits->instructions.clear();
            if (variant == 2) model.appearance.alternateInstructions = true;
            if (variant == 3) model.appearance.instructionsBackground = false;
            const auto& data = panel.Draw(model);
            Require(HasColorIn(data, IM_COL32(255, 0, 0, 255), {0, 270, 260, 35}),
                "Changing credits help changed or removed the timed body"); FiniteClips(data, 640, 480);
        }
    });
    checks.emplace_back("Moved and extreme credits remain clipped while adaptive help follows the source220 body", [] {
        for (const auto position : {MenuPosition{37, 126}, MenuPosition{-130, 0}, MenuPosition{600, 450},
            MenuPosition{std::numeric_limits<std::int32_t>::max(), std::numeric_limits<std::int32_t>::min()}})
            for (bool adaptive : {false, true}) {
                CpuPanel panel; auto model = Model(6000); model.appearance.menuPosition = position;
                model.appearance.adaptiveInstructions = adaptive;
                FiniteClips(panel.Draw(model), 640, 480);
                Require(model.appearance.menuPosition == position, "Credits silently moved a saved anchor");
            }
    });
    checks.emplace_back("All credits groups fit tested widths and aspect ratios without unbounded drawing", [] {
        for (const auto dimensions : {ImVec2{320, 240}, ImVec2{960, 720}, ImVec2{1920, 1080}, ImVec2{480, 960}})
            for (int width : {200, 260, 500}) {
                CpuPanel panel; auto model = Model(); model.appearance.menuWidth = width;
                for (int elapsed : {500, 6000, 13000, 18000, 22500, 28000, 39999}) {
                    model.menuCredits->elapsedMilliseconds = elapsed;
                    FiniteClips(panel.Draw(model, dimensions.x, dimensions.y), dimensions.x, dimensions.y);
                }
            }
    });
    checks.emplace_back("Credits return and close discard animated rows and resume ordinary presentation safely", [] {
        CpuPanel panel; auto model = Model(); ScrollPresentation animation;
        Require(!animation.Update(MakeScrollRequest(model, 640, 480), 0).transitioning,
            "Credits inherited ordinary cursor motion");
        model.menuCredits.reset(); model.rows = {{"Menu Credits", ">", true}};
        model.menuUnlock = MenuUnlockPhase::Rebuild;
        Require(!HasColorIn(panel.Draw(model), IM_COL32(255, 0, 0, 255), {0, 85, 260, 220}),
            "The rebuild phase retained old credit lines");
        model.menuUnlock = MenuUnlockPhase::Pause; FiniteClips(panel.Draw(model), 640, 480);
        model.isOpen = false; Require(panel.Draw(model).TotalVtxCount == 0, "Closed credits retained panel triangles");
    });
}
void VerifyCreditsBackendPreviews(const std::filesystem::path& fixtures, const std::filesystem::path& output) {
    const auto read = [&](const char* name) { return ReadBackendPreview(fixtures / name); };
    auto entry = read("menu-credits-enter.json");
    const auto faded = read("menu-credits-fade-in.json"), overlap = read("menu-credits-overlap.json");
    const auto late = read("menu-credits-late.json");
    const auto rebuild = read("menu-credits-unlock-rebuild.json"), pause = read("menu-credits-unlock-pause.json");
    const auto parent = read("customization-after-credits.json");
    Require(entry.menuCredits && faded.menuCredits && overlap.menuCredits && late.menuCredits,
        "Actual active credits metadata is missing");
    Require(entry.title == "Menu Customization" && entry.rows.empty() && entry.selectedIndex == 0
        && entry.menuCredits->elapsedMilliseconds == 0 && faded.menuCredits->elapsedMilliseconds == 500
        && overlap.menuCredits->elapsedMilliseconds == 1000 && late.menuCredits->elapsedMilliseconds == 39999,
        "Actual credits elapsed or source title fields were lost");
    Require(entry.menuRevision && faded.menuRevision == entry.menuRevision && overlap.menuRevision == entry.menuRevision
        && late.menuRevision == entry.menuRevision && entry.navigationRevision == late.navigationRevision,
        "A credits heartbeat changed its confirmed context");
    Require(!rebuild.menuCredits && rebuild.menuUnlock == MenuUnlockPhase::Rebuild && !pause.menuCredits
        && pause.menuUnlock == MenuUnlockPhase::Pause && !parent.menuCredits && !parent.menuUnlock
        && parent.selectedIndex == 0 && parent.rows.size() == 7 && parent.rows[0].label == "Menu Credits",
        "Actual credits exit retained old metadata or selected the wrong parent row");
    Require(entry.notice.find("CF4_99") != std::string::npos && entry.notice.find("SyndiShanX") != std::string::npos,
        "Actual attribution conflated the BO3 source and IW implementation");
    // Private saved bindings are explicitly declared for these separate CPU-only demos
    entry.menuCredits->instructions = runtime::DescribeMenuCreditsControls({{2}, {86, 6}, {2, 38}, {1, 40}, {70}});
    entry.context = "PREVIEW / SAMPLE CREDITS BINDINGS";
    entry.notice = "Original BO3 contributor roll with native font, red and linear-easing mappings. Declared saved melee keys; no game connected.";
    CpuPanel panel;
    for (const auto elapsed : {250, 1000, 6000, 13000, 18000, 28000, 9750}) {
        entry.menuCredits->elapsedMilliseconds = elapsed;
        const auto& data = panel.Draw(entry, 960, 720); FiniteClips(data, 960, 720);
        WritePreview(output / ("credits-clock-" + std::to_string(elapsed) + ".ppm"), panel.textures.Raster(data, 960, 720), 960, 720);
    }
    entry.menuCredits->elapsedMilliseconds = 250; entry.presentationFocusRevision = 0;
    credits::PresentationClock clock; clock.Update(250, entry.menuRevision, 0, 100);
    const auto sample = clock.Update(250, entry.menuRevision, 0, 150);
    entry.context = "PREVIEW / BOUNDED CREDITS CLOCK DEMO";
    entry.notice = "Reported phase: 250 ms with 50 ms local lead. Interpolation stops at one 100 ms heartbeat; backend owns exit. No game connected.";
    const auto& smooth = panel.Draw(entry, 960, 720, sample); FiniteClips(smooth, 960, 720);
    WritePreview(output / "credits-local-clock-midpoint.ppm", panel.textures.Raster(smooth, 960, 720), 960, 720);
    std::cout << "PASS Actual backend credits elapsed, attribution, context and returning phases\n";
}
}
