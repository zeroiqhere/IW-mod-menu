#include "ControlLabels.h"
#include "OpeningActionBindings.h"
#include <algorithm>
#include <stdexcept>

namespace iw::overlay::runtime {
namespace {
std::string KeyLabel(int key) {
    // Virtual-key labels match the supported saved binding names
    switch (key) {
        case 1: return "LMB";
        case 2: return "RMB";
        case 4: return "Mouse 3";
        case 5: return "Mouse 4";
        case 6: return "Mouse 5";
        case 8: return "Backspace";
        case 9: return "Tab";
        case 13: return "Enter";
        case 16: return "Shift";
        case 17: return "Ctrl";
        case 18: return "Alt";
        case 27: return "Escape";
        case 32: return "Space";
        case 37: return "Left";
        case 38: return "Up";
        case 39: return "Right";
        case 40: return "Down";
        case 46: return "Delete";
        default: break;
    }
    if ((key >= 65 && key <= 90) || (key >= 48 && key <= 57)) {
        return std::string(1, static_cast<char>(key));
    }
    if (key >= 112 && key <= 123) return "F" + std::to_string(key - 111);
    // Unnamed valid keys remain explicit instead of displaying a guessed binding
    return "Key " + std::to_string(key);
}
std::string Labels(const std::vector<int>& keys) {
    std::string result;
    for (std::size_t index = 0; index < std::min<std::size_t>(keys.size(), 2); ++index) {
        if (index) result += " / ";
        result += KeyLabel(keys[index]);
    }
    // The instructions stay compact while disclosing additional accepted bindings
    if (keys.size() > 2) result += " (+" + std::to_string(keys.size() - 2) + ")";
    return result;
}
}
std::vector<std::string> DescribeControls(const Bindings& bindings, bool quickExit, bool isRootMenu) {
    ValidateBindings(bindings);
    // Root close is immediate, while an enabled child preference distinguishes tap and hold
    const auto back = quickExit ? isRootMenu ? "Close" : "Tap back / hold close" : "Back / close";
    std::vector<std::string> hints{Labels(bindings.up) + " | " + Labels(bindings.down) + ": Scroll",
        Labels(bindings.select) + ": Select", Labels(bindings.melee) + ": " + back};
    // A partial saved pair cannot adjust both directions and must not advertise a route
    if (!bindings.previousChoice.empty() && !bindings.nextChoice.empty()) {
        hints.push_back(Labels(bindings.previousChoice) + ": Previous | "
            + Labels(bindings.nextChoice) + ": Next choice");
    }
    return hints;
}
std::vector<std::string> DescribeWidthEditorControls(const Bindings& bindings) {
    ValidateBindings(bindings);
    if (bindings.attack.empty() || bindings.previousChoice.empty() || bindings.nextChoice.empty()) {
        throw std::invalid_argument("Width editor needs exact attack and both offset controls");
    }
    // Resize excludes action-slot scroll aliases and every hint comes from saved keys
    return {Labels(bindings.attack) + ": Increase Width", Labels(bindings.aim) + ": Decrease Width",
        Labels(bindings.nextChoice) + ": Increase Offset", Labels(bindings.previousChoice) + ": Decrease Offset",
        Labels(bindings.melee) + ": Exit"};
}
std::vector<std::string> DescribeInstructionPositionControls(const Bindings& bindings) {
    ValidateBindings(bindings);
    if (bindings.moveUp.empty() || bindings.moveDown.empty()
        || bindings.previousChoice.empty() || bindings.nextChoice.empty()) {
        throw std::invalid_argument("Position editor needs four exact action-slot controls");
    }
    // Each direction uses its own saved action slot rather than an aim or attack alias
    return {Labels(bindings.moveUp) + ": Move Up", Labels(bindings.moveDown) + ": Move Down",
        Labels(bindings.previousChoice) + ": Move Left", Labels(bindings.nextChoice) + ": Move Right",
        Labels(bindings.melee) + ": Exit"};
}
std::vector<std::string> DescribeMenuPositionControls(const Bindings& bindings) {
    ValidateBindings(bindings);
    if (bindings.moveUp.empty() || bindings.moveDown.empty() || bindings.previousChoice.empty()
        || bindings.nextChoice.empty() || bindings.frag.empty() || bindings.secondaryOffhand.empty()) {
        throw std::invalid_argument("Menu positioning needs four exact slots and both grenade controls");
    }
    // The source lists frag before smoke while input priority checks smoke first
    return {Labels(bindings.moveUp) + ": Move Up", Labels(bindings.moveDown) + ": Move Down",
        Labels(bindings.previousChoice) + ": Move Left", Labels(bindings.nextChoice) + ": Move Right",
        Labels(bindings.frag) + ": Increase Offset", Labels(bindings.secondaryOffhand) + ": Decrease Offset",
        Labels(bindings.melee) + ": Exit"};
}
std::vector<std::string> DescribeMenuCreditsControls(const Bindings& bindings) {
    ValidateBindings(bindings);
    // Required saved melee is sufficient; no editor or grenade capability is borrowed
    return {Labels(bindings.melee) + ": Exit Menu Credits"};
}
OpeningActionLabels DescribeOpeningActions(const Bindings& bindings) {
    ValidateBindings(bindings);
    OpeningActionLabels labels;
    for (unsigned index = 0; index < OpeningActionCount; ++index) {
        const auto& keys = OpeningActionKeys(bindings, static_cast<OpeningAction>(index));
        if (!keys.empty()) labels[index] = Labels(keys);
    }
    return labels;
}

}
