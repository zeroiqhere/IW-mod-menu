#include "MenuPositionEditorPanel.h"
#include "PanelText.h"
#include "PanelGeometry.h"
#include "layout/MenuPositionEditorLayout.h"
#include <algorithm>
#include <limits>
#include <span>

namespace iw::overlay {
namespace {
std::span<const std::string> Hints(const MenuPositionEditor& editor) {
    // The source has seven actions, so malformed local vectors cannot add rendering work
    return {editor.instructions.data(), std::min<std::size_t>(editor.instructions.size(), 7)};
}
float EditorFont(const MenuPositionEditor& editor, const layout::Hud& hud) {
    // Whole-pixel baked fonts keep very long saved labels from rounding down to zero
    float font = std::max(1.0f, hud.rowFont);
    const float available = std::max(hud.scale, hud.body.width - 8 * hud.scale);
    for (unsigned attempt = 0; attempt < 8; ++attempt) {
        float longest = 0;
        for (const auto& hint : Hints(editor)) {
            const auto line = BoundedPanelText(hint, 256);
            longest = std::max(longest, ImGui::GetFont()->CalcTextSizeA(font,
                std::numeric_limits<float>::max(), 0, line.data(), line.data() + line.size()).x);
        }
        if (longest <= available) break;
        // Confirm each fitted size because a different baked font changes measured glyph widths
        const float fitted = std::max(1.0f, font * available / longest);
        if (fitted >= font) break;
        font = fitted;
    }
    return font;
}
std::string Offset(const MenuPositionEditor& editor) {
    return editor.step >= 1 && editor.step <= 10 ? std::to_string(editor.step) : "UNKNOWN";
}
void Prepare(float size, std::string_view text) {
    if (!text.empty()) ImGui::GetFont()->CalcTextSizeA(size, std::numeric_limits<float>::max(), 0,
        text.data(), text.data() + text.size());
}
}
void PrepareMenuPositionEditorText(const MenuSnapshot& snapshot, const layout::Hud& hud) {
    if (!snapshot.menuPositionEditor) return;
    const auto& editor = *snapshot.menuPositionEditor;
    const auto font = EditorFont(editor, hud);
    for (const auto& hint : Hints(editor)) Prepare(font, BoundedPanelText(hint, 256));
    Prepare(hud.rowFont, "Editor controls unavailable");
    Prepare(hud.rowFont, "Offset Value: "); Prepare(hud.rowFont, Offset(editor));
}
void DrawMenuPositionEditorPanel(ImDrawList& draw, const MenuSnapshot& snapshot, const layout::Hud& hud) {
    if (!snapshot.menuPositionEditor) return;
    const auto& editor = *snapshot.menuPositionEditor;
    const float font = EditorFont(editor, hud);
    const auto textLayout = layout::MakeMenuPositionTextLayout(hud);
    const float x = textLayout.first.x;
    bool hasControls = false;
    const auto viewport = ImGui::GetIO().DisplaySize;
    const auto visible = VisiblePanelRect(hud.body, viewport.x, viewport.y);
    if (visible.width <= 0 || visible.height <= 0) return;
    draw.PushClipRect({visible.x, visible.y}, {visible.Right(), visible.Bottom()}, true);
    const auto hints = Hints(editor);
    for (std::size_t index = 0; index < hints.size(); ++index) {
        const auto line = BoundedPanelText(hints[index], 256);
        if (line.empty()) continue;
        hasControls = true;
        // Source HUD anchors map to native line centers without enlarging the 122-unit body
        const float y = textLayout.first.y + textLayout.lineAdvance * index - font / 2;
        draw.AddText(ImGui::GetFont(), font, {x, y}, IM_COL32_WHITE, line.data(), line.data() + line.size());
    }
    if (!hasControls) {
        draw.AddText(ImGui::GetFont(), hud.rowFont, {x, textLayout.first.y - hud.rowFont / 2},
            IM_COL32_WHITE, "Editor controls unavailable");
    }
    // CorrectNL_BGHeight is 101 units for seven source lines, and the source subtracts ten
    const float y = textLayout.offset.y - hud.rowFont / 2;
    constexpr std::string_view label = "Offset Value: ";
    const auto labelWidth = ImGui::GetFont()->CalcTextSizeA(hud.rowFont, 10000, 0,
        label.data(), label.data() + label.size()).x;
    draw.AddText(ImGui::GetFont(), hud.rowFont, {x, y}, IM_COL32_WHITE, label.data(), label.data() + label.size());
    const auto value = Offset(editor);
    draw.AddText(ImGui::GetFont(), hud.rowFont, {x + labelWidth - 7 * hud.scale, y}, IM_COL32(0, 255, 0, 255), value.c_str());
    draw.PopClipRect();
}
}
