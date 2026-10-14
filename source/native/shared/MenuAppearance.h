#pragma once
#include <string>
#include <optional>
#include "InstructionPosition.h"
#include "MenuPosition.h"
#include "MenuDesign.h"

namespace iw::overlay {

// Presentation preferences never describe a process address or a game operation
struct MenuAppearance {
    std::string booleanDisplay = "Boxes";
    std::string boolBoxLocation = "Right";
    bool instructionsVisible = true;
    bool optionCounter = false;
    bool coloredCursor = true;
    bool spotlightCursor = true;
    bool largeCursor = false;
    // Older appearance packets keep the ordinary back behavior
    bool quickExit = false;

    // Reference width is committed in the bounded 200 to 500 unit range
    int menuWidth = 260;

    // Append instruction choices so older aggregate initializers keep their field order
    bool alternateInstructions = false;
    bool instructionsBackground = true;
    bool adaptiveInstructions = false;
    // Null preserves source style defaults instead of storing an invented coordinate
    std::optional<InstructionPosition> instructionPosition = std::nullopt;
    // Source slider steps represent ten milliseconds and default to twelve steps
    int scrollAnimationMilliseconds = 120;

    // Append the panel origin so older aggregate callers keep their field order
    std::optional<MenuPosition> menuPosition = std::nullopt;

    // Appended design preserves the order and defaults of older appearance aggregates
    MenuDesign design = MenuDesign::Apparition;

    // Stealth reserves a five-row window without adding a new game action
    bool stealthUI = false;

    bool operator==(const MenuAppearance&) const = default;
};

}
