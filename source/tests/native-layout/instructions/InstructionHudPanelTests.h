#pragma once
#include <functional>
#include <string>
#include <utility>
#include <vector>
namespace iw::overlay::tests {
void AddInstructionHudPanelChecks(std::vector<std::pair<std::string, std::function<void()>>>& checks);
}
