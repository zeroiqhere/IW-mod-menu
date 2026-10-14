#pragma once
#include <filesystem>
#include <functional>
#include <string>
#include <utility>
#include <vector>
namespace iw::overlay::tests {
// Initial owner phases are checked through the same panel used by the native renderer
void AddInitialOpenPanelChecks(std::vector<std::pair<std::string, std::function<void()>>>& checks);
void VerifyInitialOpenBackendPreviews(const std::filesystem::path& fixtures, const std::filesystem::path& output);
}
