#pragma once
#include <filesystem>
#include <functional>
#include <string>
#include <utility>
#include <vector>
namespace iw::overlay::tests {
// These checks use the production drawlist and never create a graphics device
void AddOpeningPanelChecks(std::vector<std::pair<std::string, std::function<void()>>>& checks);
void VerifyOpeningBackendPreviews(const std::filesystem::path& fixtures, const std::filesystem::path& output);
}
