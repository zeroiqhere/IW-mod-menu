#pragma once
#include <string>
#include <vector>

namespace iw::overlay {
// The controller owns progress while native presentation owns the fixed source catalog
struct MenuCredits {
    int elapsedMilliseconds = 0;
    // Runtime replaces this single action label with the exact saved melee keys
    std::vector<std::string> instructions{"Melee: Exit Menu Credits"};
    bool operator==(const MenuCredits&) const = default;
};
}
