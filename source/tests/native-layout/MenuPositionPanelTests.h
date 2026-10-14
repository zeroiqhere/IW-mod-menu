#pragma once
#include <functional>
#include <filesystem>
#include <string>
#include <utility>
#include <vector>

namespace iw::overlay::tests {
void VerifyMenuPositionBackendPreviews(const std::filesystem::path& fixtures, const std::filesystem::path& output);
void AddMenuPositionPanelChecks(std::vector<std::pair<std::string, std::function<void()>>>& checks);
}
