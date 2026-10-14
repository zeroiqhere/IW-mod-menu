#pragma once
#include <functional>
#include <stdexcept>
#include <string>
#include <utility>
#include <vector>
namespace iw::overlay::tests {
using Checks = std::vector<std::pair<std::string, std::function<void()>>>;
inline void Require(bool condition, const char *message) {
    if (!condition) {
        throw std::runtime_error(message);
    }
}
void AddRowLayoutChecks(Checks &);
void AddRowLabelChecks(Checks &);
void AddRowFontChecks(Checks &);
} // namespace iw::overlay::tests
