#include "WidthEditorPanel.h"
#include "PanelText.h"
#include "PanelGeometry.h"
#include <algorithm>
#include <limits>
#include <span>

namespace iw::overlay {
namespace {
std::span<const std::string> Hints(const WidthEditor& editor) {
    // The reference editor has five lines, so malformed local vectors stay bounded
    return {editor.instructions.data(), std::min<std::size_t>(editor.instructions.size(), 5)};
}
float EditorFont(const WidthEditor& editor, const layout::Hud& hud) {
    float longest = 0;
    for (const auto& instruction : Hints(editor)) {
        const auto line = BoundedPanelText(instruction, 256);
        longest = std::max(longest, ImGui::GetFont()->CalcTextSizeA(hud.rowFont,
            std::numeric_limits<float>::max(), 0, line.data(), line.data() + line.size()).x);
    }
    // Long saved labels stay clipped after fitting reaches the smallest whole-pixel font
    const float available = std::max(hud.scale, hud.body.width - 8 * hud.scale);
    return std::max(1.0f, longest > available ? hud.rowFont * available / longest : hud.rowFont);
}
std::string Offset(const WidthEditor& editor) {
    // Invalid local metadata must not resemble a confirmed bounded offset
    return editor.step >= 1 && editor.step <= 10 ? std::to_string(editor.step) : "UNKNOWN";
}
void Prepare(float size, std::string_view text) {
    if (!text.empty()) {
        ImGui::GetFont()->CalcTextSizeA(size, std::numeric_limits<float>::max(), 0, text.data(), text.data() + text.size());
    }
}
}

void PrepareWidthEditorText(const MenuSnapshot& snapshot, const layout::Hud& hud) {
    if (!snapshot.widthEditor) { return; }
    const auto& editor = *snapshot.widthEditor;
    const float font = EditorFont(editor, hud);
    for (const auto& instruction : Hints(editor)) { Prepare(font, BoundedPanelText(instruction, 256)); }
    Prepare(hud.rowFont, "Editor controls unavailable");
    Prepare(hud.rowFont, "Offset Value: "); Prepare(hud.rowFont, Offset(editor));
}

void DrawWidthEditorPanel(ImDrawList& draw, const MenuSnapshot& snapshot, const layout::Hud& hud) {
    if (!snapshot.widthEditor) { return; }
    const auto& editor = *snapshot.widthEditor;
    const auto instructions = Hints(editor);
    const float font = EditorFont(editor, hud);
    const ImVec2 first{hud.body.x + 4 * hud.scale, hud.body.y + 25 * hud.scale};
    const auto viewport = ImGui::GetIO().DisplaySize;
    const auto visible = VisiblePanelRect(hud.body, viewport.x, viewport.y);
    if (visible.width <= 0 || visible.height <= 0) return;
    draw.PushClipRect({visible.x, visible.y}, {visible.Right(), visible.Bottom()}, true);
    for (std::size_t index = 0; index < instructions.size(); ++index) {
        const auto line = BoundedPanelText(instructions[index], 256);
        draw.AddText(ImGui::GetFont(), font, {first.x, first.y + 14 * hud.scale * index},
            IM_COL32_WHITE, line.data(), line.data() + line.size());
    }
    if (instructions.empty()) {
        // No guessed key is advertised when private editor controls are not available
        draw.AddText(ImGui::GetFont(), hud.rowFont, first, IM_COL32_WHITE, "Editor controls unavailable");
    }
    // CorrectNL_BGHeight returns 3 plus fourteen units for each of the five source lines
    const ImVec2 offset{first.x, first.y + 73 * hud.scale};
    constexpr std::string_view label = "Offset Value: ";
    const auto measured = ImGui::GetFont()->CalcTextSizeA(hud.rowFont, 10000, 0, label.data(), label.data() + label.size());
    draw.AddText(ImGui::GetFont(), hud.rowFont, offset, IM_COL32_WHITE, label.data(), label.data() + label.size());
    const auto value = Offset(editor);
    draw.AddText(ImGui::GetFont(), hud.rowFont, {offset.x + measured.x - 7 * hud.scale, offset.y},
        IM_COL32(0, 255, 0, 255), value.c_str());
    draw.PopClipRect();
}
}
