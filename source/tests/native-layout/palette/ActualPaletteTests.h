#pragma once
#include <filesystem>
#include <functional>
#include <string>
#include <vector>
namespace iw::overlay::tests {
void AddActualLiteralPaletteChecks(std::vector<std::pair<std::string, std::function<void()>>>& checks,
    const std::filesystem::path& directory);
}
