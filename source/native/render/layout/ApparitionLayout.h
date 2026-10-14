#pragma once
#include <cstddef>
#include <cstdint>
#include <string_view>
#include <optional>
#include "../../shared/MenuAppearance.h"

namespace iw::overlay::layout {
struct Point { float x = 0, y = 0; };
struct Rect {
    float x = 0, y = 0, width = 0, height = 0;
    float Right() const noexcept { return x + width; }
    float Bottom() const noexcept { return y + height; }
};
struct Color { std::uint8_t red, green, blue, alpha; };

enum class MenuHudPresence { Present, Absent };

// Reference units keep the HUD proportions separate from viewport pixels
struct Request {
    float width = 0, height = 0;
    std::size_t rowCount = 0, selectedIndex = 0;
    float instructionWidth = 260;
    std::size_t instructionLines = 4;
    bool notice = false;
    std::size_t noticeLines = 5;
    MenuAppearance appearance{};
    bool widthEditor = false;
    // An optional local presentation edge never changes the committed appearance
    std::optional<float> presentationWidth = std::nullopt;
    // Entering keeps adaptive help until the source editor flag becomes active
    bool positionEditor = false;
    bool bypassAdaptiveInstructions = false;
    // Menu reposition uses a different source soft lock from the footer editor
    bool menuPositionEditor = false;
    // Credits keep the title but replace options with their timed roll
    bool menuCredits = false;
    // Initial opening has a logical menu before its background HUD exists
    MenuHudPresence menuHudPresence = MenuHudPresence::Present;
    // Footer geometry can follow a committed target while its existing paint remains captured
    std::optional<MenuDesign> instructionGeometryDesign = std::nullopt;
    // A closed source footer uses its saved anchor rather than adaptive menu geometry
    bool closedInstructions = false;
};
struct Hud {
    bool visible = false, hasSelection = false;
    float scale = 0, rowFont = 0, titleFont = 0, instructionFont = 0;
    Rect body, banner, separator, bottomLine, scroller, instructions, instructionText, outline, notice;
    Point title;
    Point instructionAnchor;
    Point counter;
    bool instructionsVisible = true, counterVisible = false, boxesLeft = false;
    bool instructionsBackground = true, instructionsCentered = false;
    // Design-specific anchors stay separate from shared row and footer data
    MenuDesign design = MenuDesign::Apparition;
    Rect outer, titleClip, secondaryClip, chromeBounds;
    Point secondaryTitle;
    float rowCenterOffset = 8, scrollerTopOffset = 0, secondaryFont = 0;
    bool titleCentered = true, titleVisible = true, secondaryVisible = false;
    std::uint8_t titleAlpha = 255, counterAlpha = 179;
    std::size_t capacity = 10;
    std::size_t count = 0, selected = 0, first = 0, shown = 0;
    Point OptionCenter(std::size_t displayedRow, bool booleanRow = true) const noexcept;
    Rect BooleanBox(std::size_t displayedRow) const noexcept;
};

Hud MakeHud(const Request& request) noexcept;
Color Theme(std::string_view hex) noexcept;
inline constexpr Color Banner{55, 55, 55, 255};
inline constexpr Color Body{25, 25, 25, 128};
inline constexpr Color Instructions{42, 42, 42, 255};
inline constexpr std::size_t MaxRows = 128;
}
