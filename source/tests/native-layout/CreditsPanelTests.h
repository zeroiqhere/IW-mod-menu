#pragma once
#include <filesystem>
#include <functional>
#include <string>
#include <utility>
#include <vector>

namespace iw::overlay::tests {
void AddCreditsPanelChecks(std::vector<std::pair<std::string, std::function<void()>>>& checks);
void VerifyCreditsBackendPreviews(const std::filesystem::path& fixtures, const std::filesystem::path& output);
}
