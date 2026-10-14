#pragma once
#include <functional>
#include <string>
#include <utility>
#include <vector>
namespace iw::overlay::tests {
// Missing initial HUDs affect adaptive help without disabling its X and width rules
void AddInitialOpenLayoutChecks(std::vector<std::pair<std::string, std::function<void()>>>& checks);
}
