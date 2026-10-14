#include "CreditsPanel.h"
#include "CreditsCatalog.h"
#include "../PanelGeometry.h"
#include <algorithm>
#include <limits>

namespace iw::overlay::credits {
namespace {
float Width(const TextRuns& text, float font) {
    float width = 0;
    for (std::size_t run = 0; run < text.count; ++run) {
        const auto value = text.runs[run].text;
        width += ImGui::GetFont()->CalcTextSizeA(font, std::numeric_limits<float>::max(), 0,
            value.data(), value.data() + value.size()).x;
    }
    return width;
}
float FitFont(const TextRuns& text, float font, const layout::Hud& hud) {
    const float available = std::max(hud.scale, hud.body.width - 8 * hud.scale);
    for (unsigned attempt = 0; attempt < 8; ++attempt) {
        const auto width = Width(text, font);
        if (width <= available || width <= 0) break;
        // Native substitute fonts are fitted without guessing an objective-font asset
        font *= available / width;
    }
    return font;
}
}
void PreparePanel(const Frame& frame, const layout::Hud& hud) {
    const auto catalog = Catalog();
    for (std::size_t index = 0; index < std::min(frame.count, MaximumLiveLines); ++index) {
        const auto& line = frame.lines[index];
        if (line.index >= catalog.size()) continue;
        const auto text = SplitText(catalog[line.index].text);
        FitFont(text, hud.rowFont * line.fontScale, hud);
    }
}
void DrawPanel(ImDrawList& draw, const Frame& frame, const layout::Hud& hud) {
    const auto viewport = ImGui::GetIO().DisplaySize;
    const auto visible = VisiblePanelRect(hud.body, viewport.x, viewport.y);
    if (visible.width <= 0 || visible.height <= 0) return;
    draw.PushClipRect({visible.x, visible.y}, {visible.Right(), visible.Bottom()}, true);
    const auto catalog = Catalog();
    for (std::size_t index = 0; index < std::min(frame.count, MaximumLiveLines); ++index) {
        const auto& line = frame.lines[index];
        if (line.index >= catalog.size() || line.opacity <= 0) continue;
        const auto text = SplitText(catalog[line.index].text);
        const float font = FitFont(text, hud.rowFont * line.fontScale, hud);
        float x = hud.body.x + (hud.body.width - Width(text, font)) / 2;
        // Source center anchors map to native text tops; the engine font metrics remain unverified
        const float y = hud.body.y + line.center * hud.scale - font / 2;
        const auto opacity = static_cast<unsigned>(255 * line.opacity);
        for (std::size_t run = 0; run < text.count; ++run) {
            const auto& span = text.runs[run];
            // The source red code has an explicit native RGB approximation, separate from the accent
            const auto color = span.red ? IM_COL32(255, 0, 0, opacity) : IM_COL32(255, 255, 255, opacity);
            draw.AddText(ImGui::GetFont(), font, {x, y}, color, span.text.data(), span.text.data() + span.text.size());
            x += ImGui::GetFont()->CalcTextSizeA(font, std::numeric_limits<float>::max(), 0,
                span.text.data(), span.text.data() + span.text.size()).x;
        }
    }
    draw.PopClipRect();
}
}
