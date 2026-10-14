#include "MenuNoticePlacement.h"
#include <algorithm>
#include <array>
#include <cmath>

namespace iw::overlay::layout {
namespace {
constexpr Rect Viewport{2, 2, 636, 476};
bool Overlaps(Rect a, Rect b) noexcept {
    return a.width > 0 && a.height > 0 && b.width > 0 && b.height > 0
        && a.x < b.Right() && b.x < a.Right() && a.y < b.Bottom() && b.y < a.Bottom();
}
Rect Visible(Rect box) noexcept {
    // Clamp both corners before subtracting, including extreme signed saved positions
    const float x = std::clamp(box.x, Viewport.x, Viewport.Right());
    const float y = std::clamp(box.y, Viewport.y, Viewport.Bottom());
    return {x, y, std::max(0.0f, std::clamp(box.Right(), Viewport.x, Viewport.Right()) - x),
        std::max(0.0f, std::clamp(box.Bottom(), Viewport.y, Viewport.Bottom()) - y)};
}
Rect Padded(Rect box) noexcept {
    box = Visible(box);
    return box.width > 0 && box.height > 0 ? Visible({box.x - 3, box.y - 3, box.width + 6, box.height + 6}) : Rect{};
}
}
Rect PlaceNoticeAroundMovedMenu(Rect preferred, Rect menu, Rect footer) noexcept {
    menu = Padded(menu); footer = Padded(footer);
    preferred = Visible(preferred);
    if (preferred.width >= 80 && preferred.height >= 48
        && !Overlaps(preferred, menu) && !Overlaps(preferred, footer)) return preferred;
    std::array<Rect, 16> free{}; free[0] = Viewport; std::size_t count = 1;
    for (const auto obstacle : {menu, footer}) {
        std::array<Rect, 16> next{}; std::size_t nextCount = 0;
        for (std::size_t index = 0; index < count; ++index) {
            const auto region = free[index];
            const auto append = [&](Rect box) {
                if (box.width >= 80 && box.height >= 48 && nextCount < next.size()) next[nextCount++] = box;
            };
            if (!Overlaps(region, obstacle)) { append(region); continue; }
            // Four overlapping candidates contain every unobstructed rectangular strip
            append({region.x, region.y, region.width, std::max(0.0f, obstacle.y - region.y)});
            const float bottom = std::max(region.y, obstacle.Bottom());
            append({region.x, bottom, region.width, std::max(0.0f, region.Bottom() - bottom)});
            append({region.x, region.y, std::max(0.0f, obstacle.x - region.x), region.height});
            const float right = std::max(region.x, obstacle.Right());
            append({right, region.y, std::max(0.0f, region.Right() - right), region.height});
        }
        free = next; count = nextCount;
    }
    Rect best{}; float bestScore = -1;
    for (std::size_t index = 0; index < count; ++index) {
        const auto region = free[index];
        if (Overlaps(region, menu) || Overlaps(region, footer)) continue;
        // Wide regions keep ordinary status text readable before narrower fallback strips
        const float score = region.width * region.height + (region.width >= 180 ? 640 * 480 : 0);
        if (score > bestScore) { best = region; bestScore = score; }
    }
    const float height = std::clamp(preferred.height, 48.0f, Viewport.height);
    const float width = std::clamp(preferred.width, 180.0f, Viewport.width);
    if (bestScore < 0) {
        // Some saved menu and footer positions cover all readable space, so status takes priority
        return {Viewport.x + 2, Viewport.y + 2, width, std::min(height, 120.0f)};
    }
    best.width = std::min(best.width, width); best.height = std::min(best.height, height);
    return best;
}
}
