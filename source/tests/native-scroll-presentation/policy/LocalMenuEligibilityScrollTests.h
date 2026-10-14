#pragma once
#include <functional>
#include <string>
#include <vector>
void AddLocalMenuEligibilityScrollChecks(std::vector<std::pair<std::string, std::function<void()>>>& checks);
