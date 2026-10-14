#include "../shared/paint/RowDisplayValue.h"
#include "ScrollRows.h"
#include "../runtime/scroll/SpacerScrollPlan.h"
#include "InstructionGeometry.h"
#include "../shared/MenuRowCapacity.h"
#include "PanelText.h"
#include "PanelGeometry.h"
#include "rows/RowTextLayout.h"
#include "rows/RowLabelFit.h"
#include "rows/RowFont.h"
#include <algorithm>
#include <cmath>

namespace iw::overlay {
namespace {
using layout::Rect;
ImU32 Color(layout::Color color, float opacity = 1) {
    return IM_COL32(color.red, color.green, color.blue, static_cast<unsigned>(color.alpha * opacity));
}
ImVec2 Top(const Rect& rect) { return {rect.x, rect.y}; }
ImVec2 Bottom(const Rect& rect) { return {rect.Right(), rect.Bottom()}; }
std::string_view Bounded(std::string_view text, std::size_t limit) {
    return BoundedPanelText(text, limit);
}
void Text(ImDrawList& draw, const Rect& clip, ImVec2 point, float size, ImU32 color,
        std::string_view text, float wrap = 0) {
    auto value = Bounded(text, 6000);
    if (value.empty()) { return; }
    const auto viewport = ImGui::GetIO().DisplaySize;
    const auto visible = VisiblePanelRect(clip, viewport.x, viewport.y);
    if (visible.width <= 0 || visible.height <= 0) return;
    draw.PushClipRect(Top(visible), Bottom(visible), true);
    draw.AddText(ImGui::GetFont(), size, point, color, value.data(), value.data() + value.size(), wrap);
    draw.PopClipRect();
}
void RightText(ImDrawList& draw, const Rect& clip, float right, float centerY, float size,
        ImU32 color, std::string_view text) {
    auto value = Bounded(text, 256);
    const ImVec2 measured = ImGui::GetFont()->CalcTextSizeA(size, 10000, 0, value.data(), value.data() + value.size());
    Text(draw, clip, {right - measured.x, centerY - size / 2}, size, color, value);
}
float Opacity(const MenuAppearance& appearance, const ScrollRow& row) {
    return row.opacity.value_or((appearance.spotlightCursor ? 0.4f + 0.6f * row.selection : 1) * row.visibility);
}
float FontSize(const MenuAppearance& appearance, const layout::Hud& hud, const ScrollRow& row) {
    return rows::RowFontPixels(hud.rowFont, appearance.largeCursor, row.selection);
}
rows::RowTextLayout RowInk(std::string_view value, const MenuAppearance& appearance, const layout::Hud& hud, float font) {
    const bool boolean = value == "ON" || value == "OFF" || value == "UNKNOWN";
    const bool boxed = boolean && appearance.booleanDisplay == "Boxes";
    const bool colorOnly = boolean && appearance.booleanDisplay == "Text Color";
    auto placement = rows::ValuePlacement::None;
    float valueWidth = 0;
    if (boxed) placement = hud.boxesLeft ? rows::ValuePlacement::BoxLeft : rows::ValuePlacement::BoxRight;
    else if (!value.empty() && (!colorOnly || value == "UNKNOWN")) {
        placement = rows::ValuePlacement::Text;
        const auto text = Bounded(value, 256);
        valueWidth = ImGui::GetFont()->CalcTextSizeA(font, 10000, 0, text.data(), text.data() + text.size()).x;
    }
    return rows::MakeRowTextLayout({hud.body.x, hud.body.width, hud.scale, valueWidth, placement});
}
rows::FittedRowLabel Label(const MenuRow& row, const rows::RowTextLayout& ink, float font) {
    return rows::FitRowLabel(row.label, ink.labelRight - ink.labelLeft, [&](std::string_view text) {
        return ImGui::GetFont()->CalcTextSizeA(font, 10000, 0, text.data(), text.data() + text.size()).x;
    });
}
ImU32 SelectionColor(ImU32 accent, float weight, unsigned alpha) {
    // Blend selection against the current accent, including a currently sampled rainbow hue
    const auto channel = [&](unsigned shift) {
        const auto selected = static_cast<float>((accent >> shift) & 255);
        return static_cast<unsigned>(std::lround(255 + (selected - 255) * weight));
    };
    return IM_COL32(channel(IM_COL32_R_SHIFT), channel(IM_COL32_G_SHIFT), channel(IM_COL32_B_SHIFT), alpha);
}
}
ScrollRequest MakeScrollRequest(const MenuSnapshot& snapshot, float width, float height,
    std::optional<float> presentationWidth) noexcept {
    const bool editor = snapshot.instructionPositionEditor.has_value();
    const auto instructionGeometry = ResolveInstructionGeometry(snapshot);
    const auto hud = layout::MakeHud({width, height, snapshot.rows.size(), snapshot.selectedIndex,
        260, 3, false, 0, snapshot.appearance, snapshot.widthEditor.has_value(), presentationWidth, editor, false, snapshot.menuPositionEditor.has_value(), snapshot.menuCredits.has_value(), instructionGeometry.presence, instructionGeometry.design});
    ScrollRequest request;
    // Paused preference rows remain visible without carrying a previous cursor path
    request.active = snapshot.isOpen && hud.visible && !snapshot.widthEditor && !editor
        && !snapshot.menuPositionEditor && !snapshot.menuCredits && !snapshot.menuUnlock && !snapshot.menuOptionCallback && !snapshot.menuPreferenceRefresh && !snapshot.menuDesignTransition && !snapshot.menuOpenTransition && !snapshot.menuPreferenceRebuild;
    request.menuRevision = snapshot.menuRevision; request.focusRevision = snapshot.presentationFocusRevision;
    request.rowCount = hud.count; request.first = hud.first; request.shown = hud.count ? hud.shown : 0; request.selected = hud.selected;
    request.durationMilliseconds = snapshot.appearance.scrollAnimationMilliseconds;
    const auto accent = layout::Theme(snapshot.accent);
    request.layout = {width, height, hud.body.width, hud.body.height, hud.capacity,
        snapshot.appearance.booleanDisplay == "Boxes" ? 0u : snapshot.appearance.booleanDisplay == "Text" ? 1u : 2u,
        static_cast<std::uint32_t>((accent.red << 16) | (accent.green << 8) | accent.blue),
        hud.boxesLeft, snapshot.appearance.coloredCursor, snapshot.appearance.spotlightCursor,
        snapshot.appearance.largeCursor, snapshot.smoothRainbowTheme};
    if (snapshot.menuOpening && snapshot.isOpen && !snapshot.widthEditor && !editor
        && !snapshot.menuPositionEditor && !snapshot.menuCredits && !snapshot.menuUnlock && snapshot.menuPreferenceRefresh != MenuPreferenceRefreshPhase::Cooldown
        && snapshot.menuDesignTransition != MenuDesignTransitionPhase::Rebuild
        && snapshot.menuOpenTransition != MenuOpenTransitionPhase::Rebuild
        && snapshot.menuPreferenceRebuild != MenuPreferenceRebuildPhase::Rebuild
        && (!snapshot.menuOptionCallback || CallbackKeepsOpening(*snapshot.menuOptionCallback))) request.opening = snapshot.menuOpening;
    request.layout.design = hud.design;
    request.layout.bodyX = hud.body.x; request.layout.bodyY = hud.body.y;
    const auto position = snapshot.appearance.menuPosition.value_or(DefaultMenuPosition());
    request.layout.menuX = position.x; request.layout.menuY = position.y;
    request.navigationRevision = snapshot.navigationRevision;
    request.localMenuEligibility = snapshot.localMenuEligibility; request.menuSurface = snapshot.menuSurface;
    request.menuOptionCallback = snapshot.menuOptionCallback;
    request.menuBooleanPaint = snapshot.menuBooleanPaint;
    request.openingPolicyIdentity = CaptureOpeningPolicyIdentity(snapshot.mainOpeningControls, snapshot.openingPolicies);
    if (const auto topology = runtime::scroll::CaptureTopology(snapshot)) request.topology = *topology;
    if (snapshot.spacerScrollReceipt) {
        const auto& receipt = *snapshot.spacerScrollReceipt;
        // Captured visual preferences must still describe this exact private target report
        if (receipt.sourceAppearance == snapshot.appearance && receipt.sourceAccent == snapshot.accent
            && receipt.sourceRainbow == snapshot.smoothRainbowTheme && receipt.sourceRootMenu == snapshot.isRootMenu
            && receipt.sourceOpeningControls == snapshot.mainOpeningControls
            && receipt.sourceOpeningPolicies == snapshot.openingPolicies
            && receipt.sourceLocalMenuEligibility == snapshot.localMenuEligibility
            && receipt.sourceMenuSurface == snapshot.menuSurface
            && receipt.sourceMenuOptionCallback == snapshot.menuOptionCallback
            && receipt.sourceMenuBooleanPaint == snapshot.menuBooleanPaint
            && IsValidOpeningPolicyOwnership(receipt.sourceOpeningControls, receipt.sourceOpeningPolicies))
            request.spacerReceipt = &receipt;
    }
    return request;
}
bool MatchesScrollFrame(const ScrollFrame& frame, const layout::Hud& hud, const ScrollRequest& request) noexcept {
    if ((request.menuBooleanPaint && (!request.menuRevision || !IsValidMenuBooleanPaint(*request.menuBooleanPaint, request.rowCount)))
        || frame.menuBooleanPaint != request.menuBooleanPaint
        || (request.menuOptionCallback && (!IsValidMenuOptionCallback(*request.menuOptionCallback)
            || request.active || (request.opening && !CallbackKeepsOpening(*request.menuOptionCallback))))
        || frame.menuOptionCallback != request.menuOptionCallback
        || !IsKnownMenuSurface(request.menuSurface)
        || (request.localMenuEligibility && !IsValidLocalMenuEligibility(*request.localMenuEligibility))
        || frame.localMenuEligibility != request.localMenuEligibility || frame.menuSurface != request.menuSurface
        || frame.layout != request.layout || frame.menuRevision != request.menuRevision || frame.focusRevision != request.focusRevision
        || frame.opening != request.opening) return false;
    if (request.opening) {
        if (!request.opening->revision || request.shown < 2 || request.shown > MaximumMenuRowsShown
            || request.opening->elapsedMilliseconds < 0
            || request.opening->elapsedMilliseconds >= static_cast<int>((request.shown - 1) * 100)
            || !frame.openingElapsed || !std::isfinite(*frame.openingElapsed)
            || *frame.openingElapsed < request.opening->elapsedMilliseconds
            || *frame.openingElapsed > std::min(request.opening->elapsedMilliseconds + 100.0,
                static_cast<double>((request.shown - 1) * 100 - 1))) return false;
        // A caller cannot introduce local interpolation when private identity is absent
        if ((!request.menuRevision || !request.focusRevision)
            && *frame.openingElapsed != request.opening->elapsedMilliseconds) return false;
    } else if (frame.openingElapsed) return false;
    if (frame.design != hud.design || !spacer::MatchesReceiptFrame(frame, request)) return false;
    if (hud.shown == 0) { return frame.count == 0 && frame.rowCount == 0 && frame.shown == 0 && frame.scroller == 0; }
    if (frame.count < hud.shown || frame.count > MaximumScrollRows || frame.rowCount != hud.count
        || frame.first != hud.first || frame.shown != hud.shown || frame.selected != hud.selected
        || !std::isfinite(frame.scroller) || frame.scroller < 0 || frame.scroller > hud.shown - 1) return false;
    for (std::size_t row = 0; row < frame.count; ++row) {
        const auto& item = frame.rows[row];
        if (item.index >= hud.count || !std::isfinite(item.position) || item.position < 0 || item.position > hud.shown - 1
            || !std::isfinite(item.selection) || item.selection < 0 || item.selection > 1
            || !std::isfinite(item.visibility) || item.visibility < 0 || item.visibility > 1) return false;
        for (const auto alpha : {item.opacity, item.decorationVisibility})
            if (alpha && (!std::isfinite(*alpha) || *alpha < 0 || *alpha > 1)) return false;
        if (request.opening) {
            if (!item.opacity || !item.decorationVisibility || item.outgoing || item.index != hud.first + row
                || item.position != static_cast<float>(row) || item.selection != (item.index == hud.selected ? 1 : 0)) return false;
            const float progress = row ? static_cast<float>(std::clamp(*frame.openingElapsed / (row * 100.0), 0.0, 1.0)) : 1;
            const float target = request.layout.spotlightCursor ? .4f + .6f * item.selection : 1;
            if (std::abs(*item.opacity - target * progress) > .00001f
                || std::abs(*item.decorationVisibility - progress) > .00001f) return false;
        }
        for (std::size_t other = 0; other < row; ++other) if (frame.rows[other].index == item.index) return false;
    }
    return true;
}
void PrepareScrollRows(const MenuSnapshot& snapshot, const layout::Hud& hud, const ScrollFrame& frame) {
    // Keep the established glyph-loading order so already fitting rows retain their exact pixels
    for (std::size_t displayed = 0; displayed < frame.count; ++displayed) {
        const auto& item = frame.rows[displayed];
        const auto& row = snapshot.rows[item.index];
        const auto size = FontSize(snapshot.appearance, hud, item);
        for (auto value : {Bounded(row.label, 512), Bounded(ResolveRowDisplayValue(snapshot, item.index), 256)}) {
            if (!value.empty()) { ImGui::GetFont()->CalcTextSizeA(size, 10000, 0, value.data(), value.data() + value.size()); }
        }
    }
    // Any added ellipsis glyph is also ready before panel triangles are appended
    for (std::size_t displayed = 0; displayed < frame.count; ++displayed) {
        const auto& item = frame.rows[displayed];
        const auto& row = snapshot.rows[item.index];
        const auto size = FontSize(snapshot.appearance, hud, item);
        const auto ink = RowInk(ResolveRowDisplayValue(snapshot, item.index), snapshot.appearance, hud, size);
        if (!ink.valid) continue;
        const auto label = Label(row, ink, size);
        if (!label.Text().empty()) {
            ImGui::GetFont()->CalcTextSizeA(size, 10000, 0, label.Text().data(), label.Text().data() + label.Text().size());
        }
    }
}

void DrawScrollRows(ImDrawList& draw, const MenuSnapshot& snapshot, const layout::Hud& hud,
    const ScrollFrame& frame, layout::Color theme) {
    // Moving rows remain clipped to the option body even when retiring at its edge
    const auto viewport = ImGui::GetIO().DisplaySize;
    const auto visible = VisiblePanelRect(hud.body, viewport.x, viewport.y);
    if (visible.width <= 0 || visible.height <= 0) return;
    draw.PushClipRect(Top(visible), Bottom(visible), true);
    for (std::size_t displayed = 0; displayed < frame.count; ++displayed) {
        const auto& item = frame.rows[displayed];
        const std::size_t index = item.index;
        const auto& row = snapshot.rows[index];
        const auto displayValue = ResolveRowDisplayValue(snapshot, index);
        const float alpha = Opacity(snapshot.appearance, item);
        const unsigned opacity = static_cast<unsigned>(255 * alpha);
        const ImU32 textColor = SelectionColor(Color(theme), snapshot.appearance.coloredCursor && hud.design != MenuDesign::Classic && hud.design != MenuDesign::Native ? item.selection : 0, opacity);
        const bool boolean = displayValue == "ON" || displayValue == "OFF" || displayValue == "UNKNOWN";
        const bool boxed = boolean && snapshot.appearance.booleanDisplay == "Boxes";
        const bool colorOnly = boolean && snapshot.appearance.booleanDisplay == "Text Color";
        const layout::Point center{hud.body.x + (hud.boxesLeft && boxed ? 18 : 4) * hud.scale,
            hud.body.y + (hud.rowCenterOffset + 18 * item.position) * hud.scale};
        const float rowFont = FontSize(snapshot.appearance, hud, item);
        const auto ink = RowInk(ResolveRowDisplayValue(snapshot, item.index), snapshot.appearance, hud, rowFont);
        if (!ink.valid) continue;
        const auto label = Label(row, ink, rowFont);
        Rect labelClip{hud.body.x, hud.body.y + (hud.rowCenterOffset - 8 + 18 * item.position) * hud.scale,
            ink.labelRight - hud.body.x, 18 * hud.scale};
        const ImU32 labelColor = colorOnly && displayValue == "ON" ? IM_COL32(0, 255, 0, opacity) : textColor;
        Text(draw, labelClip, {center.x, center.y - rowFont / 2}, rowFont, labelColor, label.Text());
        if (boxed) {
            const Rect box{hud.boxesLeft ? hud.body.x + 4 * hud.scale : hud.body.Right() - 13 * hud.scale,
                center.y - 5 * hud.scale, 10 * hud.scale, 10 * hud.scale};
            draw.AddRectFilled(Top(box), Bottom(box), IM_COL32(64, 64, 64, static_cast<unsigned>(255 * alpha)));
            Rect inner{box.x + hud.scale, box.y + hud.scale, 8 * hud.scale, 8 * hud.scale};
            draw.AddRectFilled(Top(inner), Bottom(inner), displayValue == "ON" ? Color(theme, alpha) : IM_COL32(0, 0, 0, static_cast<unsigned>(255 * alpha)));
            // The source ON fill has no extra check mark, including a black theme
            // Unknown cannot look like a confirmed off state
            if (displayValue == "UNKNOWN") {
                Text(draw, box, {box.x + hud.scale, center.y - hud.rowFont / 2}, hud.rowFont, IM_COL32(255, 255, 255, opacity), "?");
            }
        } else if (!colorOnly || displayValue == "UNKNOWN") {
            Rect valueClip{ink.valueClipLeft, labelClip.y, hud.body.Right() - ink.valueClipLeft, labelClip.height};
            // UNKNOWN is an IW diagnostic rather than a confirmed source boolean value
            const ImU32 valueColor = displayValue == "UNKNOWN" ? IM_COL32(255, 255, 255, opacity) : textColor;
            RightText(draw, valueClip, hud.body.Right() - 4 * hud.scale, center.y, rowFont, valueColor, displayValue);
        }
        if (row.isSpacer) {
            const float decoration = item.decorationVisibility.value_or(item.visibility);
            draw.AddRectFilled({hud.body.x + 30 * hud.scale, center.y - .5f * hud.scale},
                {hud.body.Right() - 30 * hud.scale, center.y + .5f * hud.scale}, Color(theme, .4f * decoration));
        } else if (!row.enabled) {
            // This IW capability marker is not the reference menu's blank spacer or skip rule
            const float decoration = item.decorationVisibility.value_or(item.visibility);
            draw.AddLine({hud.body.x + 30 * hud.scale, center.y}, {hud.body.Right() - 30 * hud.scale, center.y}, Color(theme, 0.4f * decoration), hud.scale);
        }
    }
    draw.PopClipRect();
}
}
