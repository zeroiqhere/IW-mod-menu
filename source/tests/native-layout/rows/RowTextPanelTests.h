#pragma once
#include <functional>
#include <string>
#include <utility>
#include <vector>
namespace iw::overlay::tests {
// Actual offline packets keep long labels separate from complete status ink
void AddRowTextPanelChecks(std::vector<std::pair<std::string, std::function<void()>>> &checks);
} // namespace iw::overlay::tests
