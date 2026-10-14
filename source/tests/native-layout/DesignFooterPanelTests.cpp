#include "DesignFooterPanelTests.h"
#include "CpuDrawlist.h"
#include "BackendPreview.h"
#include "../../native/render/ApparitionPanel.h"
#include "../../native/render/InstructionPanel.h"
#include "../../native/render/InstructionGeometry.h"
#include "../../native/render/ScrollRows.h"
#include <imgui.h>
#include <algorithm>
#include <cmath>
#include <iostream>
#include <limits>
#include <stdexcept>

namespace iw::overlay::tests {
namespace {
void Require(bool value, const char* reason) {
    if (!value) {
        throw std::runtime_error(reason);
    }
}
class CpuPanel final {
    public:
    CpuPanel() {
        context_ = ImGui::CreateContext();
        ImGui::SetCurrentContext(context_);
        auto& io = ImGui::GetIO();
        io.IniFilename = nullptr;
        io.LogFilename = nullptr;
        io.BackendFlags |= ImGuiBackendFlags_RendererHasTextures | ImGuiBackendFlags_RendererHasVtxOffset;
        ImGui::GetPlatformIO().Renderer_TextureMaxWidth = 4096;
        ImGui::GetPlatformIO().Renderer_TextureMaxHeight = 4096;
    }
    ~CpuPanel() {
        ImGui::DestroyContext(context_);
    }
    ImDrawData& Draw(const MenuSnapshot& model, float width = 640, float height = 480,
                     const ScrollFrame* rows = nullptr, std::optional<layout::Color> color = std::nullopt) {
        // The production panel and official texture-update contract run without a graphics backend
        ImGui::SetCurrentContext(context_);
        auto& io = ImGui::GetIO();
        io.DisplaySize = {width, height};
        io.DeltaTime = 1.0f / 60;
        ImGui::NewFrame();
        DrawApparitionPanel(model, nullptr, std::nullopt, color, rows);
        ImGui::Render();
        auto& data = *ImGui::GetDrawData();
        textures.Update(data);
        return data;
    }
    CpuTextures textures;

    private:
    ImGuiContext* context_;
};
MenuSnapshot Model(MenuDesign design = MenuDesign::Apparition) {
    MenuSnapshot model;
    model.isOpen = true;
    model.isRootMenu = false;
    model.title = "Menu Preferences";
    model.context = "CPU TEST / DISCONNECTED";
    model.notice = "No game is connected";
    model.appearance.design = design;
    model.menuRevision = 3;
    model.presentationFocusRevision = 0;
    model.rows.resize(8, {"Current preference", "OFF", true});
    model.appearance.coloredCursor = false;
    model.appearance.adaptiveInstructions = true;
    model.menuDesignTransition = MenuDesignTransitionPhase::Rebuild;
    model.menuDesignFooterDesign = MenuDesign::Aio;
    return model;
}
bool HasColorIn(const ImDrawData& data, ImU32 color, layout::Rect area) {
    for (auto* list : data.CmdLists) {
        for (const auto& vertex : list->VtxBuffer) {
            if (vertex.col == color && vertex.pos.x >= area.x && vertex.pos.x < area.Right() &&
                vertex.pos.y >= area.y && vertex.pos.y < area.Bottom()) {
                return true;
            }
        }
    }
    return false;
}
void FiniteClips(const ImDrawData& data, float width, float height) {
    Require(data.TotalVtxCount < 100000 && data.TotalIdxCount < 150000, "A design exceeded bounded drawing work");
    for (auto* list : data.CmdLists) {
        for (const auto& vertex : list->VtxBuffer) {
            Require(std::isfinite(vertex.pos.x) && std::isfinite(vertex.pos.y) && std::isfinite(vertex.uv.x) &&
                        std::isfinite(vertex.uv.y),
                    "A design emitted nonfinite vertices");
        }
        for (const auto& command : list->CmdBuffer) {
            Require(command.ClipRect.x >= 0 && command.ClipRect.y >= 0 && command.ClipRect.z <= width &&
                        command.ClipRect.w <= height,
                    "A design escaped its viewport clip");
        }
    }
}
} // namespace
void AddDesignFooterPanelChecks(std::vector<std::pair<std::string, std::function<void()>>>& checks) {
    checks.emplace_back(
        "Design rebuild uses committed footer anchors while retaining the captured background paint", [] {
            CpuPanel panel;
            auto model = Model();
            // Only the new geometry crosses into the absent menu gap, not the target's black fill
            Require(HasColorIn(panel.Draw(model), IM_COL32(42, 42, 42, 255), {-1.1f, 431.9f, 262.2f, 40.2f}),
                    "Committed AIO footer geometry did not retain the old Apparition fill");
            Require(!MakeScrollRequest(model, 640, 480).active && model.appearance.design == MenuDesign::Apparition,
                    "Footer geometry changed control policy or committed captured paint");
        });
    checks.emplace_back("All twenty-five Design pairs preserve old fill while positioning target adaptive help", [] {
        CpuPanel panel;
        for (auto old :
             {MenuDesign::Apparition, MenuDesign::Classic, MenuDesign::Native, MenuDesign::Aio, MenuDesign::Basic}) {
            for (auto target : {MenuDesign::Apparition, MenuDesign::Classic, MenuDesign::Native, MenuDesign::Aio,
                                MenuDesign::Basic}) {
                auto model = Model(old);
                model.menuDesignFooterDesign = target;
                const auto expected = old == MenuDesign::Apparition ? IM_COL32(42, 42, 42, 255)
                                      : old == MenuDesign::Classic  ? IM_COL32(25, 25, 25, 217)
                                                                    : IM_COL32(0, 0, 0, 255);
                const float x = target == MenuDesign::Aio                                      ? -1
                                : target == MenuDesign::Classic || target == MenuDesign::Basic ? 0
                                                                                               : 1;
                const auto& data = panel.Draw(model);
                Require(HasColorIn(data, expected, {x - .1f, 431.9f, 270, 40.2f}),
                        "A target Design repainted its retained footer");
                FiniteClips(data, 640, 480);
            }
        }
    });
    checks.emplace_back("Missing and invalid footer descriptors keep exact legacy captured gap pixels", [] {
        CpuPanel panel;
        auto model = Model(MenuDesign::Classic);
        model.menuDesignFooterDesign.reset();
        const auto geometry = ResolveInstructionGeometry(model);
        Require(geometry.presence == layout::MenuHudPresence::Present && !geometry.design,
                "Legacy gap geometry became absent without a known target");
        const auto& legacyData = panel.Draw(model);
        Require(HasColorIn(legacyData, IM_COL32(25, 25, 25, 217), {0, 240.9f, 270, 40.2f}),
                "Legacy captured gap stopped following its present Classic body");
        const auto expected = panel.textures.Raster(legacyData, 640, 480);
        model.menuDesignFooterDesign = static_cast<MenuDesign>(999);
        Require(panel.textures.Raster(panel.Draw(model), 640, 480) == expected,
                "An unknown local footer target invented a geometry change");
        model.menuDesignFooterDesign.reset();
        const auto request = MakeScrollRequest(model, 640, 480);
        Require(!request.active && request.layout.design == MenuDesign::Classic,
                "Legacy descriptor fallback changed cursor policy");
    });
    checks.emplace_back("Footer overrides outside rebuild are ignored and cooldown uses only its declared new style",
                        [] {
                            CpuPanel panel;
                            auto model = Model(MenuDesign::Aio);
                            model.menuDesignTransition = MenuDesignTransitionPhase::Cooldown;
                            model.menuDesignFooterDesign.reset();
                            const auto expected = panel.textures.Raster(panel.Draw(model), 640, 480);
                            model.menuDesignFooterDesign = MenuDesign::Classic;
                            Require(panel.textures.Raster(panel.Draw(model), 640, 480) == expected,
                                    "A stale rebuild footer target leaked into cooldown");
                            model.menuDesignTransition.reset();
                            const auto ready = panel.textures.Raster(panel.Draw(model), 640, 480);
                            model.menuDesignFooterDesign.reset();
                            Require(panel.textures.Raster(panel.Draw(model), 640, 480) == ready,
                                    "A stale descriptor changed settled geometry");
                        });
    checks.emplace_back("Known Design footer targets preserve hidden plain alternate and nonadaptive footer rules", [] {
        CpuPanel panel;
        for (bool visible : {false, true}) {
            for (bool background : {false, true}) {
                for (bool alternate : {false, true}) {
                    auto model = Model();
                    model.appearance.instructionsVisible = visible;
                    model.appearance.instructionsBackground = background;
                    model.appearance.alternateInstructions = alternate;
                    model.appearance.adaptiveInstructions = !alternate;
                    const auto& data = panel.Draw(model);
                    FiniteClips(data, 640, 480);
                    Require(HasColorIn(data, IM_COL32_WHITE, {0, 420, 540, 60}) == visible,
                            "Footer decoration or style removed text or exposed hidden help");
                    if (!alternate) {
                        model.appearance.adaptiveInstructions = false;
                        const auto overridden = panel.textures.Raster(panel.Draw(model), 640, 480);
                        model.menuDesignFooterDesign.reset();
                        Require(panel.textures.Raster(panel.Draw(model), 640, 480) == overridden,
                                "Nonadaptive help used target layout metrics");
                    }
                }
            }
        }
    });
    checks.emplace_back(
        "Footer geometry neither changes epochs nor reuses row frames and keeps extreme status readable", [] {
            CpuPanel panel;
            auto model = Model();
            model.navigationRevision = 7;
            auto legacy = model;
            legacy.menuDesignFooterDesign.reset();
            const auto before = MakeScrollRequest(legacy, 960, 720);
            const auto after = MakeScrollRequest(model, 960, 720);
            Require(before.layout == after.layout && before.menuRevision == after.menuRevision && !after.active &&
                        !after.opening,
                    "Footer-only geometry changed row context or invented an opening animation");
            model.appearance.menuPosition =
                MenuPosition{std::numeric_limits<std::int32_t>::max(), std::numeric_limits<std::int32_t>::min()};
            model.appearance.instructionPosition =
                InstructionPosition{std::numeric_limits<std::int32_t>::min(), std::numeric_limits<std::int32_t>::max()};
            const auto& data = panel.Draw(model, 960, 720);
            FiniteClips(data, 960, 720);
            Require(data.TotalVtxCount > 0 && model.navigationRevision == 7 && model.menuRevision == 3,
                    "Extreme footer positioning removed status or mutated owner epochs");
        });
}
void VerifyDesignFooterBackendPreviews(const std::filesystem::path& fixtures, const std::filesystem::path& output) {
    auto start = ReadBackendPreview(fixtures / "preferences-design-adaptive-aio-rebuild.json");
    const auto before = ReadBackendPreview(fixtures / "preferences-design-adaptive-aio-pre-rebuild.json");

    // Actual packets preserve captured paint and epochs throughout the first forty-nine milliseconds
    Require(start.menuDesignTransition == MenuDesignTransitionPhase::Rebuild &&
                before.menuDesignTransition == MenuDesignTransitionPhase::Rebuild &&
                start.menuDesignFooterDesign == MenuDesign::Aio && before.menuDesignFooterDesign == MenuDesign::Aio &&
                start.appearance.design == MenuDesign::Apparition &&
                before.appearance.design == MenuDesign::Apparition && start.appearance.adaptiveInstructions &&
                before.appearance.adaptiveInstructions && start.rows.size() == 12 && before.rows.size() == 12 &&
                start.navigationRevision == before.navigationRevision && start.menuRevision == before.menuRevision,
            "Actual adaptive target, captured paint or rebuild identity differs");

    const auto geometry = ResolveInstructionGeometry(start);
    Require(geometry.presence == layout::MenuHudPresence::Absent && geometry.design == MenuDesign::Aio,
            "Actual typed Design rebuild did not resolve its absent-HUD geometry");

    CpuPanel panel;
    const auto actual = panel.textures.Raster(panel.Draw(start), 640, 480);
    Require(panel.textures.Raster(panel.Draw(before), 640, 480) == actual,
            "The stable 0/49 ms rebuild changed its footer raster");
    Require(HasColorIn(panel.Draw(start), IM_COL32(42, 42, 42, 255), {-1.1f, 431.9f, 262.2f, 40.2f}),
            "Actual target geometry recreated AIO paint before the owner cooldown");

    // A missing descriptor remains a deliberate legacy fallback rather than a row-text inference
    auto legacy = start;
    legacy.menuDesignFooterDesign.reset();
    Require(panel.textures.Raster(panel.Draw(legacy), 640, 480) != actual,
            "Actual committed target geometry was indistinguishable from the legacy captured gap");
    legacy.context = "PREVIEW / LEGACY GEOMETRY FALLBACK";
    legacy.notice = "Actual producer packet with the optional footer descriptor explicitly removed for this CPU demo. "
                    "Captured geometry remains present.";
    const auto& legacyData = panel.Draw(legacy, 960, 720);
    WritePreview(output / "design-footer-legacy-fallback-demo.ppm", panel.textures.Raster(legacyData, 960, 720), 960,
                 720);
    std::cout << "PASS Actual two adaptive Design footer profiles, captured paint, legacy fallback and stable epochs\n";
}
} // namespace iw::overlay::tests
