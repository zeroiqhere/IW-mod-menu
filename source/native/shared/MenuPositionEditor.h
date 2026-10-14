#pragma once
#include <string>
#include <vector>

namespace iw::overlay {
// Editor metadata owns the confirmed offset and private saved-control descriptions
struct MenuPositionEditor {
    int step = 1;
    // Exact runtime bindings replace these generic action names
    std::vector<std::string> instructions{
        "Action slot 1: Move Up", "Action slot 2: Move Down", "Action slot 3: Move Left",
        "Action slot 4: Move Right", "Frag: Increase Offset", "Secondary offhand: Decrease Offset", "Melee: Exit"};
    // Ordinary snapshot instructions remain available when the parent page returns
    std::vector<std::string> footerInstructions{
        "Reposition Menu", "Menu navigation paused", "Melee: Exit editor"};
    bool operator==(const MenuPositionEditor&) const = default;
};
}
