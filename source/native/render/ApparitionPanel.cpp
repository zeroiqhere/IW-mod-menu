#include "instructions/ClosedInstructionPanel.h"
#include "ApparitionPanel.h"
#include "PanelText.h"
#include "PanelGeometry.h"
#include "credits/CreditsPanel.h"
#include "WidthEditorPanel.h"
#include "MenuPositionEditorPanel.h"
#include "InstructionPanel.h"
#include "InstructionGeometry.h"
#include "ScrollRows.h"
#include "designs/DesignChrome.h"
#include "designs/DesignText.h"
#include "layout/ApparitionLayout.h"
#include <algorithm>
#include <cmath>
#include <limits>
#include <string_view>
#include <imgui.h>

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

}

bool DrawApparitionPanel(const MenuSnapshot& snapshot, PanelDrawSummary* summary, std::optional<float> presentationWidth,
    std::optional<layout::Color> presentationColor, const ScrollFrame* presentationRows,
    std::optional<double> presentationCreditsElapsed, std::optional<layout::Color> basicScrollerColor, bool instructionHudAdmitted, std::optional<InstructionHudColors> instructionColors, const ChromeColorFrame* chromeColors) {

    if (summary) { *summary = {}; }
    if (!snapshot.isOpen) { return DrawClosedInstructionPanel(snapshot, presentationColor, instructionHudAdmitted, instructionColors); }
    const bool rebuilding = snapshot.menuUnlock == MenuUnlockPhase::Rebuild
        || snapshot.menuDesignTransition == MenuDesignTransitionPhase::Rebuild
        || snapshot.menuOpenTransition == MenuOpenTransitionPhase::Rebuild
        || snapshot.menuPreferenceRebuild == MenuPreferenceRebuildPhase::Rebuild;
    const bool drawChrome = chromeColors ? chromeColors->admitted : !rebuilding;
    const auto instructionGeometry = ResolveInstructionGeometry(snapshot);
    const auto instructions = instructionHudAdmitted ? MakeInstructionContent(snapshot) : InstructionPanelContent{};
    const bool menuCredits = snapshot.menuCredits.has_value();
    const bool menuPositionEditor = snapshot.menuPositionEditor.has_value();
    const bool positionEditor = snapshot.instructionPositionEditor.has_value();
    const bool bypassAdaptive = positionEditor
        && snapshot.instructionPositionEditor->phase != InstructionPositionEditorPhase::Entering;
    const auto viewport = ImGui::GetIO().DisplaySize;
    auto hud = layout::MakeHud({viewport.x, viewport.y, snapshot.rows.size(), snapshot.selectedIndex,
        260, instructions.LineCount(), !snapshot.context.empty() || !snapshot.notice.empty(), 5, snapshot.appearance, snapshot.widthEditor.has_value(), presentationWidth, positionEditor, bypassAdaptive, menuPositionEditor, menuCredits, instructionGeometry.presence, instructionGeometry.design});
    if (!hud.visible) { return false; }
    const float instructionWidth = MeasureInstructionWidth(instructions, hud);
    hud = layout::MakeHud({viewport.x, viewport.y, snapshot.rows.size(), snapshot.selectedIndex,
        instructionWidth, instructions.LineCount(), !snapshot.context.empty() || !snapshot.notice.empty(), 5, snapshot.appearance, snapshot.widthEditor.has_value(), presentationWidth, positionEditor, bypassAdaptive, menuPositionEditor, menuCredits, instructionGeometry.presence, instructionGeometry.design});
    const auto message = Bounded(snapshot.notice, 6000);
    const auto messageSize = ImGui::GetFont()->CalcTextSizeA(hud.rowFont, 10000,
        std::max(hud.scale, hud.notice.width - 8 * hud.scale), message.data(), message.data() + message.size());
    const auto noticeLines = static_cast<std::size_t>(std::ceil(messageSize.y / hud.rowFont));
    hud = layout::MakeHud({viewport.x, viewport.y, snapshot.rows.size(), snapshot.selectedIndex,
        instructionWidth, instructions.LineCount(), !snapshot.context.empty() || !snapshot.notice.empty(), noticeLines, snapshot.appearance, snapshot.widthEditor.has_value(), presentationWidth, positionEditor, bypassAdaptive, menuPositionEditor, menuCredits, instructionGeometry.presence, instructionGeometry.design});
    const auto scrollRequest = MakeScrollRequest(snapshot, viewport.x, viewport.y, presentationWidth);
    const auto fallbackRows = StaticScrollFrame(scrollRequest);
    // Refresh rows must use the declared current styles, not an earlier cursor transition
    const auto& rowFrame = presentationRows && (!snapshot.menuPreferenceRefresh || scrollRequest.opening) && !snapshot.menuUnlock
        && (!snapshot.menuDesignTransition || scrollRequest.opening)
        && (!snapshot.menuPreferenceRebuild || scrollRequest.opening)
        && (!snapshot.menuOpenTransition || scrollRequest.opening) && hud.count && !rebuilding && MatchesScrollFrame(*presentationRows, hud, scrollRequest)
        ? *presentationRows : fallbackRows;
    if (hud.hasSelection) { hud.scroller.y = hud.body.y + (hud.scrollerTopOffset + 18 * rowFrame.scroller) * hud.scale; }
    auto counterHud = hud;
    // Only matched receipt-phase evidence may paint an earlier counter; action selection stays current
    if (rowFrame.counterSelection && spacer::MatchesReceiptFrame(rowFrame, scrollRequest))
        counterHud.selected = *rowFrame.counterSelection;
    if (summary) { summary->firstRow = rebuilding ? 0 : hud.first; summary->shownRows = rebuilding ? 0 : hud.shown; }
    double creditsElapsed = menuCredits ? snapshot.menuCredits->elapsedMilliseconds : 0;
    if (menuCredits && presentationCreditsElapsed && std::isfinite(*presentationCreditsElapsed)
        && *presentationCreditsElapsed >= creditsElapsed
        && *presentationCreditsElapsed <= std::min(creditsElapsed + 100, 39999.0)) {
        // Only the bounded local lead can replace reported elapsed, never the backend's exit decision
        creditsElapsed = *presentationCreditsElapsed;
    }
    const auto creditsFrame = menuCredits ? credits::Sample(creditsElapsed) : credits::Frame{};
    const float instructionFont = FitInstructionText(instructions, hud);
    // Load every required glyph before emitting triangles so late atlas growth cannot split a panel
    auto prepare = [](float size, std::string_view text) {
        if (!text.empty()) { ImGui::GetFont()->CalcTextSizeA(size, 10000, 0, text.data(), text.data() + text.size()); }
    };
    if (drawChrome) { PrepareDesignText(snapshot, counterHud); }
    prepare(hud.rowFont, Bounded(snapshot.context, 256));
    prepare(hud.rowFont, "No Options Found?... (continued)");
    if (!rebuilding) { PrepareScrollRows(snapshot, hud, rowFrame); }
    if (!rebuilding && !positionEditor && !menuPositionEditor && !menuCredits) { PrepareWidthEditorText(snapshot, hud); }
    if (!rebuilding && !menuCredits) { PrepareMenuPositionEditorText(snapshot, hud); }
    if (!rebuilding && menuCredits) { credits::PreparePanel(creditsFrame, hud); }
    PrepareInstructionText(instructions, hud, instructionFont);
    if (hud.counterVisible) { prepare(hud.rowFont, "0123456789/"); }
    auto& draw = *ImGui::GetForegroundDrawList();
    // Presentation samples never replace the producer's retained fixed accent
    const auto theme = snapshot.smoothRainbowTheme && presentationColor
        ? *presentationColor : layout::Theme(snapshot.accent);
    const ImU32 accent = Color(theme);
    // Only the IW status header uses a contrast aid; source HUD colors remain literal
    const bool darkStatus = (299 * theme.red + 587 * theme.green + 114 * theme.blue) < 70000;
    const ImU32 statusText = darkStatus ? IM_COL32_WHITE : accent;
    const Rect viewportClip{0, 0, viewport.x, viewport.y};
    draw.PushClipRect(Top(viewportClip), Bottom(viewportClip), true);
    // Owned chrome presence is separate from the ordinary row rebuild pause
    if (drawChrome) {
        DrawDesignChrome(draw, hud, theme, snapshot.smoothRainbowTheme, basicScrollerColor, chromeColors);
        if (rebuilding) DrawDesignTitles(draw, snapshot, counterHud, theme, chromeColors);
    }
    if (!rebuilding && (!chromeColors || chromeColors->admitted)) {
        if (snapshot.widthEditor && !positionEditor && !menuPositionEditor && !menuCredits) {
            DrawWidthEditorPanel(draw, snapshot, hud);
        } else {
            DrawDesignTitles(draw, snapshot, counterHud, theme, chromeColors);
            if (!hud.count && !positionEditor && !menuPositionEditor && !menuCredits) {
                auto center = hud.OptionCenter(0, false);
                Text(draw, hud.body, {center.x, center.y - hud.rowFont / 2}, hud.rowFont, IM_COL32_WHITE, "No Options Found");
            }
            if (menuCredits) { credits::DrawPanel(draw, creditsFrame, hud); }
            else if (menuPositionEditor) { DrawMenuPositionEditorPanel(draw, snapshot, hud); }
            else { DrawScrollRows(draw, snapshot, hud, rowFrame, theme); }
            DrawDesignCounter(draw, counterHud);
        }
    }
    DrawInstructionPanel(draw, instructions, InstructionPaintHud(hud, snapshot), theme, instructionFont, snapshot.smoothRainbowTheme, layout::Theme(snapshot.accent), instructionColors);
    if (hud.notice.width > 0) {
        draw.AddRectFilled({hud.notice.x - hud.scale, hud.notice.y - hud.scale},
            {hud.notice.Right() + hud.scale, hud.notice.Bottom() + hud.scale}, accent);
        draw.AddRectFilled(Top(hud.notice), Bottom(hud.notice), Color(layout::Instructions));
        Text(draw, hud.notice, {hud.notice.x + 4 * hud.scale, hud.notice.y + 3 * hud.scale}, hud.rowFont,
            statusText, Bounded(snapshot.context, 256));
        Rect detail{hud.notice.x + 4 * hud.scale, hud.notice.y + 20 * hud.scale,
            hud.notice.width - 8 * hud.scale, hud.notice.height - 24 * hud.scale};
        const bool continues = messageSize.y > detail.height || message.size() < snapshot.notice.size();
        if (summary) { summary->noticeContinues = continues; }
        // Whole visible lines and an explicit marker keep long errors from silently disappearing
        detail.height = std::max(0.0f, std::floor((detail.height - (continues ? hud.rowFont : 0)) / hud.rowFont) * hud.rowFont);
        Text(draw, detail, Top(detail), hud.rowFont, IM_COL32_WHITE, message, detail.width);
        if (continues) {
            Text(draw, hud.notice, {detail.x, hud.notice.Bottom() - hud.rowFont - 3 * hud.scale},
                hud.rowFont, statusText, "... (continued)");
        }
    }
    draw.PopClipRect();
    return true;
}
}
