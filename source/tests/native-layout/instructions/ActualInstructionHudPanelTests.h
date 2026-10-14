#pragma once
#include "InstructionHudPanelTests.h"
#include <filesystem>
namespace iw::overlay::tests {
void AddActualInstructionHudPanelChecks(std::vector<std::pair<std::string,std::function<void()>>>& checks,
    const std::filesystem::path& directory);
}
