#include "../../native/render/credits/CreditsPresentationClock.h"
#include <cmath>
#include <functional>
#include <iostream>
#include <limits>
#include <stdexcept>
#include <string>
#include <vector>

namespace {
using namespace iw::overlay::credits;
void Require(bool value, const char* reason) { if (!value) throw std::runtime_error(reason); }
void Equal(std::optional<double> actual, double expected) {
    Require(actual && std::abs(*actual - expected) < .001, "Reported or interpolated credits timing differs");
}
}
int main() {
    std::vector<std::pair<std::string, std::function<void()>>> checks;
    checks.emplace_back("Initial late entry anchors to authoritative elapsed instead of restarting credits", [] {
        PresentationClock clock; Equal(clock.Update(12000, 3, 0, 500), 12000);
        Equal(clock.Update(12000, 3, 0, 550), 12050);
    });
    checks.emplace_back("Repeated stale reports saturate at one heartbeat without running indefinitely", [] {
        PresentationClock clock; clock.Update(1000, 3, 0, 0);
        Equal(clock.Update(1000, 3, 0, 50), 1050); Equal(clock.Update(1000, 3, 0, 100), 1100);
        Equal(clock.Update(1000, 3, 0, 1000000), 1100);
    });
    checks.emplace_back("Increasing delayed reports rebase without rewinding the displayed phase", [] {
        PresentationClock clock; clock.Update(1000, 3, 0, 0); Equal(clock.Update(1000, 3, 0, 80), 1080);
        Equal(clock.Update(1050, 3, 0, 90), 1090); Equal(clock.Update(1050, 3, 0, 110), 1110);
        Equal(clock.Update(1300, 3, 0, 120), 1300);
    });
    checks.emplace_back("Observed elapsed rollback and local clock rollback snap to the latest report", [] {
        PresentationClock clock; clock.Update(1000, 3, 0, 100); clock.Update(1000, 3, 0, 150);
        Equal(clock.Update(900, 3, 0, 160), 900); Equal(clock.Update(900, 3, 0, 120), 900);
    });
    checks.emplace_back("Changed menu and observed focus identities cannot inherit previous phase interpolation", [] {
        PresentationClock clock; clock.Update(1000, 3, 0, 0); Equal(clock.Update(1000, 4, 0, 50), 1000);
        Equal(clock.Update(1000, 4, 1, 80), 1000); Equal(clock.Update(1000, 4, 1, 100), 1020);
    });
    checks.emplace_back("Missing legacy identities return exact reports and cannot seed future interpolation", [] {
        PresentationClock clock; clock.Update(1000, 3, 0, 0);
        Equal(clock.Update(1000, std::nullopt, 0, 50), 1000); Equal(clock.Update(1000, 3, std::nullopt, 80), 1000);
        Equal(clock.Update(1000, 3, 0, 100), 1000);
    });
    checks.emplace_back("Large unsigned deltas and wrap remain bounded before subtraction", [] {
        PresentationClock clock; clock.Update(1000, 3, 0, 0);
        Equal(clock.Update(1000, 3, 0, std::numeric_limits<std::uint64_t>::max()), 1100);
        Equal(clock.Update(1000, 3, 0, 0), 1000);
    });
    checks.emplace_back("End bounds and invalid reports never invent the authoritative forty-second exit", [] {
        PresentationClock clock; Equal(clock.Update(39950, 3, 0, 0), 39950);
        Equal(clock.Update(39950, 3, 0, 500), 39999);
        Require(!clock.Update(40000, 3, 0, 600) && !clock.Update(-1, 3, 0, 700), "Invalid credits reports were admitted");
        Equal(clock.Update(1200, 3, 0, 800), 1200);
    });
    checks.emplace_back("Explicit renderer reset discards all old interpolation state", [] {
        PresentationClock clock; clock.Update(1000, 3, 0, 0); clock.Reset();
        Equal(clock.Update(1000, 3, 0, 50), 1000);
    });
    unsigned passed = 0;
    for (const auto& [name, check] : checks) {
        try { check(); ++passed; std::cout << "PASS " << name << '\n'; }
        catch (const std::exception& error) { std::cerr << "FAIL " << name << ": " << error.what() << '\n'; return 1; }
    }
    std::cout << "PASS " << passed << " pure credits clock checks\n";
}
