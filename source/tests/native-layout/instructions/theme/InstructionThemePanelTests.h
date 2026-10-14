#pragma once
#include <functional>
#include <string>
#include <vector>
namespace iw::overlay::tests {
void AddInstructionThemePanelChecks(std::vector<std::pair<std::string,std::function<void()>>>& checks);
}
