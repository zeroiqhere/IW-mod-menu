#pragma once
#include <functional>
#include <filesystem>
#include <string>
#include <vector>
namespace iw::overlay::tests {
void AddActualInstructionColorPanelChecks(std::vector<std::pair<std::string,std::function<void()>>>& checks,
    const std::filesystem::path& directory);
}
