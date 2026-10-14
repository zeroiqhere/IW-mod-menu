#include "ClosedSourceCadenceSupport.h"
#include <iostream>
#include <string_view>
#include <utility>
#include <vector>

namespace {
std::vector<std::pair<std::string_view, void (*)()>> checks;
void Add(const char* name, void (*run)()) { checks.emplace_back(name, run); }
}
int main() {
    using namespace iw::overlay::runtime::tests::closedCadence;
    // Every case uses the production router without launching a game or reading desktop keys
    RegisterTimingChecks(Add);
    RegisterPauseChecks(Add);
    RegisterRecoveryChecks(Add);
    int failed = 0;
    for (const auto& [name, run] : checks) {
        try { run(); std::cout << "PASS " << name << '\n'; }
        catch (const std::exception& error) {
            ++failed;
            std::cout << "FAIL " << name << ": " << error.what() << '\n';
        }
    }
    // A failed logical cycle rejects the complete runner while preserving each case result
    std::cout << checks.size() - failed << '/' << checks.size() << " closed source cadence checks passed\n";
    return failed ? 1 : 0;
}
