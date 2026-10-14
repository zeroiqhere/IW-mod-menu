#pragma once
#include <filesystem>
#include <functional>
#include <string>
#include <utility>
#include <vector>
namespace iw::overlay::tests {
// Rebuild geometry is separated from existing footer paint and owner input state
void AddDesignFooterPanelChecks(std::vector<std::pair<std::string, std::function<void()>>>& checks);
void VerifyDesignFooterBackendPreviews(const std::filesystem::path& fixtures, const std::filesystem::path& output);
} // namespace iw::overlay::tests
