#include "PreferenceRefreshPanelTests.h"
#include "CpuDrawlist.h"
#include "BackendPreview.h"
#include "../../native/render/ApparitionPanel.h"
#include "../../native/render/InstructionPanel.h"
#include "../../native/render/ScrollRows.h"
#include <imgui_internal.h>
#include <array>
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
        const ScrollFrame* rows = nullptr) {
        // The production panel and official texture-update contract run without a graphics backend
        ImGui::SetCurrentContext(context_); auto& io = ImGui::GetIO(); io.DisplaySize = {width, height}; io.DeltaTime = 1.0f / 60;
        ImGui::NewFrame(); DrawApparitionPanel(model, nullptr, std::nullopt, std::nullopt, rows); ImGui::Render();
        auto& data = *ImGui::GetDrawData(); textures.Update(data); return data;
    }
    std::array<int, 4> Atlas() {
        // Internal atlas counters are inspected only in this regression, never in the renderer
        ImGui::SetCurrentContext(context_); const auto& atlas = *ImGui::GetIO().Fonts;
        return {atlas.Builder->BakedPool.Size, atlas.TexData->Width, atlas.TexData->Height, atlas.TexList.Size};
    }
    CpuTextures textures;
private:
    ImGuiContext* context_;
};
MenuSnapshot Model() {
    MenuSnapshot model; model.isOpen = true; model.isRootMenu = false; model.title = "Menu Preferences";
    model.context = "CPU TEST / DISCONNECTED"; model.notice = "Updating menu; controls paused";
    model.menuRevision = 3; model.presentationFocusRevision = 0; model.selectedIndex = 3;
    model.rows.resize(9, {"Preference label", "OFF", true}); return model;
}
bool HasColorIn(const ImDrawData& data, ImU32 color, layout::Rect area) {
    for (auto* list : data.CmdLists) for (const auto& vertex : list->VtxBuffer)
        if (vertex.col == color && vertex.pos.x >= area.x && vertex.pos.x < area.Right()
            && vertex.pos.y >= area.y && vertex.pos.y < area.Bottom()) return true;
    return false;
}
void FiniteClips(const ImDrawData& data, float width, float height) {
    Require(data.TotalVtxCount < 100000 && data.TotalIdxCount < 150000, "Refresh exceeded bounded drawing work");
    for (auto* list : data.CmdLists) {
        for (const auto& vertex : list->VtxBuffer)
            Require(std::isfinite(vertex.pos.x) && std::isfinite(vertex.pos.y), "Refresh emitted nonfinite vertices");
        for (const auto& command : list->CmdBuffer)
            Require(command.ClipRect.x >= 0 && command.ClipRect.y >= 0 && command.ClipRect.z <= width
                && command.ClipRect.w <= height, "Refresh escaped its viewport clip");
    }
}
}
void AddPreferenceRefreshPanelChecks(std::vector<std::pair<std::string, std::function<void()>>>& checks) {
    checks.emplace_back("Both preference refresh phases replace inactive navigation help and preserve saved controls", [] {
        auto model = Model(); const auto saved = model.instructions;
        for (const auto phase : {MenuPreferenceRefreshPhase::Pending, MenuPreferenceRefreshPhase::Cooldown}) {
            model.menuPreferenceRefresh = phase;
            const auto help = MakeInstructionContent(model);
            Require(help.LineCount() == 3 && help.Line(0) == "Updating menu" && help.Line(1) == "Menu controls paused"
                && help.Line(2) == "Please wait", "Preference refresh still advertises active Scroll or Select");
            Require(model.instructions == saved, "Paused help overwrote the normal saved controls");
        }
        model.menuPreferenceRefresh.reset(); const auto help = MakeInstructionContent(model);
        Require(help.Line(0) == saved[0] && help.Line(1) == saved[1] && help.Line(2) == saved[2],
            "Normal saved help did not resume after refresh");
    });
    checks.emplace_back("Preference refresh stops cursor motion in both phases and restarts from the declared selection", [] {
        auto model = Model(); model.selectedIndex = 2; ScrollPresentation animation;
        animation.Update(MakeScrollRequest(model, 640, 480), 0);
        model.selectedIndex = 3;
        Require(animation.Update(MakeScrollRequest(model, 640, 480), 10).transitioning, "The ordinary setup did not begin motion");
        for (const auto phase : {MenuPreferenceRefreshPhase::Pending, MenuPreferenceRefreshPhase::Cooldown}) {
            model.menuPreferenceRefresh = phase;
            const auto request = MakeScrollRequest(model, 640, 480);
            Require(!request.active, "A preference refresh retained active scrolling");
            const auto frame = animation.Update(request, 30);
            Require(!frame.transitioning && frame.scroller == 3 && frame.rows[3].selection == 1,
                "A refresh retained an old cursor path instead of declared selection");
        }
        model.menuPreferenceRefresh.reset(); ++*model.menuRevision;
        Require(!animation.Update(MakeScrollRequest(model, 640, 480), 430).transitioning,
            "A completed refresh replayed motion from the previous menu epoch");
    });
    checks.emplace_back("Refresh retains the ordinary title rows selection and counter pixels", [] {
        CpuPanel panel; auto model = Model(); model.appearance.optionCounter = true;
        auto baseline = panel.textures.Raster(panel.Draw(model), 640, 480);
        for (const auto phase : {MenuPreferenceRefreshPhase::Pending, MenuPreferenceRefreshPhase::Cooldown}) {
            model.menuPreferenceRefresh = phase;
            const auto& data = panel.Draw(model); const auto actual = panel.textures.Raster(data, 640, 480);
            // Only footer help changes here; the same declared row and appearance data remain visible
            for (int y = 50; y < 300; ++y) for (int x = 0; x < 260; ++x) for (int channel = 0; channel < 3; ++channel)
                Require(actual[(x + y * 640) * 3 + channel] == baseline[(x + y * 640) * 3 + channel],
                    "Refresh hid or changed the declared title, rows, selection or counter");
            FiniteClips(data, 640, 480);
        }
    });
    checks.emplace_back("Pending and cooldown paint only their declared cursor styles and boolean values", [] {
        CpuPanel panel; auto model = Model(); model.menuPreferenceRefresh = MenuPreferenceRefreshPhase::Pending;
        // Quick Exit can already be committed while its displayed row remains on the prior value
        model.appearance.quickExit = true;
        const auto& pending = panel.Draw(model);
        Require(HasColorIn(pending, IM_COL32(57, 152, 254, 255), {4, 139, 180, 18})
            && HasColorIn(pending, IM_COL32(0, 0, 0, 255), {248, 139, 8, 18}),
            "Pending guessed new label styles or an actual Quick Exit row value");
        model.menuPreferenceRefresh = MenuPreferenceRefreshPhase::Cooldown;
        model.appearance.coloredCursor = false; model.appearance.spotlightCursor = false; model.appearance.largeCursor = true;
        model.rows[3].value = "ON"; const auto& cooldown = panel.Draw(model);
        Require(HasColorIn(cooldown, IM_COL32_WHITE, {4, 139, 180, 18})
            && !HasColorIn(cooldown, IM_COL32(57, 152, 254, 255), {4, 139, 180, 18})
            && HasColorIn(cooldown, IM_COL32(57, 152, 254, 255), {248, 139, 8, 18}),
            "Cooldown failed to paint the producer's new cursor styles and ON value");
    });
    checks.emplace_back("A caller supplied old cursor frame cannot override a preference refresh", [] {
        CpuPanel panel; auto model = Model(); model.menuPreferenceRefresh = MenuPreferenceRefreshPhase::Pending;
        auto request = MakeScrollRequest(model, 640, 480); auto old = StaticScrollFrame(request);
        old.scroller = 2.5f; old.rows[2].selection = .5f; old.rows[3].selection = .5f; old.transitioning = true;
        auto expected = panel.textures.Raster(panel.Draw(model), 640, 480);
        auto actual = panel.textures.Raster(panel.Draw(model, 640, 480, &old), 640, 480);
        Require(actual == expected, "An old cursor presentation frame changed a refresh snapshot");
    });
    checks.emplace_back("Refresh help respects hidden alternate background and adaptive preferences without losing the panel", [] {
        for (unsigned variant = 0; variant < 4; ++variant) {
            CpuPanel panel; auto model = Model(); model.menuPreferenceRefresh = MenuPreferenceRefreshPhase::Pending;
            if (variant == 0) model.appearance.instructionsVisible = false;
            if (variant == 1) { model.appearance.alternateInstructions = true; model.appearance.instructionsBackground = false; }
            if (variant == 2) model.appearance.adaptiveInstructions = true;
            if (variant == 3) { model.instructions.clear(); model.appearance.instructionsBackground = false; }
            const auto help = MakeInstructionContent(model);
            Require(help.LineCount() == (variant == 1 ? 1u : 3u), "Footer preferences lost bounded pause help");
            const auto& data = panel.Draw(model);
            Require(HasColorIn(data, IM_COL32(57, 152, 254, 255), {0, 65, 260, 19}), "Refresh removed the retained menu title");
            FiniteClips(data, 640, 480);
        }
    });
    checks.emplace_back("Repeated refresh cycles keep warmed font bakes and textures stable", [] {
        CpuPanel panel; auto model = Model(); std::array<int, 4> warm{};
        for (unsigned frame = 0; frame < 900; ++frame) {
            // A single context exercises resume, both pause phases and the new cursor font repeatedly
            const unsigned phase = frame % 3;
            if (phase == 0) model.menuPreferenceRefresh.reset();
            else model.menuPreferenceRefresh = phase == 1 ? MenuPreferenceRefreshPhase::Pending : MenuPreferenceRefreshPhase::Cooldown;
            model.appearance.largeCursor = (frame / 3) % 2;
            model.appearance.coloredCursor = (frame / 6) % 2;
            model.appearance.spotlightCursor = (frame / 12) % 2;
            panel.Draw(model); const auto current = panel.Atlas();
            if (frame == 120) warm = current;
            if (frame > 120) Require(current == warm, "Preference refresh cycles grew or replaced the warmed atlas");
        }
        Require(warm[0] > 0 && warm[1] > 0 && warm[2] > 0, "The atlas check did not create any font textures");
    });
    checks.emplace_back("Refresh long notices and extreme menu anchors keep finite bounded drawing across viewports", [] {
        for (const auto size : {ImVec2{320, 240}, ImVec2{960, 720}, ImVec2{480, 960}})
            for (const auto position : {MenuPosition{37, 126}, MenuPosition{-80, 450},
                MenuPosition{std::numeric_limits<std::int32_t>::max(), std::numeric_limits<std::int32_t>::min()}}) {
                CpuPanel panel; auto model = Model(); model.menuPreferenceRefresh = MenuPreferenceRefreshPhase::Cooldown;
                model.notice.assign(6000, 'N'); model.appearance.menuPosition = position;
                FiniteClips(panel.Draw(model, size.x, size.y), size.x, size.y);
            }
    });
}
void VerifyPreferenceRefreshBackendPreviews(const std::filesystem::path& fixtures, const std::filesystem::path&) {
    const std::array<const char*, 4> names{"spotlight", "colored", "large", "quick-exit"};
    const std::array<const char*, 5> times{"pending", "pre-refresh", "cooldown", "pre-unlock", "unlocked"};
    CpuPanel panel;
    for (std::size_t target = 0; target < names.size(); ++target) {
        std::array<MenuSnapshot, 5> states;
        for (std::size_t time = 0; time < times.size(); ++time)
            states[time] = ReadBackendPreview(fixtures / (std::string("preferences-refresh-") + names[target] + "-" + times[time] + ".json"));
        const auto& pending = states[0]; const auto& before = states[1]; const auto& cooldown = states[2];
        const auto& late = states[3]; const auto& resumed = states[4];
        Require(pending.menuPreferenceRefresh == MenuPreferenceRefreshPhase::Pending
            && before.menuPreferenceRefresh == MenuPreferenceRefreshPhase::Pending
            && cooldown.menuPreferenceRefresh == MenuPreferenceRefreshPhase::Cooldown
            && late.menuPreferenceRefresh == MenuPreferenceRefreshPhase::Cooldown && !resumed.menuPreferenceRefresh,
            "Actual preference refresh lost a 180 ms or 430 ms boundary");
        Require(pending.menuRevision && before.menuRevision == pending.menuRevision
            && cooldown.menuRevision == *pending.menuRevision + 1
            && late.menuRevision == cooldown.menuRevision && resumed.menuRevision == cooldown.menuRevision,
            "Actual refresh did not invalidate the displayed menu exactly once");
        Require(pending.navigationRevision == resumed.navigationRevision && !pending.menuUnlock && !cooldown.menuUnlock,
            "A refresh fabricated navigation or used the hidden unlock phase");
        Require(pending.appearance.spotlightCursor && pending.appearance.coloredCursor && !pending.appearance.largeCursor,
            "Actual pending presentation exposed committed cursor styles before 180 ms");
        Require(cooldown.appearance.spotlightCursor == (target != 0)
            && cooldown.appearance.coloredCursor == (target != 1) && cooldown.appearance.largeCursor == (target == 2)
            && pending.appearance.quickExit == (target == 3) && resumed.appearance.quickExit == (target == 3),
            "Actual committed Quick Exit or deferred cursor flags differ from the intended preference");
        Require(pending.appearance == before.appearance && cooldown.appearance == late.appearance
            && cooldown.appearance == resumed.appearance, "Actual presentation changed within one refresh phase");
        const auto selected = pending.selectedIndex;
        Require(selected < pending.rows.size() && before.selectedIndex == selected && cooldown.selectedIndex == selected
            && resumed.selectedIndex == selected && cooldown.rows.size() == pending.rows.size(),
            "Actual refresh hid options or changed its retained cursor");
        const auto priorValue = target < 2 ? "ON" : "OFF";
        const auto nextValue = target < 2 ? "OFF" : "ON";
        Require(pending.rows[selected].value == priorValue && before.rows[selected].value == priorValue
            && cooldown.rows[selected].value == nextValue && late.rows[selected].value == nextValue
            && resumed.rows[selected].value == nextValue, "Actual preference boolean display crossed the wrong refresh boundary");
        for (std::size_t time = 0; time < states.size(); ++time) {
            const auto& state = states[time]; const auto help = MakeInstructionContent(state);
            // No extra timer or style cache belongs to the native presentation path
            if (time < 4) Require(help.Line(0) == "Updating menu" && help.Line(1) == "Menu controls paused"
                && !MakeScrollRequest(state, 960, 720).active, "Actual locked refresh advertised live navigation");
            else Require(help.Line(0) == state.instructions[0] && MakeScrollRequest(state, 960, 720).active,
                "Actual unlocked preference did not resume saved controls");
            FiniteClips(panel.Draw(state, 960, 720), 960, 720);
        }
    }
    std::cout << "PASS Actual backend four preference refreshes at 0/179/180/429/430 ms\n";
}

}
