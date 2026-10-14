#include "NativeInputRouter.h"

#include <array>
#include <iostream>
#include <set>
#include <stdexcept>

using namespace iw::overlay;
using namespace iw::overlay::runtime;

namespace {
void Require(bool condition, const char* message) {
    if (!condition) throw std::runtime_error(message);
}

// The real router reads only the fixture's keys, clock and copied menu receipt
struct MenuKeys {
    Bindings bindings{{2}, {86}, {2, 38}, {1, 40}, {70}, {51}, {52}};
    NativeInputRouter router;
    std::set<int> held;
    std::uint64_t now = 0, navigation = 1;
    int duration = 120;
    bool focused = true, pending = false, quickExit = false, root = false;
    MenuSurface surface = MenuSurface::Main;
    LocalMenuEligibility eligibility{10, PlayerLifeState::Alive, true, false, false};

    MenuKeys() : router(MakeBindings()) {}
    Bindings MakeBindings() {
        bindings.jump = {32};
        return bindings;
    }
    auto Poll() {
        return router.Poll(focused, true, pending, [&](int key) { return held.contains(key); },
            now, navigation, quickExit, root, InputMode::Menu, duration, 1,
            nullptr, 0, 0, nullptr, &eligibility, surface);
    }
};

void DueScrollingPrecedesFreshSelectWithoutReplayingTheUsePress() {
    for (int key : {2, 1}) for (int duration : {100, 120, 250}) {
        MenuKeys keys;keys.duration = duration;keys.Poll();keys.held = {key};
        const auto expected = key == 2 ? Input::Up : Input::Down;
        Require(keys.Poll() == expected, "The initial source scroll did not start");
        keys.now = duration + 75;keys.held.insert(70);
        Require(keys.Poll() == expected, "Fresh Use overtook the due source scroll branch");
        Require(!keys.Poll(), "A discarded Use press was queued behind scrolling");
        keys.held = {70};keys.now += duration + 75;
        Require(!keys.Poll(), "Held discarded Use replayed after scroll release");
    }
}

void FreshScrollingPrecedesKnifeInBothMenuFamilies() {
    for (auto family : {MenuSurface::Main, MenuSurface::Quick}) for (int key : {2, 1}) {
        MenuKeys keys;keys.surface = family;keys.Poll();keys.held = {key, 86};
        Require(keys.Poll() == (key == 2 ? Input::Up : Input::Down),
            "Knife overtook a fresh source scroll branch");
        keys.now = 195;keys.held = {86};
        Require(!keys.Poll(), "Knife discarded by scrolling became a delayed Back");
    }
}

void DueScrollingPrecedesKnifeWithoutStartingQuickExit() {
    for (bool quick : {false, true}) {
        MenuKeys keys;keys.quickExit = quick;keys.Poll();keys.held = {2};keys.Poll();
        keys.now = 195;keys.held.insert(86);
        Require(keys.Poll() == Input::Up, "Knife swallowed due scrolling");
        keys.now = 285;
        Require(!keys.Poll(), "A rejected knife branch started an invisible Quick Exit");
    }
}

void SelectPrecedesFreshKnifeAndChoice() {
    for (bool quick : {false, true}) {
        MenuKeys keys;keys.quickExit = quick;keys.Poll();keys.held = {70, 86, 51};
        Require(keys.Poll() == Input::Select, "Knife or slider overtook source Use");
        keys.now = 1000;
        Require(!keys.Poll(), "Lower-priority held controls replayed after Use");
    }
}

void FreshChoicePrecedesKnife() {
    for (int key : {51, 52}) for (bool quick : {false, true}) {
        MenuKeys keys;keys.quickExit = quick;keys.Poll();keys.held = {key, 86};
        Require(keys.Poll() == (key == 51 ? Input::PreviousChoice : Input::NextChoice),
            "Knife overtook a fresh source slider branch");
        keys.held = {86};keys.now = 1000;
        Require(!keys.Poll(), "Discarded slider-time knife became delayed Back");
    }
}

void DueChoicePrecedesFreshKnife() {
    for (int key : {51, 52}) {
        MenuKeys keys;keys.Poll();keys.held = {key};keys.Poll();
        keys.now = 180;keys.held.insert(86);
        Require(keys.Poll() == (key == 51 ? Input::PreviousChoice : Input::NextChoice),
            "Fresh knife overtook a due source slider branch");
    }
}

void NeutralScrollingStillAllowsUseBeforeKnife() {
    MenuKeys keys;keys.Poll();keys.held = {2, 1, 70, 86};
    Require(keys.Poll() == Input::Select, "Neutral scrolling did not advance to source Use");
}

void DeadQuickJumpFollowsUseAndSliderBranches() {
    for (int key : {70, 51, 52}) {
        MenuKeys keys;keys.surface = MenuSurface::Quick;keys.eligibility.life = PlayerLifeState::Dead;
        keys.Poll();keys.held = {32, key};
        const auto expected = key == 70 ? Input::Select : key == 51 ? Input::PreviousChoice : Input::NextChoice;
        Require(keys.Poll() == expected, "Dead Jump overtook a preceding source branch");
    }
}

void AnAdmittedQuickExitRemainsExclusiveThroughItsSourceLoop() {
    MenuKeys keys;keys.quickExit = true;keys.Poll();keys.held = {86};
    Require(!keys.Poll(), "Quick Exit failed to enter its own source loop");
    keys.now = 40;keys.held = {86, 2, 70, 51};
    Require(!keys.Poll(), "Ordinary input escaped an admitted knife hold");
    keys.now = 90;
    Require(keys.Poll() == Input::Close, "The admitted source knife hold did not close once");
    keys.now = 1000;
    Require(!keys.Poll(), "Completed knife hold allowed stale lower-priority keys");
}

void DeadQuickJumpAndKnifeShareTheEnabledChildHold() {
    MenuKeys keys;keys.surface = MenuSurface::Quick;keys.eligibility.life = PlayerLifeState::Dead;
    keys.quickExit = true;keys.Poll();keys.held = {32, 86};
    Require(!keys.Poll(), "Combined Jump and knife bypassed the source Quick Exit hold");
    keys.now = 89;Require(!keys.Poll(), "The combined hold closed before its source threshold");
    keys.now = 90;Require(keys.Poll() == Input::Close, "The combined source hold failed to close");
    keys.now = 1000;Require(!keys.Poll(), "The combined hold closed more than once");
}

void DeadQuickJumpAndKnifeShortReleaseReturnsOnePage() {
    MenuKeys keys;keys.surface = MenuSurface::Quick;keys.eligibility.life = PlayerLifeState::Dead;
    keys.quickExit = true;keys.Poll();keys.held = {32, 86};
    Require(!keys.Poll(), "The combined source tap navigated before knife release");
    keys.now = 50;keys.held = {32};
    Require(keys.Poll() == Input::Back, "A short combined source tap did not return one page");
    keys.now = 1000;Require(!keys.Poll(), "The held Jump replayed after the completed tap");
}

void DeadQuickJumpAloneKeepsImmediateScopedBack() {
    for (bool quick : {false, true}) for (bool root : {false, true}) {
        MenuKeys keys;keys.surface = MenuSurface::Quick;keys.eligibility.life = PlayerLifeState::Dead;
        keys.quickExit = quick;keys.root = root;keys.Poll();keys.held = {32};
        Require(keys.Poll() == Input::JumpBack, "Jump alone started a nonexistent knife hold");
        keys.now = 1000;Require(!keys.Poll(), "Held Jump repeated its scoped Back");
    }
}

void DeadQuickRootOrDisabledHoldKeepsImmediateScopedBack() {
    for (bool quick : {false, true}) for (bool root : {false, true}) {
        if (quick && !root) continue;
        MenuKeys keys;keys.surface = MenuSurface::Quick;keys.eligibility.life = PlayerLifeState::Dead;
        keys.quickExit = quick;keys.root = root;keys.Poll();keys.held = {32, 86};
        Require(keys.Poll() == Input::JumpBack, "Root or disabled Quick Exit delayed scoped Back");
    }
}

void FreshDeadQuickJumpCanAdmitAHoldAfterAnEarlierKnifeWasDiscarded() {
    MenuKeys keys;keys.surface = MenuSurface::Quick;keys.eligibility.life = PlayerLifeState::Dead;
    keys.quickExit = true;keys.Poll();keys.held = {70, 86};
    Require(keys.Poll() == Input::Select, "The setup must consume knife under source Use");
    keys.now = 500;keys.held = {86};Require(!keys.Poll(), "Discarded knife silently rearmed");
    // The new Jump edge supplies a new Back intent under the current dead Quick receipt
    keys.now = 700;keys.held.insert(32);
    Require(!keys.Poll(), "Fresh Jump failed to enter the source combined knife hold");
    keys.now = 790;Require(keys.Poll() == Input::Close, "The freshly admitted combined hold did not close");
}

void FocusAndPendingAdmissionDiscardSimultaneousActions() {
    for (bool focusLoss : {false, true}) {
        MenuKeys keys;keys.Poll();keys.held = {2, 70, 86, 51};
        keys.pending = !focusLoss;keys.focused = !focusLoss;
        Require(!keys.Poll(), "Blocked owner dispatched a branch");
        keys.pending = false;keys.focused = true;keys.now = 1000;
        Require(!keys.Poll(), "Blocked simultaneous actions replayed after recovery");
        keys.now = 2000;
        Require(!keys.Poll(), "Blocked holds silently rearmed");
        keys.held.clear();keys.Poll();keys.held = {70};
        Require(keys.Poll() == Input::Select, "A deliberate released Use failed after recovery");
    }
}
}

int main() {
    struct Test { const char* name; void (*run)(); };
    const std::array tests{
        Test{"DueScrollingPrecedesFreshSelectWithoutReplayingTheUsePress", DueScrollingPrecedesFreshSelectWithoutReplayingTheUsePress},
        Test{"FreshScrollingPrecedesKnifeInBothMenuFamilies", FreshScrollingPrecedesKnifeInBothMenuFamilies},
        Test{"DueScrollingPrecedesKnifeWithoutStartingQuickExit", DueScrollingPrecedesKnifeWithoutStartingQuickExit},
        Test{"SelectPrecedesFreshKnifeAndChoice", SelectPrecedesFreshKnifeAndChoice},
        Test{"FreshChoicePrecedesKnife", FreshChoicePrecedesKnife},
        Test{"DueChoicePrecedesFreshKnife", DueChoicePrecedesFreshKnife},
        Test{"NeutralScrollingStillAllowsUseBeforeKnife", NeutralScrollingStillAllowsUseBeforeKnife},
        Test{"DeadQuickJumpFollowsUseAndSliderBranches", DeadQuickJumpFollowsUseAndSliderBranches},
        Test{"AnAdmittedQuickExitRemainsExclusiveThroughItsSourceLoop", AnAdmittedQuickExitRemainsExclusiveThroughItsSourceLoop},
        Test{"DeadQuickJumpAndKnifeShareTheEnabledChildHold", DeadQuickJumpAndKnifeShareTheEnabledChildHold},
        Test{"DeadQuickJumpAndKnifeShortReleaseReturnsOnePage", DeadQuickJumpAndKnifeShortReleaseReturnsOnePage},
        Test{"DeadQuickJumpAloneKeepsImmediateScopedBack", DeadQuickJumpAloneKeepsImmediateScopedBack},
        Test{"DeadQuickRootOrDisabledHoldKeepsImmediateScopedBack", DeadQuickRootOrDisabledHoldKeepsImmediateScopedBack},
        Test{"FreshDeadQuickJumpCanAdmitAHoldAfterAnEarlierKnifeWasDiscarded", FreshDeadQuickJumpCanAdmitAHoldAfterAnEarlierKnifeWasDiscarded},
        Test{"FocusAndPendingAdmissionDiscardSimultaneousActions", FocusAndPendingAdmissionDiscardSimultaneousActions}
    };
    int failures = 0;
    for (const auto& test : tests) {
        try { test.run();std::cout << "PASS " << test.name << '\n'; }
        catch (const std::exception& error) { ++failures;std::cerr << "FAIL " << test.name << ": " << error.what() << '\n'; }
    }
    std::cout << tests.size() - failures << " source monitor priority checks passed; " << failures << " failed\n";
    return failures == 0 ? 0 : 1;
}
