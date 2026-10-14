#pragma once
#include <functional>
#include <filesystem>
#include <string>
#include <utility>
#include <vector>

namespace iw::overlay::tests {
// Scroll checks exercise the production panel in a CPU-only ImGui context
void VerifyScrollBackendPreviews(const std::filesystem::path& fixtures, const std::filesystem::path& output);
void AddScrollPanelChecks(std::vector<std::pair<std::string, std::function<void()>>>& checks);
}
