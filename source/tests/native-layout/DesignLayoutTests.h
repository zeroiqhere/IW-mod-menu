#pragma once
#include <functional>
#include <string>
#include <utility>
#include <vector>

namespace iw::overlay::tests {
// Reference geometry checks run without ImGui or a graphics device
void AddDesignLayoutChecks(std::vector<std::pair<std::string, std::function<void()>>>& checks);
}
