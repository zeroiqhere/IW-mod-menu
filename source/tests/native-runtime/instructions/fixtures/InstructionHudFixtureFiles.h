#pragma once
#include "InstructionHudFixtureExpected.h"
#include <filesystem>
#include <fstream>
#include <set>
#include <stdexcept>
namespace iw::overlay::tests::instructions {
inline std::string InstructionFilename(const ExpectedInstructionHud& profile) {
    return "instruction-hud-" + std::string(profile.name) + ".json";
}
inline void RequireInstruction(bool value, const char* reason) {
    if (!value) throw std::runtime_error(reason);
}
inline void CheckInstructionInventory(const std::filesystem::path& directory) {
    std::set<std::string> expected, actual;
    for (const auto& profile : InstructionProfiles) expected.insert(InstructionFilename(profile));
    for (const auto& file : std::filesystem::directory_iterator(directory)) {
        RequireInstruction(file.is_regular_file() && !file.is_symlink(), "Instruction inventory includes a non-regular input");
        actual.insert(file.path().filename().string());
    }
    RequireInstruction(actual == expected, "The actual instruction inventory must contain exactly35 named packets");
}
inline std::string ReadInstructionPacket(const std::filesystem::path& path) {
    RequireInstruction(std::filesystem::is_regular_file(path) && !std::filesystem::is_symlink(path)
        && std::filesystem::file_size(path) <= 32768, "Instruction packet missing, non-regular or oversized");
    std::ifstream file(path, std::ios::binary);
    if (!file) throw std::runtime_error("Could not read instruction packet");
    return {std::istreambuf_iterator<char>(file), std::istreambuf_iterator<char>()};
}
}
