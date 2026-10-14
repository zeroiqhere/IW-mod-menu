#pragma once
#include <filesystem>
#include <functional>
#include <string>
#include <utility>
#include <vector>

namespace iw::overlay::tests {
// These checks use only the production panel and owned CPU draw data
void AddPreferenceRefreshPanelChecks(std::vector<std::pair<std::string, std::function<void()>>>& checks);
void VerifyPreferenceRefreshBackendPreviews(const std::filesystem::path& fixtures, const std::filesystem::path& output);
}
