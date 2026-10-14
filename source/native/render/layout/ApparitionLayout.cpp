#include "ApparitionLayout.h"
#include "../../shared/scroll/MenuScrollWindow.h"
#include "../../shared/MenuRowCapacity.h"
#include "NoticePlacement.h"
#include "MenuNoticePlacement.h"
#include "../designs/DesignGeometry.h"
#include "../../shared/MenuDesign.h"
#include <algorithm>
#include <cmath>

namespace iw::overlay::layout {
Hud MakeHud(const Request& request) noexcept {
    Hud hud;
    if (!std::isfinite(request.width) || !std::isfinite(request.height)
        || request.width < 160 || request.height < 120) { return hud; }
    // Uniform scaling preserves the reference proportions on wide and portrait viewports
    hud.scale = std::min(request.width / 640, request.height / 480);
    const float left = (request.width - 640 * hud.scale) / 2;
    const float top = (request.height - 480 * hud.scale) / 2;
    auto rect = [&](float x, float y, float width, float height) {
        return Rect{left + x * hud.scale, top + y * hud.scale, width * hud.scale, height * hud.scale};
    };
    hud.visible = true;
    hud.count = std::min(request.rowCount, MaxRows);
    hud.hasSelection = hud.count != 0;
    hud.selected = hud.count ? std::min(request.selectedIndex, hud.count - 1) : 0;
    const auto& appearance = request.appearance;
    hud.design = appearance.design;
    const auto metrics = MetricsFor(hud.design);
    // Direct portable callers also keep the compact window free of a counter
    const bool counter = !appearance.stealthUI && appearance.optionCounter && metrics.counter;
    const auto menuPosition = appearance.menuPosition.value_or(DefaultMenuPosition());
    // Cast before offsets so signed integer limits never overflow while moving the panel
    const float menuX = static_cast<float>(menuPosition.x), menuY = static_cast<float>(menuPosition.y);
    const bool nonBoxes = appearance.booleanDisplay != "Boxes";
    hud.capacity = MenuRowCapacity(appearance);
    float menuWidth = static_cast<float>(std::clamp(appearance.menuWidth, 200, 500));
    // Invalid local samples fall back to committed state before any coordinate arithmetic
    if (request.presentationWidth && std::isfinite(*request.presentationWidth)) {
        menuWidth = std::clamp(*request.presentationWidth, 200.0f, 500.0f);
    }
    hud.instructionsVisible = appearance.instructionsVisible;
    hud.counterVisible = counter;
    hud.boxesLeft = !nonBoxes && appearance.boolBoxLocation == "Left";
    hud.shown = std::max<std::size_t>(1, std::min(hud.count, hud.capacity));
    // The source keeps the cursor near the middle until either end of the option list
    hud.first = MenuScrollWindowFirst(hud.count, hud.capacity, hud.selected).value_or(0);
    // Soft lock removes ordinary options, selection and the option counter
    if (request.widthEditor || request.positionEditor || request.menuPositionEditor || request.menuCredits) {
        hud.hasSelection = false; hud.counterVisible = false;
        hud.count = 0; hud.first = 0; hud.selected = 0; hud.shown = 0;
    }
    const bool softLock = request.widthEditor || request.positionEditor || request.menuPositionEditor || request.menuCredits;
    const float bodyHeight = request.menuCredits ? 220 : request.menuPositionEditor ? 122 : request.positionEditor ? 20
        : request.widthEditor ? 120 : 18 * static_cast<float>(hud.shown) + metrics.bodyExtra;
    hud.body = rect(menuX, menuY, menuWidth, bodyHeight);
    hud.rowFont = 13 * hud.scale; hud.instructionFont = 1.1f * hud.rowFont;
    ConfigureDesignHud(hud, softLock, request.widthEditor);
    // Ordinary text keeps its established raster placement while alternate uses source center anchors
    const bool alternate = appearance.alternateInstructions;
    const bool adaptive = appearance.adaptiveInstructions && !alternate && !request.bypassAdaptiveInstructions && !request.closedInstructions;
    const std::size_t lines = alternate ? 1 : std::clamp<std::size_t>(request.instructionLines, 1, 6);
    hud.instructionsVisible = appearance.instructionsVisible && request.instructionLines != 0;
    hud.instructionsBackground = appearance.instructionsBackground;
    hud.instructionsCentered = alternate && appearance.instructionsBackground;
    const float instructionHeight = hud.instructionsVisible ? lines == 1 ? 12 : 14 * static_cast<float>(lines) - 2 : 0;
    const float wantedWidth = std::isfinite(request.instructionWidth) ? request.instructionWidth : 260;
    float instructionWidth = std::clamp(wantedWidth, 1.0f, 384.0f);
    const auto anchor = appearance.instructionPosition.value_or(DefaultInstructionPosition(alternate));
    // Cast before arithmetic so extreme signed anchors never overflow an integer
    const float anchorX = static_cast<float>(anchor.x), anchorY = static_cast<float>(anchor.y);
    float instructionX = anchorX, instructionY = anchorY - instructionHeight;
    if (adaptive) {
        // Only horizontal footer metrics follow a known target, never menu chrome or paint
        const auto footerMetrics = request.instructionGeometryDesign
            && IsKnownMenuDesign(*request.instructionGeometryDesign)
            ? MetricsFor(*request.instructionGeometryDesign) : metrics;
        instructionX = menuX + footerMetrics.footerX;
        // Missing HUDs retain adaptive horizontal anchors but fall back to saved footer Y
        instructionY = request.menuHudPresence == MenuHudPresence::Absent
            ? anchorY - instructionHeight : menuY + bodyHeight + metrics.footerY;
        instructionWidth = std::clamp(std::max(wantedWidth, menuWidth + footerMetrics.footerWidth), 1.0f, 638.0f);
    }
    hud.instructions = rect(instructionX, instructionY, instructionWidth, instructionHeight);
    hud.instructionText = hud.instructions;
    if (alternate) {
        // The source subtracts 28 units from alternate background width, not from the text clip
        const float textWidth = std::clamp(wantedWidth, 1.0f, hud.instructionsCentered ? 638.0f : 319.0f);
        const float backgroundWidth = std::clamp(wantedWidth - 28, 1.0f, 638.0f);
        const float textAnchorY = anchorY - instructionHeight;
        hud.instructions = rect(anchorX - backgroundWidth / 2, textAnchorY - instructionHeight / 2,
            backgroundWidth, instructionHeight);
        hud.instructionText = rect(hud.instructionsCentered ? anchorX - textWidth / 2 : anchorX,
            textAnchorY - instructionHeight / 2, textWidth, instructionHeight);
        hud.instructionAnchor = {left + anchorX * hud.scale, top + textAnchorY * hud.scale};
    }
    if (hud.instructionsVisible && hud.instructionsBackground) {
        hud.outline = {hud.instructions.x - hud.scale, hud.instructions.y - hud.scale,
            hud.instructions.width + 2 * hud.scale, hud.instructions.height + 2 * hud.scale};
    } else {
        // Text geometry stays intact when only the instruction decoration is hidden
        hud.outline = {hud.instructions.x, hud.instructions.y, 0, 0};
    }
    // An adjacent status box remains readable without overlapping the movable footer
    if (request.notice) {
        // Hidden help has no visible region that can reduce status space
        const float footerTop = hud.instructionsVisible
            ? std::clamp((hud.instructionText.y - top) / hud.scale, 0.0f, 472.0f) : 472;
        const float footerBottom = (hud.instructionText.Bottom() - top) / hud.scale;
        const float footerRight = (hud.instructionText.Right() - left) / hud.scale;
        const float wanted = 28 + 13 * static_cast<float>(std::min<std::size_t>(request.noticeLines, 128));
        const float sideLeft = menuWidth + 15, sideWidth = 630 - sideLeft;
        float belowTop = 65 + bodyHeight + 35 + 12;
        float belowSpace = footerTop - belowTop - 8;
        float sideTop = 85, sideSpace = footerTop - sideTop - 8;
        if (adaptive && hud.instructionsVisible) {
            belowTop = std::max(belowTop, footerBottom + 12);
            belowSpace = 472 - belowTop - 8;
            const bool crossesFooter = footerRight > sideLeft;
            sideSpace = crossesFooter ? footerTop - sideTop - 8 : 472 - sideTop;
            // A short menu can leave too little room above its footer for a readable notice
            if (crossesFooter && sideSpace < 48) {
                sideTop = footerBottom + 8; sideSpace = 472 - sideTop;
            }
        }
        if (sideWidth < 200 && belowSpace >= 48) {
            hud.notice = rect(4, belowTop, menuWidth - 8, std::min(wanted, belowSpace));
        } else {
            hud.notice = rect(sideLeft, sideTop, sideWidth, std::clamp(wanted, 48.0f, std::max(48.0f, sideSpace)));
        }
    }
    if (request.notice && hud.instructionsVisible && (appearance.instructionPosition || request.positionEditor)) {
        // Default panels keep their original pixels while moved help gets bounded free space
        const auto units = [&](Rect box) { return Rect{(box.x - left) / hud.scale, (box.y - top) / hud.scale,
            box.width / hud.scale, box.height / hud.scale}; };
        Rect footer = hud.instructionText;
        if (hud.instructionsBackground) {
            // Alternate text is wider than its source background and must also remain unobscured
            const float x = std::min(footer.x, hud.outline.x), y = std::min(footer.y, hud.outline.y);
            footer = {x, y, std::max(footer.Right(), hud.outline.Right()) - x,
                std::max(footer.Bottom(), hud.outline.Bottom()) - y};
        }
        const float belowY = 65 + bodyHeight + 35 + 12;
        auto status = PlaceNoticeAwayFromInstructions(units(hud.notice),
            {4, belowY, menuWidth - 8, std::max(0.0f, 464 - belowY)},
            {menuWidth + 15, 85, 615 - menuWidth, 387}, units(footer));
        hud.notice = rect(status.x, status.y, status.width, status.height);
    }
    if (request.notice && (appearance.menuPosition || hud.design != MenuDesign::Apparition)) {
        // The menu anchor remains exact while the IW notice uses visible viewport space
        const auto units = [&](Rect box) { return Rect{(box.x - left) / hud.scale, (box.y - top) / hud.scale,
            box.width / hud.scale, box.height / hud.scale}; };
        Rect footer = hud.instructionsVisible ? hud.instructionText : Rect{};
        if (hud.instructionsVisible && hud.instructionsBackground) {
            const float x = std::min(footer.x, hud.outline.x), y = std::min(footer.y, hud.outline.y);
            footer = {x, y, std::max(footer.Right(), hud.outline.Right()) - x,
                std::max(footer.Bottom(), hud.outline.Bottom()) - y};
        }
        const auto status = PlaceNoticeAroundMovedMenu(units(hud.notice), units(hud.chromeBounds), units(footer));
        hud.notice = rect(status.x, status.y, status.width, status.height);
    }
    return hud;
}

Point Hud::OptionCenter(std::size_t displayedRow, bool booleanRow) const noexcept {
    if (!visible || !shown) { return {}; }
    const float row = static_cast<float>(std::min(displayedRow, shown - 1));
    return {body.x + (boxesLeft && booleanRow ? 18 : 4) * scale, body.y + (rowCenterOffset + 18 * row) * scale};
}

Rect Hud::BooleanBox(std::size_t displayedRow) const noexcept {
    if (!visible || !shown) { return {}; }
    const auto center = OptionCenter(displayedRow);
    // The source places the box center eight units from the right edge
    return {boxesLeft ? body.x + 4 * scale : body.Right() - 13 * scale, center.y - 5 * scale, 10 * scale, 10 * scale};
}

Color Theme(std::string_view hex) noexcept {
    constexpr Color fallback{57, 152, 254, 255};
    if (hex.size() != 7 || hex.front() != '#') { return fallback; }
    unsigned value = 0;
    for (char digit : hex.substr(1)) {
        const unsigned part = digit >= '0' && digit <= '9' ? digit - '0'
            : digit >= 'a' && digit <= 'f' ? digit - 'a' + 10
            : digit >= 'A' && digit <= 'F' ? digit - 'A' + 10 : 16;
        // Malformed themes cannot introduce invalid or transparent drawing colors
        if (part > 15) { return fallback; }
        value = (value << 4) | part;
    }
    return {static_cast<std::uint8_t>(value >> 16), static_cast<std::uint8_t>(value >> 8),
        static_cast<std::uint8_t>(value), 255};
}
}
