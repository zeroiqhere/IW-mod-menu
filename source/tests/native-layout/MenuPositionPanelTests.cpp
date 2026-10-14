#include "MenuPositionPanelTests.h"
#include "CpuDrawlist.h"
#include "BackendPreview.h"
#include "../../native/runtime/ControlLabels.h"
#include "../../native/render/InstructionPanel.h"
#include <iostream>
#include "../../native/render/ApparitionPanel.h"
#include "../../native/render/ScrollRows.h"
#include <imgui.h>
#include <cmath>
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
    ImDrawData& Draw(const MenuSnapshot& model, const ScrollFrame* rows, float width = 640, float height = 480,
        bool referenceLastHint = false) {
        // Texture updates follow the official dynamic font-atlas contract without a GPU
        ImGui::SetCurrentContext(context_); auto& io = ImGui::GetIO(); io.DisplaySize = {width, height}; io.DeltaTime = 1.0f / 60;
        ImGui::NewFrame(); DrawApparitionPanel(model, nullptr, std::nullopt, std::nullopt, rows);
        if (referenceLastHint) {
            // A single unclipped reference line checks final glyph coverage, not a second panel implementation
            ImGui::GetForegroundDrawList()->AddText(ImGui::GetFont(), 13, {4, 178.5f}, IM_COL32_WHITE, "Melee: Exit");
        }
        ImGui::Render();
        auto& data = *ImGui::GetDrawData(); textures.Update(data); return data;
    }
    CpuTextures textures;
private:
    ImGuiContext* context_;
};
MenuSnapshot Model(std::size_t count = 37) {
    MenuSnapshot model; model.isOpen = true; model.title = "Menu position fixture";
    model.context = "CPU TEST / DISCONNECTED"; model.notice = "No game is connected";
    model.menuRevision = 5; model.presentationFocusRevision = 0;
    model.rows.resize(count, {"Current player", "ON", true}); return model;
}
bool HasColorIn(const ImDrawData& data, ImU32 color, layout::Rect area) {
    for (auto* list : data.CmdLists) for (const auto& vertex : list->VtxBuffer)
        if (vertex.col == color && vertex.pos.x >= area.x && vertex.pos.x < area.Right()
            && vertex.pos.y >= area.y && vertex.pos.y < area.Bottom()) return true;
    return false;
}
void FiniteClips(const ImDrawData& data, float width, float height) {
    Require(data.TotalVtxCount < 100000, "Menu drawing work exceeded its fixed bound");
    for (auto* list : data.CmdLists) {
        for (const auto& vertex : list->VtxBuffer)
            Require(std::isfinite(vertex.pos.x) && std::isfinite(vertex.pos.y), "Menu emitted nonfinite vertices");
        for (const auto& command : list->CmdBuffer)
            Require(command.ClipRect.x >= 0 && command.ClipRect.y >= 0 && command.ClipRect.z <= width
                && command.ClipRect.w <= height, "Menu escaped its viewport clip");
    }
}
}
void AddMenuPositionPanelChecks(std::vector<std::pair<std::string, std::function<void()>>>& checks) {
    checks.emplace_back("Menu reposition draws the retained title and seven controls without ordinary options", [] {
        CpuPanel panel; auto model = Model(); model.title = "Menu Customization"; model.rows.clear();
        model.menuPositionEditor = MenuPositionEditor{};
        const auto& data = panel.Draw(model, nullptr);
        Require(HasColorIn(data, IM_COL32(57, 152, 254, 255), {0, 65, 260, 19}), "The reposition editor removed the retained title");
        Require(HasColorIn(data, IM_COL32(0, 255, 0, 255), {0, 190, 260, 15}), "The confirmed editor offset is absent");
        Require(HasColorIn(data, IM_COL32(255, 255, 255, 255), {0, 181, 260, 14}), "The seventh editor control is missing");
        FiniteClips(data, 640, 480);
    });
    checks.emplace_back("The seventh reposition hint has complete glyphs and remains separate from the offset", [] {
        CpuPanel panel; auto model = Model(); model.rows.clear(); model.menuPositionEditor = MenuPositionEditor{};
        auto actual = panel.textures.Raster(panel.Draw(model, nullptr), 640, 480);
        model.menuPositionEditor->instructions[6].clear();
        auto reference = panel.textures.Raster(panel.Draw(model, nullptr, 640, 480, true), 640, 480);
        for (int y = 175; y < 207; ++y) for (int x = 0; x < 260; ++x) for (int channel = 0; channel < 3; ++channel)
            Require(actual[(x + y * 640) * 3 + channel] == reference[(x + y * 640) * 3 + channel],
                "The last editor control lost glyphs or overlapped the offset anchor");
    });
    checks.emplace_back("Moved menus retain their footer but adaptive instructions follow the new body", [] {
        CpuPanel panel; auto model = Model(8); auto original = MakeInstructionContent(model);
        model.appearance.menuPosition = MenuPosition{37, 126}; model.menuPositionEditor = MenuPositionEditor{};
        const auto contextual = MakeInstructionContent(model);
        Require(contextual.Line(0) == "Reposition Menu" && contextual.Line(1) == "Menu navigation paused"
            && contextual.Line(2) == "Melee: Exit editor", "The editor footer advertised inactive ordinary controls");
        Require(model.instructions[0] == original.Line(0), "The modal footer overwrote ordinary saved help");
        const auto& ordinary = panel.Draw(model, nullptr);
        Require(HasColorIn(ordinary, IM_COL32(255, 255, 255, 255), {255, 430, 370, 42}),
            "Moving the menu moved the independent ordinary footer");
        model.appearance.adaptiveInstructions = true;
        const auto& adaptive = panel.Draw(model, nullptr);
        Require(HasColorIn(adaptive, IM_COL32(255, 255, 255, 255), {38, 266, 258, 42}),
            "Adaptive contextual help did not follow the editor body");
        model.appearance.instructionsVisible = false;
        Require(!HasColorIn(panel.Draw(model, nullptr), IM_COL32(255, 255, 255, 255), {38, 266, 258, 42}),
            "A hidden editor footer kept drawing its help");
    });
    checks.emplace_back("Moving a menu resets cursor motion and keeps width presentation relative to its new origin", [] {
        auto model = Model(8); ScrollPresentation animation;
        animation.Update(MakeScrollRequest(model, 640, 480), 0); model.selectedIndex = 1;
        animation.Update(MakeScrollRequest(model, 640, 480), 100);
        model.appearance.menuPosition = MenuPosition{37, 126};
        const auto settled = animation.Update(MakeScrollRequest(model, 640, 480), 130);
        Require(!settled.transitioning && settled.scroller == 1, "A moved panel carried the previous cursor path");
        CpuPanel panel; model.widthEditor = WidthEditor{}; model.appearance.menuWidth = 500;
        const auto hud = layout::MakeHud({640, 480, 0, 0, 260, 3, false, 0, model.appearance, true, 380});
        Require(hud.body.x == 37 && hud.body.y == 126 && hud.body.width == 380,
            "Width interpolation replaced the committed menu origin");
        FiniteClips(panel.Draw(model, nullptr), 640, 480);
    });
    checks.emplace_back("Missing and hostile local editor hints stay bounded and never advertise guessed controls", [] {
        for (int width : {200, 260, 500}) {
            CpuPanel panel; auto model = Model(); model.rows.clear(); model.menuPositionEditor = MenuPositionEditor{};
            model.appearance.menuWidth = width; model.menuPositionEditor->instructions.clear();
            const auto& unavailable = panel.Draw(model, nullptr);
            Require(HasColorIn(unavailable, IM_COL32_WHITE, {0, 100, static_cast<float>(width), 16}),
                "Missing private controls had no honest unavailable message");
            model.menuPositionEditor->instructions.resize(1000, std::string(1000, 'W') + " 中");
            model.menuPositionEditor->step = 11;
            FiniteClips(panel.Draw(model, nullptr, 320, 240), 320, 240);
            Require(HasColorIn(panel.Draw(model, nullptr), IM_COL32(0, 255, 0, 255), {0, 190, static_cast<float>(width), 15}),
                "Invalid local step had no explicit UNKNOWN display");
        }
    });
    checks.emplace_back("Menu editors and returning phases stay clipped at negative and partially visible anchors", [] {
        for (const auto position : {MenuPosition{-30, 45}, MenuPosition{200, 200}, MenuPosition{600, 460}})
            for (unsigned mode = 0; mode < 5; ++mode) {
                CpuPanel panel; auto model = Model(); model.appearance.menuPosition = position;
                if (mode == 0) model.menuPositionEditor = MenuPositionEditor{};
                if (mode == 1) model.widthEditor = WidthEditor{};
                if (mode == 2) model.instructionPositionEditor = InstructionPositionEditor{};
                if (mode == 3) model.menuUnlock = MenuUnlockPhase::Rebuild;
                if (mode == 4) model.menuUnlock = MenuUnlockPhase::Pause;
                FiniteClips(panel.Draw(model, nullptr), 640, 480);
                Require(model.appearance.menuPosition == position, "A returning phase reset saved menu coordinates");
            }
    });
    checks.emplace_back("Extreme saved menu anchors remain finite and clipped while status stays visible", [] {
        for (auto position : {MenuPosition{std::numeric_limits<std::int32_t>::min(), std::numeric_limits<std::int32_t>::max()},
            MenuPosition{std::numeric_limits<std::int32_t>::max(), std::numeric_limits<std::int32_t>::min()}, MenuPosition{-400, -400}}) {
            CpuPanel panel; auto model = Model(); model.appearance.menuPosition = position;
            const auto& data = panel.Draw(model, nullptr); FiniteClips(data, 640, 480);
            Require(HasColorIn(data, IM_COL32(255, 255, 255, 255), {0, 0, 640, 430}),
                "An offscreen saved menu hid its session status");
            Require(model.appearance.menuPosition == position, "The renderer clamped a saved menu anchor");
        }
    });
}
void VerifyMenuPositionBackendPreviews(const std::filesystem::path& fixtures, const std::filesystem::path& output) {
    const auto read = [&](const char* name) { return ReadBackendPreview(fixtures / name); };
    auto original = read("menu-position-editor-default.json");
    const auto moved = read("menu-position-editor-moved.json");
    const auto maximum = read("menu-position-editor-step-max.json");
    const auto rebuilding = read("menu-position-unlock-rebuild.json");
    const auto paused = read("menu-position-unlock-pause.json");
    const auto returned = read("customization-positioned.json");
    Require(original.menuPositionEditor && original.menuPositionEditor->step == 1 && !original.appearance.menuPosition
        && original.title == "Menu Customization" && original.rows.empty() && original.selectedIndex == 0,
        "Actual immediate editor entry changed source title or metadata");
    Require(moved.menuPositionEditor && maximum.menuPositionEditor, "Actual moving editor metadata is missing");
    Require(moved.appearance.menuPosition == MenuPosition{1, 86} && moved.menuPositionEditor->step == 1
        && maximum.appearance.menuPosition == MenuPosition{11, 96} && maximum.menuPositionEditor->step == 10
        && moved.menuRevision == original.menuRevision && maximum.menuRevision == original.menuRevision,
        "Actual confirmed coordinates, step or stable editor context were lost");
    Require(!rebuilding.menuPositionEditor && rebuilding.menuUnlock == MenuUnlockPhase::Rebuild
        && !paused.menuPositionEditor && paused.menuUnlock == MenuUnlockPhase::Pause
        && !returned.menuUnlock && !returned.menuPositionEditor && returned.selectedIndex < returned.rows.size()
        && returned.rows[returned.selectedIndex].label == "Reposition Menu"
        && returned.appearance.menuPosition == MenuPosition{11, 96},
        "Actual returning phases removed the retained menu position or restored the wrong cursor");
    Require(original.notice.find("X: 0 Y: 85") != std::string::npos
        && moved.notice.find("X: 1 Y: 86") != std::string::npos
        && maximum.notice.find("X: 11 Y: 96") != std::string::npos,
        "Actual editor status does not expose its confirmed coordinates for offscreen recovery");
    // A separate declared-binding image exercises the real native formatter without claiming live keys
    original.context = "PREVIEW / SAMPLE REPOSITION BINDINGS";
    original.notice = "Declared saved actions only. The footer remains separate from the menu. No game is connected.";
    original.menuPositionEditor->instructions = runtime::DescribeMenuPositionControls(
        {{2}, {86, 6}, {2, 38}, {1, 40}, {70}, {37}, {39}, {1}, {38}, {40}, {71}, {81}});
    original.menuPositionEditor->footerInstructions[2] = original.menuPositionEditor->instructions[6];
    CpuPanel panel; const auto& saved = panel.Draw(original, nullptr, 960, 720);
    FiniteClips(saved, 960, 720);
    WritePreview(output / "menu-position-saved-controls.ppm", panel.textures.Raster(saved, 960, 720), 960, 720);
    // This explicit edge-case demo leaves the current coordinates unmodified by rendering
    original.appearance.menuPosition = MenuPosition{std::numeric_limits<std::int32_t>::max(),
        std::numeric_limits<std::int32_t>::min()};
    original.context = "PREVIEW / OFFSCREEN POSITION DEMO";
    original.notice = "Declared extreme signed coordinates. The saved menu is offscreen; status and independent footer remain visible. No game is connected.";
    const auto& offscreen = panel.Draw(original, nullptr, 960, 720); FiniteClips(offscreen, 960, 720);
    WritePreview(output / "menu-position-offscreen-demo.ppm", panel.textures.Raster(offscreen, 960, 720), 960, 720);
    std::cout << "PASS Actual backend menu position entry, confirmed coordinates, step and returning phases\n";
}

}
