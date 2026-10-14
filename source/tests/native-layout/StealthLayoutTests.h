#pragma once
#include <functional>
#include <string>
#include <utility>
#include <vector>
namespace iw::overlay::tests {
// Compact row geometry uses the same Design metrics as ordinary menus
void AddStealthLayoutChecks(std::vector<std::pair<std::string, std::function<void()>>> &checks);
} // namespace iw::overlay::tests
