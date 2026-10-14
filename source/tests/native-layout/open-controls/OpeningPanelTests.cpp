#include "OpeningPanelTests.h"
#include "../../../native/render/ScrollRows.h"
#include "../../../native/runtime/ControlLabels.h"
#include "../../../native/runtime/OpeningControlsSchema.h"
#include "../CpuDrawlist.h"
#include <cmath>
#include <nlohmann/json.hpp>
#include <stdexcept>

namespace iw::overlay::tests {
namespace {
void Require(bool value, const char *message) {
    if (!value) {
        throw std::runtime_error(message);
    }
}
class RowFrame final {
  public:
    RowFrame() {
        context_ = ImGui::CreateContext();
        auto &io = ImGui::GetIO();
        io.IniFilename = nullptr;
        io.LogFilename = nullptr;
        io.BackendFlags |= ImGuiBackendFlags_RendererHasTextures | ImGuiBackendFlags_RendererHasVtxOffset;
        ImGui::GetPlatformIO().Renderer_TextureMaxWidth = 4096;
        ImGui::GetPlatformIO().Renderer_TextureMaxHeight = 4096;
    }
    ~RowFrame() { ImGui::DestroyContext(context_); }
    std::vector<ImDrawVert> Draw(MenuSnapshot model, std::optional<float> decoration = std::nullopt) {
        auto &io = ImGui::GetIO();
        io.DisplaySize = {960, 720};
        io.DeltaTime = 1.f / 60;
        ImGui::NewFrame();
        // The production row painter runs without a window, hook or graphics device
        const auto hud = layout::MakeHud(
            {960, 720, model.rows.size(), model.selectedIndex, 260, 3, false, 0, model.appearance});
        auto rows = StaticScrollFrame(MakeScrollRequest(model, 960, 720));
        for (std::size_t row = 0; row < rows.count; ++row) {
            rows.rows[row].decorationVisibility = decoration;
        }
        PrepareScrollRows(model, hud, rows);
        auto &draw = *ImGui::GetForegroundDrawList();
        DrawScrollRows(draw, model, hud, rows, layout::Theme(model.accent));
        const std::vector<ImDrawVert> vertices(draw.VtxBuffer.begin(), draw.VtxBuffer.end());
        ImGui::Render();
        textures_.Update(*ImGui::GetDrawData());
        return vertices;
    }

  private:
    ImGuiContext *context_;
    CpuTextures textures_;
};
void SourceSpacerPaintsOneBoundedRectangleAcrossDesigns() {
    for (auto design : {MenuDesign::Apparition, MenuDesign::Classic, MenuDesign::Native, MenuDesign::Aio,
                        MenuDesign::Basic}) {
        MenuSnapshot model;
        model.isOpen = true;
        model.rows = {{"", "", true}, {"", "", false, true}};
        model.appearance.design = design;
        model.appearance.coloredCursor = design != MenuDesign::Classic && design != MenuDesign::Native;
        const auto hud = layout::MakeHud({960, 720, 2, 0, 260, 3, false, 0, model.appearance});
        RowFrame frame;
        const auto vertices = frame.Draw(model, .5f);
        // One source spacer owns a filled four-corner quad rather than the disabled strike geometry
        Require(vertices.size() == 4, "Spacer emitted text or an additional disabled marker");
        const float center = hud.body.y + (hud.rowCenterOffset + 18) * hud.scale;
        float left = vertices[0].pos.x, right = left, top = vertices[0].pos.y, bottom = top;
        for (const auto &vertex : vertices) {
            left = std::min(left, vertex.pos.x);
            right = std::max(right, vertex.pos.x);
            top = std::min(top, vertex.pos.y);
            bottom = std::max(bottom, vertex.pos.y);
            Require((vertex.col >> 24) == 51, "Spacer ignored its opening decoration opacity");
        }
        Require(std::abs(left - (hud.body.x + 30 * hud.scale)) < .001f &&
                    std::abs(right - (hud.body.Right() - 30 * hud.scale)) < .001f &&
                    std::abs(top - (center - .5f * hud.scale)) < .001f &&
                    std::abs(bottom - (center + .5f * hud.scale)) < .001f,
                "Spacer changed source anchors or thickness");
        model.rows[1].isSpacer = false;
        Require(frame.Draw(model).size() > 4, "Ordinary disabled row lost its capability strike");
    }
}
void SavedOpeningCaptionPaintMatchesTheResolvedOrdinaryCaption() {
    runtime::Bindings bindings{{2}, {86, 6}, {2, 38}, {1, 40}, {70}};
    const auto labels = runtime::DescribeOpeningActions(bindings);
    MenuSnapshot typed;
    typed.isOpen = true;
    typed.rows = {{"Melee", "ON", true}, {"Jump", "UNAVAILABLE", false}};
    runtime::ParseOpeningRowMetadata(nlohmann::json{{"openingAction", 4u}}, typed.rows[0], &labels);
    runtime::ParseOpeningRowMetadata(nlohmann::json{{"openingAction", 12u}}, typed.rows[1], &labels);
    Require(typed.rows[0].label == "V / Mouse 5" && typed.rows[1].label == "Jump",
            "Mapped caption or missing-group logical fallback changed");
    RowFrame frame;
    const auto actual = frame.Draw(typed);
    auto ordinary = typed;
    for (auto &row : ordinary.rows) {
        row.openingAction.reset();
    }
    const auto expected = frame.Draw(ordinary);
    Require(actual.size() == expected.size() && !actual.empty(), "Typed caption changed row drawing work");
    // Metadata resolves once before painting and cannot alter the resolved row's mapped font or ink
    for (std::size_t index = 0; index < actual.size(); ++index) {
        Require(actual[index].pos.x == expected[index].pos.x &&
                    actual[index].pos.y == expected[index].pos.y && actual[index].col == expected[index].col,
                "Typed row changed resolved caption geometry or color");
    }
}
} // namespace
void AddOpeningControlsPanelChecks(std::vector<std::pair<std::string, std::function<void()>>> &checks) {
    checks.emplace_back("SourceSpacerPaintsOneBoundedRectangleAcrossDesigns",
                        SourceSpacerPaintsOneBoundedRectangleAcrossDesigns);
    checks.emplace_back("SavedOpeningCaptionPaintMatchesTheResolvedOrdinaryCaption",
                        SavedOpeningCaptionPaintMatchesTheResolvedOrdinaryCaption);
}
} // namespace iw::overlay::tests
