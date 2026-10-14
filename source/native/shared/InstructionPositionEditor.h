#pragma once
#include <string>
#include <vector>

namespace iw::overlay {
enum class InstructionPositionEditorPhase { Entering, Editing, Exiting };

// The editor owns its phase and help without keeping a pointer to private packets
struct InstructionPositionEditor {
    InstructionPositionEditorPhase phase = InstructionPositionEditorPhase::Entering;
    std::vector<std::string> instructions{
        "Action slot 1: Move Up", "Action slot 2: Move Down", "Action slot 3: Move Left",
        "Action slot 4: Move Right", "Melee: Exit"};
    bool operator==(const InstructionPositionEditor&) const = default;
};
}
