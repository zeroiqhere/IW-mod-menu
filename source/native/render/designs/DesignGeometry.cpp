#include "DesignGeometry.h"

namespace iw::overlay::layout {
DesignMetrics MetricsFor(MenuDesign design) noexcept {
    switch (design) {
        case MenuDesign::Classic: return {11, 7, 8, 0, 5, 0, true, false};
        case MenuDesign::Native: return {8, 0, 8, 1, 5, -2, true, false};
        case MenuDesign::Aio: return {15, 13, 11, -1, 30, 2, false, true};
        case MenuDesign::Basic: return {30, 22, 8, 0, 5, 0, false, true};
        default: return {8, 0, 8, 1, 18, -2, true, true};
    }
}
void ConfigureDesignHud(Hud& hud, bool softLock, bool widthEditor) noexcept {
    const auto metrics = MetricsFor(hud.design);
    const float s = hud.scale, width = hud.body.width, height = hud.body.height;
    const auto rect = [&](float x, float y, float w, float h) {
        return Rect{hud.body.x + x * s, hud.body.y + y * s, w, h};
    };
    const auto point = [&](float x, float y) { return Point{hud.body.x + x * s, hud.body.y + y * s}; };
    hud.rowCenterOffset = metrics.rowOffset; hud.scrollerTopOffset = metrics.rowOffset - metrics.scrollerOffset;
    hud.scroller = rect(0, hud.scrollerTopOffset + 18 * static_cast<float>(hud.selected - hud.first),
        hud.design == MenuDesign::Aio ? 2 * s : width, hud.hasSelection ? (hud.design == MenuDesign::Aio ? 23 : 18) * s : 0);
    hud.titleVisible = !widthEditor; hud.secondaryVisible = !widthEditor && (hud.design == MenuDesign::Native || hud.design == MenuDesign::Aio);
    // Source header and lower status anchors are not shared between these layouts
    switch (hud.design) {
        case MenuDesign::Classic:
            hud.banner = rect(-1, -13, width + 2 * s, height + 14 * s);
            hud.title = point(4, -7); hud.titleFont = 1.2f * hud.rowFont; hud.titleCentered = false;
            hud.titleClip = rect(0, -13, width, 13 * s); hud.counterAlpha = 255;
            hud.chromeBounds = hud.banner; break;
        case MenuDesign::Native:
            hud.banner = rect(0, -55, width, 38 * s); hud.separator = rect(0, -17, width, 17 * s);
            hud.title = point(4, -9.5f); hud.titleFont = hud.rowFont; hud.titleCentered = false; hud.titleAlpha = 179;
            hud.titleClip = hud.separator; hud.secondaryClip = hud.banner;
            hud.secondaryTitle = {hud.body.x + width / 2, hud.body.y - 36 * s}; hud.secondaryFont = 1.6f * hud.rowFont;
            hud.chromeBounds = rect(0, -55, width, height + 55 * s); break;
        case MenuDesign::Aio:
            hud.separator = rect(0, -25, width, 25 * s); hud.bottomLine = rect(0, 0, width, 25 * s);
            hud.bottomLine.y = hud.body.Bottom() - (softLock ? s : 0);
            hud.outer = rect(-2, -27, width + 4 * s, height + (softLock ? 53 : 54) * s);
            hud.title = point(4, -13.5f); hud.titleFont = 1.4f * hud.rowFont; hud.titleCentered = false;
            hud.titleClip = hud.separator; hud.secondaryClip = hud.bottomLine;
            hud.secondaryTitle = {hud.body.x + 2 * s, hud.bottomLine.y + 11.5f * s}; hud.secondaryFont = 1.4f * hud.rowFont;
            hud.chromeBounds = hud.outer; break;
        case MenuDesign::Basic:
            hud.banner = rect(0, 0, width, 20 * s); hud.title = {hud.body.x + width / 2, hud.body.y + 9 * s};
            hud.titleFont = 1.4f * hud.rowFont; hud.titleClip = hud.banner; hud.chromeBounds = hud.body; break;
        default:
            hud.banner = rect(0, -20, width, height + 35 * s); hud.separator = rect(0, -1, width, s);
            hud.bottomLine = rect(0, 0, width, s); hud.bottomLine.y = hud.body.Bottom() - (softLock ? s : 0);
            hud.title = {hud.body.x + width / 2, hud.body.y - 12 * s}; hud.titleFont = 1.5f * hud.rowFont;
            hud.titleClip = {hud.banner.x, hud.banner.y, width, 19 * s}; hud.chromeBounds = hud.banner; break;
    }
    hud.counter = {hud.body.Right() - (hud.count >= 10 ? 16 : 12) * s,
        hud.design == MenuDesign::Classic || hud.design == MenuDesign::Native ? hud.title.y : hud.body.Bottom() + 8 * s};
}
}
