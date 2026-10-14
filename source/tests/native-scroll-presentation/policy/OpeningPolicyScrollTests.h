#pragma once
#include <functional>
#include <string>
#include <utility>
#include <vector>

// These checks keep policy evidence separate from ordinary row fading
void AddOpeningPolicyScrollChecks(std::vector<std::pair<std::string, std::function<void()>>>& checks);
