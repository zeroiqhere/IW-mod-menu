#pragma once
#include <filesystem>
#include <functional>
#include <string>
#include <utility>
#include <vector>

namespace iw::overlay::tests {
// These checks draw the production design painters without any graphics backend
void AddDesignPanelChecks(std::vector<std::pair<std::string, std::function<void()>>>& checks);
void VerifyDesignBackendPreviews(const std::filesystem::path& fixtures, const std::filesystem::path& output);
}
