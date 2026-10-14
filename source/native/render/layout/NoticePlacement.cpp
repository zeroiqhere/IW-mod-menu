#include "NoticePlacement.h"
#include <algorithm>
#include <array>

namespace iw::overlay::layout {
namespace {
bool Overlaps(Rect a, Rect b) noexcept {
    return a.width > 0 && a.height > 0 && b.width > 0 && b.height > 0
        && a.x < b.Right() && b.x < a.Right() && a.y < b.Bottom() && b.y < a.Bottom();
}
Rect Intersect(Rect a, Rect b) noexcept {
    const float x = std::max(a.x, b.x), y = std::max(a.y, b.y);
    return {x, y, std::max(0.0f, std::min(a.Right(), b.Right()) - x),
        std::max(0.0f, std::min(a.Bottom(), b.Bottom()) - y)};
}
}
Rect PlaceNoticeAwayFromInstructions(Rect preferred, Rect below, Rect side, Rect footer) noexcept {
    // Offscreen help has no visible area that a status box needs to avoid
    footer = Intersect(footer, {0, 0, 640, 480});
    if (!Overlaps(preferred, footer)) { return preferred; }
    // A small gap separates the status outline from the footer text and outline
    footer = {footer.x - 3, footer.y - 3, footer.width + 6, footer.height + 6};
    Rect best{};
    float bestArea = -1;
    for (auto region : std::array<Rect, 2>{below, side}) {
        // Each candidate remains outside the menu and inside the reference viewport
        const std::array<Rect, 4> pieces{{
            {region.x, region.y, region.width, std::max(0.0f, std::min(region.Bottom(), footer.y) - region.y)},
            {region.x, std::max(region.y, footer.Bottom()), region.width,
                std::max(0.0f, region.Bottom() - std::max(region.y, footer.Bottom()))},
            {region.x, region.y, std::max(0.0f, std::min(region.Right(), footer.x) - region.x), region.height},
            {std::max(region.x, footer.Right()), region.y,
                std::max(0.0f, region.Right() - std::max(region.x, footer.Right())), region.height}}};
        for (auto piece : pieces) {
            if (piece.width < 80 || piece.height < 48 || Overlaps(piece, footer)) { continue; }
            const float area = piece.width * piece.height;
            if (area > bestArea) { best = piece; bestArea = area; }
        }
    }
    if (bestArea < 0) { return {}; }
    // Keep the requested status height when the chosen free region has more space
    best.height = std::min(best.height, preferred.height);
    return best;
}
}
