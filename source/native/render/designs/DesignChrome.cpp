#include "DesignChrome.h"
#include "../PanelGeometry.h"
#include <imgui.h>

namespace iw::overlay {
void DrawDesignChrome(ImDrawList& draw, const layout::Hud& hud, layout::Color theme, bool rainbow, std::optional<layout::Color> basicScrollerColor, const ChromeColorFrame* ownedColors) {
    const auto fill = [&](layout::Rect rectangle, layout::Color color) {
        const auto viewport = ImGui::GetIO().DisplaySize;
        const auto visible = VisiblePanelRect(rectangle, viewport.x, viewport.y);
        if (visible.width > 0 && visible.height > 0)
            draw.AddRectFilled({visible.x, visible.y}, {visible.Right(), visible.Bottom()}, IM_COL32(color.red, color.green, color.blue, color.alpha));
    };
    const auto role = [&](ChromeRole name, layout::Rect rectangle, layout::Color legacy) {
        if (ownedColors) {
            if (!ownedColors->admitted || !(ownedColors->currentRoleMask & ChromeRoleBit(name))) return;
            fill(rectangle, ownedColors->Color(name));
        } else fill(rectangle, legacy);
    };
    if (ownedColors && !ownedColors->admitted) return;
    // Painter order follows source HUD sort values, including translucent bodies
    switch (hud.design) {
        case MenuDesign::Classic:
            role(ChromeRole::Banner, hud.banner, theme); fill(hud.body, {25, 25, 25, 217});
            if (hud.hasSelection) role(ChromeRole::Scroller, hud.scroller, theme);
            break;
        case MenuDesign::Native: {
            auto banner = theme; banner.alpha = 230; auto scroller = theme; scroller.alpha = 179;
            role(ChromeRole::Banner, hud.banner, banner); fill(hud.body, {25, 25, 25, 115});
            if (hud.hasSelection) role(ChromeRole::Scroller, hud.scroller, scroller);
            role(ChromeRole::Separator, hud.separator, {0, 0, 0, 255}); break;
        }
        case MenuDesign::Aio:
            fill(hud.outer, {0, 0, 0, 77}); fill(hud.body, {0, 0, 0, 115});
            if (hud.hasSelection) role(ChromeRole::Scroller, hud.scroller, theme);
            role(ChromeRole::Separator, hud.separator, theme); role(ChromeRole::BottomLine, hud.bottomLine, theme); break;
        case MenuDesign::Basic:
            fill(hud.body, {0, 0, 0, 115}); role(ChromeRole::Banner, hud.banner, {0, 0, 0, 255});
            // Fixed Basic leaves the existing scroller RGB unchanged after direct Rainbow samples
            if (hud.hasSelection) role(ChromeRole::Scroller, hud.scroller, basicScrollerColor.value_or(rainbow ? theme : layout::Color{0, 0, 0, 255}));
            break;
        default:
            role(ChromeRole::Banner, hud.banner, layout::Banner); fill(hud.body, layout::Body);
            if (hud.hasSelection) role(ChromeRole::Scroller, hud.scroller, layout::Banner);
            role(ChromeRole::Separator, hud.separator, theme); role(ChromeRole::BottomLine, hud.bottomLine, theme); break;
    }
}
}
