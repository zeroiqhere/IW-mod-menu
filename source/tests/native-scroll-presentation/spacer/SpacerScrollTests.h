#pragma once
#include <functional>
#include <string>
#include <utility>
#include <vector>

// Each check runs the real presentation owner with an explicit native clock
void AddSpacerScrollChecks(std::vector<std::pair<std::string, std::function<void()>>>& checks);
