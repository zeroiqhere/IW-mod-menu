#include "StealthPanelTests.h"
#include "../../native/render/ApparitionPanel.h"
#include "../../native/render/InstructionGeometry.h"
#include "../../native/render/InstructionPanel.h"
#include "../../native/render/ScrollRows.h"
#include "BackendPreview.h"
#include "CpuDrawlist.h"
#include <cmath>
#include <imgui.h>
#include <stdexcept>

namespace iw::overlay::tests {
namespace {
void Require(bool value, const char *reason) {
    if (!value)
        throw std::runtime_error(reason);
}
class CpuPanel final {
  public:
    CpuPanel() {
        context_ = ImGui::CreateContext();
        ImGui::SetCurrentContext(context_);
        auto &io = ImGui::GetIO();
        io.IniFilename = nullptr;
        io.LogFilename = nullptr;
        io.BackendFlags |= ImGuiBackendFlags_RendererHasTextures | ImGuiBackendFlags_RendererHasVtxOffset;
        ImGui::GetPlatformIO().Renderer_TextureMaxWidth = 4096;
        ImGui::GetPlatformIO().Renderer_TextureMaxHeight = 4096;
    }
    ~CpuPanel() {
        ImGui::DestroyContext(context_);
    }
    ImDrawData &Draw(const MenuSnapshot &model, float width = 640, float height = 480,
                     const ScrollFrame *rows = nullptr, std::optional<layout::Color> color = std::nullopt,
                     PanelDrawSummary *summary = nullptr) {
        // The production panel and official texture-update contract run without a graphics backend
        ImGui::SetCurrentContext(context_);
        auto &io = ImGui::GetIO();
        io.DisplaySize = {width, height};
        io.DeltaTime = 1.0f / 60;
        ImGui::NewFrame();
        DrawApparitionPanel(model, summary, std::nullopt, color, rows);
        ImGui::Render();
        auto &data = *ImGui::GetDrawData();
        textures.Update(data);
        return data;
    }
    CpuTextures textures;

  private:
    ImGuiContext *context_;
};

MenuSnapshot Model() {
    MenuSnapshot model;
    model.isOpen = true;
    model.isRootMenu = false;
    model.title = "Menu Preferences";
    model.appearance.stealthUI = true;
    model.appearance.booleanDisplay = "Text";
    model.menuRevision = 6;
    model.presentationFocusRevision = 0;
    model.rows.resize(12, {"Preference", "OFF", true});
    return model;
}
} // namespace
void AddStealthPanelChecks(std::vector<std::pair<std::string, std::function<void()>>> &checks) {
    checks.emplace_back(
        "Every Stealth Design draws five logical options without changing palette or width", [] {
            CpuPanel panel;
            for (auto design : {MenuDesign::Apparition, MenuDesign::Classic, MenuDesign::Native,
                                MenuDesign::Aio, MenuDesign::Basic}) {
                auto model = Model();
                model.appearance.design = design;
                model.appearance.coloredCursor = false;
                PanelDrawSummary summary;
                auto &data = panel.Draw(model, 640, 480, nullptr, std::nullopt, &summary);
                Require(summary.shownRows == 5 && summary.firstRow == 0 && data.TotalVtxCount > 0,
                        "Compact production panel did not draw its five-row window");
                for (auto *list : data.CmdLists)
                    for (const auto &vertex : list->VtxBuffer)
                        Require(std::isfinite(vertex.pos.x) && std::isfinite(vertex.pos.y),
                                "Compact panel emitted nonfinite geometry");
                Require(model.appearance.menuWidth == 260 && model.accent == "#3998FE",
                        "Compact drawing changed appearance data");
            }
        });
    checks.emplace_back(
        "Preference rebuild hides rows while preserving typed paused help and captured geometry", [] {
            CpuPanel panel;
            auto model = Model();
            model.menuPreferenceRebuild = MenuPreferenceRebuildPhase::Rebuild;
            model.appearance.adaptiveInstructions = true;
            PanelDrawSummary summary;
            panel.Draw(model, 640, 480, nullptr, std::nullopt, &summary);
            const auto geometry = ResolveInstructionGeometry(model);
            const auto help = MakeInstructionContent(model);
            Require(summary.shownRows == 0 && summary.firstRow == 0 &&
                        geometry.presence == layout::MenuHudPresence::Absent && !geometry.design &&
                        help.LineCount() == 3 && help.Line(0) == "Updating menu" &&
                        help.Line(1) == "Menu controls paused" && !MakeScrollRequest(model, 640, 480).active,
                    "Rebuild exposed rows, ordinary controls or a Design target");
        });
    checks.emplace_back(
        "Preference cooldown shows compact rows and accepts only its bounded redraw fade", [] {
            CpuPanel panel;
            auto model = Model();
            model.menuPreferenceRebuild = MenuPreferenceRebuildPhase::Cooldown;
            model.menuOpening = MenuOpening{2, 200};
            PanelDrawSummary summary;
            const auto request = MakeScrollRequest(model, 640, 480);
            Require(!request.active && request.opening == model.menuOpening &&
                        ResolveInstructionGeometry(model).presence == layout::MenuHudPresence::Present,
                    "Cooldown lost its fade or allowed ordinary scroll animation");
            panel.Draw(model, 640, 480, nullptr, std::nullopt, &summary);
            Require(summary.shownRows == 5 && MakeInstructionContent(model).Line(1) == "Menu controls paused",
                    "Cooldown kept rows hidden or restored ordinary help early");
        });
    checks.emplace_back(
        "Ordinary Stealth windows keep absolute indices and reject stale noncompact frames", [] {
            CpuPanel panel;
            auto model = Model();
            model.appearance.stealthUI = false;
            const auto old = StaticScrollFrame(MakeScrollRequest(model, 640, 480));
            model.appearance.stealthUI = true;
            for (std::size_t selected : {std::size_t{0}, std::size_t{4}, std::size_t{11}}) {
                model.selectedIndex = selected;
                PanelDrawSummary summary;
                panel.Draw(model, 640, 480, &old, std::nullopt, &summary);
                Require(summary.shownRows == 5 && summary.firstRow == (selected < 2   ? 0
                                                                       : selected > 9 ? 7
                                                                                      : selected - 2),
                        "Stale ordinary-capacity frame moved a compact cursor window");
            }
        });
    checks.emplace_back("Pending preference refresh preserves opening alpha until the actual redraw", [] {
        CpuPanel panel;
        auto model = Model();
        model.appearance.stealthUI = false;
        model.appearance.booleanDisplay = "Boxes";
        model.menuOpening = MenuOpening{2, 729};
        model.menuPreferenceRefresh = MenuPreferenceRefreshPhase::Pending;
        const auto request = MakeScrollRequest(model, 640, 480);
        Require(!request.active && request.opening == model.menuOpening,
                "Pending preference refresh discarded the old opening descriptor");
        const auto fading = StaticScrollFrame(request);
        panel.Draw(model, 640, 480, &fading);
        model.menuPreferenceRefresh = MenuPreferenceRefreshPhase::Cooldown;
        Require(!MakeScrollRequest(model, 640, 480).opening,
                "Preference redraw retained an expired opening descriptor");
    });
    checks.emplace_back(
        "Long saved hints never request a zero-sized baked font in ordinary and editor panels", [] {
            CpuPanel panel;
            for (auto viewport : {layout::Point{320, 240}, layout::Point{160, 120}}) {
                for (bool alternate : {false, true})
                    for (int editor = 0; editor < 3; ++editor) {
                        auto model = Model();
                        model.appearance.alternateInstructions = alternate;
                        model.instructions.assign(100, std::string(250, 'K'));
                        if (editor == 1) {
                            model.widthEditor = WidthEditor{};
                            model.widthEditor->instructions = model.instructions;
                        }
                        if (editor == 2) {
                            model.menuPositionEditor = MenuPositionEditor{};
                            model.menuPositionEditor->instructions = model.instructions;
                        }
                        const auto &data = panel.Draw(model, viewport.x, viewport.y);
                        for (auto *list : data.CmdLists)
                            for (const auto &command : list->CmdBuffer)
                                Require(command.ClipRect.x >= 0 && command.ClipRect.y >= 0 &&
                                            command.ClipRect.z <= viewport.x &&
                                            command.ClipRect.w <= viewport.y,
                                        "Long saved hints escaped viewport clipping");
                        const auto content = MakeInstructionContent(model);
                        layout::Request request{viewport.x, viewport.y,          12,    0,
                                                500,        content.LineCount(), false, 0};
                        request.appearance = model.appearance;
                        Require(FitInstructionText(content, layout::MakeHud(request)) >= 1.0f,
                                "Long saved controls rounded the fitted font to zero");
                    }
            }
        });
    checks.emplace_back("Preference rebuild metadata changes footer and body only for its own lifetime", [] {
        CpuPanel panel;
        auto model = Model();
        panel.Draw(model);
        const auto normal = panel.textures.Raster(panel.Draw(model), 640, 480);
        model.menuPreferenceRebuild = MenuPreferenceRebuildPhase::Rebuild;
        panel.Draw(model);
        model.menuPreferenceRebuild.reset();
        Require(panel.textures.Raster(panel.Draw(model), 640, 480) == normal &&
                    MakeInstructionContent(model).Line(0) == model.instructions[0],
                "Completed rebuild retained paused help or changed settled pixels");
    });
}
} // namespace iw::overlay::tests

namespace iw::overlay::tests {
void VerifyStealthBackendPreviews(const std::filesystem::path &fixtures) {
    CpuPanel panel;
    for (const auto *prefix : {"stealth-rebuild", "counter-on", "counter-off"}) {
        const auto hidden = ReadBackendPreview(fixtures / (std::string(prefix) + "-rebuild.json"));
        const auto before = ReadBackendPreview(fixtures / (std::string(prefix) + "-pre-rebuild.json"));
        const auto drawn = ReadBackendPreview(fixtures / (std::string(prefix) + "-cooldown.json"));
        PanelDrawSummary summary;
        panel.Draw(hidden, 640, 480, nullptr, std::nullopt, &summary);
        Require(summary.shownRows == 0 &&
                    hidden.menuPreferenceRebuild == MenuPreferenceRebuildPhase::Rebuild &&
                    hidden.appearance == before.appearance && hidden.menuRevision == before.menuRevision &&
                    !ResolveInstructionGeometry(hidden).design,
                "Actual local rebuild leaked rows or a Design target");
        const auto initial = StaticScrollFrame(MakeScrollRequest(drawn, 640, 480));
        panel.Draw(drawn, 640, 480, &initial, std::nullopt, &summary);
        Require(summary.shownRows == (drawn.appearance.stealthUI       ? 5
                                      : drawn.appearance.optionCounter ? 8
                                                                       : 10) &&
                    drawn.menuRevision == *hidden.menuRevision + 1 &&
                    drawn.navigationRevision == hidden.navigationRevision && drawn.menuOpening &&
                    initial.opening == drawn.menuOpening,
                "Actual local redraw used a stale capacity, public epoch or row fade");
    }
    const auto pending = ReadBackendPreview(fixtures / "stealth-retained-opening-pending.json");
    const auto before = ReadBackendPreview(fixtures / "stealth-retained-opening-pre-refresh.json");
    const auto cooldown = ReadBackendPreview(fixtures / "stealth-retained-opening-cooldown.json");
    Require(pending.menuOpening == MenuOpening{2, 550} && before.menuOpening == MenuOpening{2, 729} &&
                !cooldown.menuOpening && pending.menuRevision == before.menuRevision &&
                cooldown.menuRevision == *pending.menuRevision + 1,
            "Actual retained opening changed before the ordinary preference redraw");
    for (const auto *model : {&pending, &before, &cooldown}) {
        const auto frame = StaticScrollFrame(MakeScrollRequest(*model, 640, 480));
        PanelDrawSummary summary;
        panel.Draw(*model, 640, 480, &frame, std::nullopt, &summary);
        Require(summary.shownRows == (model->appearance.stealthUI ? 5 : 10),
                "Actual pending preference frame borrowed its later compact window");
    }
}
} // namespace iw::overlay::tests
