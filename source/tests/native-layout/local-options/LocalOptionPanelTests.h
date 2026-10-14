#pragma once
#include <functional>
#include <filesystem>
#include <string>
#include <utility>
#include <vector>

namespace iw::overlay::tests {
void AddLocalOptionPanelChecks(std::vector<std::pair<std::string, std::function<void()>>>& checks);
void AddActualLocalPaletteChecks(std::vector<std::pair<std::string, std::function<void()>>>& checks,
    const std::filesystem::path& directory);
}
