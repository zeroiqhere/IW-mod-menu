#pragma once
#include <string>
#include <vector>

namespace iw::overlay {
// Editor state owns the confirmed adjustment step and saved-control help
struct WidthEditor {
    int step = 1;
    // Runtime replaces these action labels with the matching saved binding names
    std::vector<std::string> instructions{
        "Fire: Increase Width", "Aim: Decrease Width", "Next choice: Increase Offset",
        "Previous choice: Decrease Offset", "Melee: Exit"};
    // Editor help describes paused navigation without advertising inactive controls
    std::vector<std::string> footerInstructions{
        "Width Editor", "Menu navigation paused", "Melee: Exit editor"};
    bool operator==(const WidthEditor&) const = default;
};
}
