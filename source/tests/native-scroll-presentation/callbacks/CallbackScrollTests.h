#pragma once
#include <functional>
#include <string>
#include <utility>
#include <vector>
void AddCallbackScrollChecks(std::vector<std::pair<std::string, std::function<void()>>>& checks);
