#include "DesignText.h"
#include "../PanelGeometry.h"
#include "../PanelText.h"
#include <algorithm>
#include <limits>
#include <string>
#include <imgui.h>

namespace iw::overlay {
namespace {
float Width(float font, std::string_view text) {
    return ImGui::GetFont()->CalcTextSizeA(font, std::numeric_limits<float>::max(), 0, text.data(), text.data() + text.size()).x;
}
float CounterX(const layout::Hud& hud) {
    return std::min(hud.counter.x, hud.body.Right() - 4 * hud.scale - Width(hud.rowFont, std::to_string(hud.count)));
}
layout::Rect TitleClip(const layout::Hud& hud) {
    auto clip = hud.titleClip;
    if (hud.counterVisible && hud.design != MenuDesign::Apparition) {
        // Native substitute fonts reserve real counter ink before fitting a long header
        const float end = CounterX(hud) - 7 * hud.scale - Width(hud.rowFont, std::to_string(hud.selected + 1));
        clip.width = std::max(0.0f, end - clip.x);
    }
    return clip;
}
std::string Ellipsis(float font, std::string_view text, float available) {
    if (Width(font, text) <= available) return std::string(text);
    // Keep the mapped source font size and remove complete UTF-8 characters instead
    if (Width(font, "...") > available) return {};
    std::size_t low = 0, high = text.size();
    // Width is monotonic across complete prefixes, so bounded binary search avoids repeated full scans
    while (low < high) {
        const auto middle = low + (high - low + 1) / 2;
        const auto prefix = BoundedPanelText(text, middle);
        if (Width(font, std::string(prefix) + "...") <= available) low = middle;
        else high = middle - 1;
    }
    auto result = std::string(BoundedPanelText(text, low)) + "...";
    // Warm the final font and glyph set before any draw triangle is emitted
    Width(font, result); return result;
}
std::string_view Secondary(MenuDesign design) {
    return design == MenuDesign::Native ? "Apparition" : "Status: local IW menu";
}
void Text(ImDrawList& draw, layout::Rect clip, layout::Point center, float font, ImU32 color,
    std::string_view text, bool centered) {
    const auto viewport = ImGui::GetIO().DisplaySize;
    const auto visible = VisiblePanelRect(clip, viewport.x, viewport.y);
    if (visible.width <= 0 || visible.height <= 0 || text.empty()) return;
    draw.PushClipRect({visible.x, visible.y}, {visible.Right(), visible.Bottom()}, true);
    draw.AddText(ImGui::GetFont(), font, {center.x - (centered ? Width(font, text) / 2 : 0), center.y - font / 2},
        color, text.data(), text.data() + text.size()); draw.PopClipRect();
}
}
FittedDesignTitle FitDesignTitle(const MenuSnapshot& snapshot, const layout::Hud& hud) {
    const auto text = BoundedPanelText(snapshot.title.empty() ? std::string_view("Apparition") : snapshot.title, 256);
    const auto clip = TitleClip(hud);
    const float available = std::max(0.0f, clip.width - (hud.titleCentered ? 4 * hud.scale : hud.title.x - clip.x + hud.scale));
    const float font = hud.titleFont;
    // Apparition retains its established clipping and pixels; other designs reserve header counters
    auto fitted = hud.design == MenuDesign::Apparition ? std::string(text) : Ellipsis(font, text, available);
    return {fitted, font, Width(font, fitted), available};
}
void PrepareDesignText(const MenuSnapshot& snapshot, const layout::Hud& hud) {
    if (hud.titleVisible) FitDesignTitle(snapshot, hud);
    if (hud.secondaryVisible) {
        const auto text = Ellipsis(hud.secondaryFont, Secondary(hud.design), hud.secondaryClip.width - 4 * hud.scale);
        Width(hud.secondaryFont, text);
    }
    // Warm every counter glyph before the first triangle is appended
    if (hud.counterVisible) Width(hud.rowFont, "0123456789/");
}
void DrawDesignTitles(ImDrawList& draw, const MenuSnapshot& snapshot, const layout::Hud& hud, layout::Color accentText, const ChromeColorFrame* ownedColors) {
    if (hud.titleVisible && (!ownedColors || (ownedColors->admitted && (ownedColors->currentRoleMask & ChromeRoleBit(ChromeRole::Title))))) {
        const auto fitted = FitDesignTitle(snapshot, hud);
        const bool white = hud.design == MenuDesign::Classic || hud.design == MenuDesign::Native || hud.design == MenuDesign::Aio;
        const auto titleRgb = ownedColors ? ownedColors->Color(ChromeRole::Title) : accentText;
        // RGB history never replaces the source title alpha or an unrelated secondary label
        const ImU32 color = ownedColors ? IM_COL32(titleRgb.red, titleRgb.green, titleRgb.blue, hud.titleAlpha) : white ? IM_COL32(255, 255, 255, hud.titleAlpha)
            : IM_COL32(accentText.red, accentText.green, accentText.blue, accentText.alpha);
        Text(draw, TitleClip(hud), hud.title, fitted.font, color, fitted.text, hud.titleCentered);
    }
    if (hud.secondaryVisible && (!ownedColors || (ownedColors->admitted && (ownedColors->currentRoleMask & ChromeRoleBit(ChromeRole::Title))))) {
        const auto text = Ellipsis(hud.secondaryFont, Secondary(hud.design), hud.secondaryClip.width - 4 * hud.scale);
        Text(draw, hud.secondaryClip, hud.secondaryTitle, hud.secondaryFont, IM_COL32_WHITE, text, hud.design == MenuDesign::Native);
    }
}
void DrawDesignCounter(ImDrawList& draw, const layout::Hud& hud) {
    if (!hud.counterVisible) return;
    const auto total = std::to_string(hud.count), current = std::to_string(hud.count ? hud.selected + 1 : 0);
    const float x = CounterX(hud); const auto color = IM_COL32(255, 255, 255, hud.counterAlpha);
    const auto clip = hud.design == MenuDesign::Apparition ? hud.banner : hud.titleClip;
    Text(draw, clip, {x - 3 * hud.scale - Width(hud.rowFont, current), hud.counter.y}, hud.rowFont, color, current, false);
    Text(draw, clip, {x - 2 * hud.scale, hud.counter.y}, hud.rowFont, color, "/", false);
    Text(draw, clip, {x + 3 * hud.scale, hud.counter.y}, hud.rowFont, color, total, false);
}
}
