#include "../closed-level-opening/ClosedLevelOpeningGraphSupport.h"
#include <iostream>

namespace iw::overlay::tests::ordinaryLevels {
using closedLevelGraph::Read;
using closedLevelGraph::Router;

void DeadJumpHeldThroughQuickOpeningRunsAtItsFirstReadyCycle(const std::filesystem::path& directory) {
    Router router;
    router.held = {2, 32};
    Check(router.Poll(Read(directory / "dead-before0.json"), 0) == runtime::Input::OpenQuick,
        "Saved Aim and Jump did not open the actual dead Quick predecessor");
    router.pending = true;
    Check(!router.Poll(Read(directory / "dead-pending0.json"), 0), "Pending dead opening emitted ordinary input");
    // Jump remains held throughout opening, while Aim releases to remove higher-priority scrolling
    router.pending = false;
    router.held = {32};
    Check(!router.Poll(Read(directory / "dead-drawn0.json"), 0), "Dead Quick draw skipped its caller pause");
    Check(!router.Poll(Read(directory / "dead-wait549.json"), 549), "Held Jump bypassed Quick's complete550 wait");
    const auto ready = Read(directory / "dead-ready550.json");
    Check(ready.snapshot->isOpen && ready.snapshot->menuSurface == MenuSurface::Quick
        && ready.snapshot->localMenuEligibility->life == PlayerLifeState::Dead
        && ready.snapshot->localMenuEligibility->controlsDisabled == false
        && !ready.snapshot->localMenuEligibility->blocked, "Actual ready packet did not independently permit dead input");
    Check(router.Poll(ready, 550) == runtime::Input::JumpBack,
        "Held Jump required a release or fresh edge after actual Quick readiness");
}
void UseHeldDuringQuickDrawRunsAfterReadinessWithoutASecondPress(const std::filesystem::path& directory) {
    Router router;
    router.held = {2, 69};
    Check(router.Poll(Read(directory / "quick-before0.json"), 0) == runtime::Input::OpenQuick,
        "Actual living Quick did not open from its saved chord");
    router.pending = true;
    Check(!router.Poll(Read(directory / "quick-pending0.json"), 0), "Pending living opening emitted Use");
    // Use is sampled during the locked draw and remains held at first readiness
    router.pending = false;
    router.held = {70};
    Check(!router.Poll(Read(directory / "quick-drawn0.json"), 0), "Quick draw skipped its caller pause");
    Check(!router.Poll(Read(directory / "quick-wait549.json"), 549), "Held Use bypassed Quick550");
    const auto ready = Read(directory / "quick-ready550.json");
    Check(ready.snapshot->localMenuEligibility->life == PlayerLifeState::Alive
        && ready.snapshot->localMenuEligibility->hasMenu == true
        && ready.snapshot->localMenuEligibility->controlsDisabled == false
        && !ready.snapshot->localMenuEligibility->blocked, "Actual ready packet did not independently permit living input");
    Check(router.Poll(ready, 550) == runtime::Input::Select,
        "Held Use required a second press after actual Quick readiness");
}
}

int main(int argc, char** argv) {
    using namespace iw::overlay::tests::ordinaryLevels;
    if (argc != 2) return 2;
    const std::filesystem::path directory(argv[1]);
    int failed = 0;
    // Both checks exercise production readers, router and immutable actual producer packets
    for (const auto& [name, run] : {
        std::pair{"DeadJumpHeldThroughQuickOpeningRunsAtItsFirstReadyCycle", DeadJumpHeldThroughQuickOpeningRunsAtItsFirstReadyCycle},
        std::pair{"UseHeldDuringQuickDrawRunsAfterReadinessWithoutASecondPress", UseHeldDuringQuickDrawRunsAfterReadinessWithoutASecondPress}}) {
        try { run(directory); std::cout << "PASS " << name << '\n'; }
        catch (const std::exception& error) { ++failed; std::cout << "FAIL " << name << ": " << error.what() << '\n'; }
    }
    std::cout << 2 - failed << "/2 ordinary opening hold checks passed\n";
    return failed ? 1 : 0;
}
