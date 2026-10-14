#pragma once
#include <functional>
#include <string>
#include <utility>
#include <vector>
namespace iw::overlay::tests {
// Design gap geometry does not replace captured menu or footer painting
void AddDesignFooterLayoutChecks(std::vector<std::pair<std::string, std::function<void()>>>& checks);
} // namespace iw::overlay::tests
