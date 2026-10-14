#pragma once
#include <filesystem>
#include <functional>
#include <string>
#include <utility>
#include <vector>
namespace iw::overlay::tests {
// CPU panels keep compact rows separate from paused rebuild help
void AddStealthPanelChecks(std::vector<std::pair<std::string, std::function<void()>>> &checks);
void VerifyStealthBackendPreviews(const std::filesystem::path &fixtures);
} // namespace iw::overlay::tests
