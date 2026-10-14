#include "../../native/render/opening/OpeningPresentationClock.h"
#include <cmath>
#include <functional>
#include <iostream>
#include <limits>
#include <stdexcept>
#include <string>
#include <vector>

namespace {
using iw::overlay::opening::PresentationClock;
void Equal(double actual, double expected) {
    if (std::abs(actual - expected) > .001) throw std::runtime_error("The bounded opening clock differs from its owner report");
}
}
int main() {
    std::vector<std::pair<std::string, std::function<void()>>> checks;
    checks.emplace_back("A late initial report starts at owner elapsed then advances locally", [] {
        PresentationClock clock; Equal(clock.Observe(400, 1, 3, 0, 900), 400); Equal(clock.Sample(925), 425);
    });
    checks.emplace_back("Repeated stale reports saturate at one hundred milliseconds of lead", [] {
        PresentationClock clock; clock.Observe(40, 1, 3, 0, 100);
        Equal(clock.Observe(40, 1, 3, 0, 200), 140); Equal(clock.Observe(40, 1, 3, 0, 5000), 140);
    });
    checks.emplace_back("Increasing reports do not rewind an already displayed bounded phase", [] {
        PresentationClock clock; clock.Observe(0, 1, 3, 0, 100); Equal(clock.Sample(180), 80);
        Equal(clock.Observe(40, 1, 3, 0, 180), 80); Equal(clock.Sample(240), 140);
    });
    checks.emplace_back("Observed elapsed rollback restarts from the new owner report", [] {
        PresentationClock clock; clock.Observe(400, 1, 3, 0, 100);
        Equal(clock.Observe(20, 1, 3, 0, 125), 20); Equal(clock.Sample(150), 45);
    });
    checks.emplace_back("Event menu and observed focus changes each reset the local anchor", [] {
        PresentationClock clock; clock.Observe(100, 1, 3, 0, 0);
        Equal(clock.Observe(200, 2, 3, 0, 100), 200);
        Equal(clock.Observe(300, 2, 4, 0, 200), 300);
        Equal(clock.Observe(400, 2, 4, 1, 300), 400);
    });
    checks.emplace_back("Clock rollback and maximum unsigned deltas cannot wrap or overrun the source bound", [] {
        PresentationClock clock; clock.Observe(1250, 1, 3, 0, 100);
        Equal(clock.Sample(std::numeric_limits<std::uint64_t>::max()), 1300);
        Equal(clock.Observe(1250, 1, 3, 0, 90), 1250); Equal(clock.Sample(89), 1250);
    });
    checks.emplace_back("Readonly samples before the last observed clock fall back to owner elapsed", [] {
        PresentationClock clock; clock.Observe(20, 1, 3, 0, 100); clock.Observe(20, 1, 3, 0, 200);
        Equal(clock.Sample(150), 20); Equal(clock.Sample(200), 120);
    });
    checks.emplace_back("Legacy missing identities retain reported timing without local motion", [] {
        PresentationClock clock; Equal(clock.Observe(30, 1, std::nullopt, 0, 100), 30); Equal(clock.Sample(200), 30);
        Equal(clock.Observe(40, 1, 3, std::nullopt, 300), 40); Equal(clock.Sample(400), 40);
    });
    checks.emplace_back("Invalid elapsed and zero event fail closed and reset old phase", [] {
        PresentationClock clock; clock.Observe(100, 1, 3, 0, 100);
        Equal(clock.Observe(-1, 1, 3, 0, 200), 0); Equal(clock.Sample(300), 0);
        Equal(clock.Observe(1301, 1, 3, 0, 300), 0); Equal(clock.Observe(50, 0, 3, 0, 400), 0);
        clock.Reset(); Equal(clock.Sample(500), 0);
    });
    unsigned passed = 0;
    for (const auto& [name, check] : checks) {
        try { check(); ++passed; std::cout << "PASS " << name << '\n'; }
        catch (const std::exception& error) { std::cerr << "FAIL " << error.what() << '\n'; return 1; }
    }
    std::cout << "PASS " << passed << " bounded opening clock checks\n";
}
