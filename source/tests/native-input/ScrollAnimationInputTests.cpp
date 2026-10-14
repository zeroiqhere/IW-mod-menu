#include "NativeInputRouter.h"
#include <iostream>
#include <limits>
#include <set>
#include <stdexcept>

using namespace iw::overlay::runtime;
namespace {
void Require(bool condition, const char* message) {
    if (!condition) throw std::runtime_error(message);
}
// The fixture owns saved keys and time while using the production router
struct Keys {
    NativeInputRouter router{Bindings{{2}, {86}, {2, 38}, {1, 40}, {70}, {51}, {52}}};
    std::set<int> held;
    std::uint64_t now = 0, navigation = 1;
    std::optional<std::uint64_t> menu = 1;
    int duration = 120;
    bool focused = true, open = true, pending = false;
    InputMode mode = InputMode::Menu;
    auto Poll() { return router.Poll(focused, open, pending, [&](int key) { return held.contains(key); },
        now, navigation, false, false, mode, duration, menu); }
};
void SupportedDurationsCaptureTheirCompleteSourceWait() {
    for (int duration : {100, 120, 250}) {
        Keys keys; keys.duration = duration; keys.Poll(); keys.held = {2};
        Require(keys.Poll() == Input::Up, "Fresh scrolling failed");
        keys.now = duration + 74; Require(!keys.Poll(), "Scroll repeated before its captured complete wait");
        keys.now = duration + 75; Require(keys.Poll() == Input::Up, "Configured source interval was ignored");
        keys.now = 10000; Require(keys.Poll() == Input::Up, "A delayed poll lost its one repeat");
        Require(!keys.Poll(), "A delayed poll caught up in a burst");
    }
}
void CommittingDurationDoesNotRetargetAnExistingWait() {
    Keys keys; keys.duration = 250; keys.Poll(); keys.held = {2}; keys.Poll();
    keys.duration = 100; keys.now = 175; Require(!keys.Poll(), "A shorter commit cut an existing wait");
    keys.now = 325; Require(keys.Poll() == Input::Up, "The captured old wait lost its deadline");
    keys.duration = 250; keys.now = 499; Require(!keys.Poll(), "The next wait used a retroactive longer duration");
    keys.now = 500; Require(keys.Poll() == Input::Up, "The next scroll did not capture the shorter committed duration");
    keys.now = 824; Require(!keys.Poll(), "The latest captured longer wait was shortened");
    keys.now = 825; Require(keys.Poll() == Input::Up, "The longer commit did not apply to its next wait");
}
void CooldownSurvivesReleaseAndConsumesEveryOtherFreshAction() {
    for (int key : {1, 70, 86, 51, 52}) {
        Keys keys; keys.Poll(); keys.held = {2}; keys.Poll();
        // Releasing navigation cannot bypass the source monitor's already-started wait
        keys.held.clear(); keys.now = 10; Require(!keys.Poll(), "Release emitted an action");
        keys.held = {key}; keys.now = 20; Require(!keys.Poll(), "A fresh action bypassed scroll cooldown");
        keys.now = 195; Require(!keys.Poll(), "A consumed cooldown press replayed at its deadline");
        keys.now = 1000; Require(!keys.Poll(), "A consumed cooldown hold rearmed later");
        keys.held.clear(); keys.Poll(); keys.held = {key};
        const auto expected = key == 1 ? Input::Down : key == 70 ? Input::Select : key == 86 ? Input::Back
            : key == 51 ? Input::PreviousChoice : Input::NextChoice;
        Require(keys.Poll() == expected, "A fresh action failed after cooldown and release");
    }
}
void DueSourceScrollAtDeadlinePrecedesFreshSelect() {
    Keys keys; keys.Poll(); keys.held = {2}; keys.Poll(); keys.now = 195; keys.held.insert(70);
    Require(keys.Poll() == Input::Up, "Fresh Select overtook the due source scroll");
    Require(!keys.Poll(), "The consumed Use press replayed on the same poll");
    keys.now = 390; Require(keys.Poll() == Input::Up, "The held source scroll lost its next interval");
}
void ConsumedOtherActionHoldsDoNotBlockEstablishedNavigationAtDeadline() {
    for (int key : {70, 86, 51, 52}) {
        Keys keys; keys.Poll(); keys.held = {2}; keys.Poll();
        keys.now = 50; keys.held.insert(key);
        Require(!keys.Poll(), "Another fresh action escaped the scroll wait");
        // A consumed edge cannot steal ownership from the already-established navigation hold
        keys.now = 195;
        Require(keys.Poll() == Input::Up, "A consumed action hold blocked established scrolling");
    }
}
void PendingRebasesCapturedWaitWithoutChangingDurationOrReplayingEdges() {
    Keys keys; keys.duration = 250; keys.Poll(); keys.held = {2}; keys.Poll();
    keys.duration = 100; keys.pending = true; keys.now = 200; keys.held.insert(70); keys.Poll();
    keys.pending = false; keys.now = 524; Require(!keys.Poll(), "Acknowledgement shortened the captured pending wait");
    keys.now = 525; Require(keys.Poll() == Input::Up, "Pending rebasing failed to preserve the existing navigation hold");
    Require(!keys.Poll(), "Consumed pending Select replayed after acknowledgement");
}
void MenuIdentityChangesReseedButCursorRevisionsKeepHeldCadence() {
    Keys keys; keys.Poll(); keys.held = {2}; keys.Poll(); ++keys.navigation;
    keys.now = 195; Require(keys.Poll() == Input::Up, "A cursor revision cancelled held scrolling");
    ++*keys.menu; keys.now = 200; Require(!keys.Poll(), "A new menu inherited the old held scroll");
    keys.now = 1000; Require(!keys.Poll(), "New menu context rearmed before release");
    keys.held.clear(); keys.Poll(); keys.held = {2}; Require(keys.Poll() == Input::Up, "Fresh navigation failed in the new menu");
}
void FocusOpenAndModeTransitionsDiscardInheritedCooldownHolds() {
    for (int transition = 0; transition < 3; ++transition) {
        Keys keys; keys.Poll(); keys.held = {2}; keys.Poll(); keys.now = 10;
        if (transition == 0) keys.focused = false;
        if (transition == 1) keys.open = false;
        if (transition == 2) keys.mode = InputMode::MenuLocked;
        Require(!keys.Poll(), "A replaced input context dispatched during cooldown");
        keys.focused = true; keys.open = true; keys.mode = InputMode::Menu; keys.now = 1000; keys.Poll();
        Require(!keys.Poll(), "Context return replayed held navigation");
        keys.held.clear(); keys.Poll(); keys.held = {2}; Require(keys.Poll() == Input::Up, "Context release did not recover navigation");
    }
}
void RollbackCancelsCooldownEvenWhenTheNavigationKeyWasReleased() {
    Keys keys; keys.now = 100; keys.Poll(); keys.held = {2}; keys.Poll();
    keys.held.clear(); keys.now = 110; keys.Poll(); keys.now = 99; keys.held = {70};
    Require(!keys.Poll(), "Rollback replayed a fresh cooldown action");
    keys.now = 1000; Require(!keys.Poll(), "Rollback rearmed the consumed action hold");
    keys.held.clear(); keys.Poll(); keys.held = {70}; Require(keys.Poll() == Input::Select, "Release after rollback failed to recover");
}
void NearLimitTimesUseElapsedSubtractionWithoutDeadlineOverflow() {
    Keys keys; keys.now = std::numeric_limits<std::uint64_t>::max() - 195; keys.Poll(); keys.held = {2}; keys.Poll();
    keys.now = std::numeric_limits<std::uint64_t>::max() - 1; Require(!keys.Poll(), "Near-limit cooldown expired early");
    keys.now = std::numeric_limits<std::uint64_t>::max(); Require(keys.Poll() == Input::Up, "Near-limit repeat deadline overflowed");
}
void InvalidDirectDurationRejectsBeforeKeyReadsAndReseedsOnRecovery() {
    for (int duration : {-1, 0, 99, 101, 251, std::numeric_limits<int>::max()}) {
        Keys keys; keys.Poll(); keys.held = {2}; keys.Poll();
        int reads = 0; bool rejected = false;
        try { keys.router.Poll(true, true, false, [&](int) { ++reads; return true; }, 1000, 1, false,
            false, InputMode::Menu, duration, 1); }
        catch (const std::invalid_argument&) { rejected = true; }
        Require(rejected && reads == 0, "An invalid direct duration reached key IO or was accepted");
        keys.now = 1000; Require(!keys.Poll(), "Invalid configuration retained an armed repeat");
    }
}
void LegacyCallsKeepDefaultDurationAndMissingMenuIdentity() {
    NativeInputRouter router(Bindings{{2}, {86}, {2}, {1}, {70}});
    bool held = false;
    router.Poll(true, true, false, [&](int) { return held; }, 0);
    held = true;
    const auto reader = [&](int key) { return key == 2 && held; };
    Require(router.Poll(true, true, false, reader, 0) == Input::Up, "Legacy callers lost fresh navigation");
    Require(!router.Poll(true, true, false, reader, 194), "Legacy repeat default changed");
    Require(router.Poll(true, true, false, reader, 195) == Input::Up, "Legacy default interval was not retained");
}
}
int main() {
    try {
        SupportedDurationsCaptureTheirCompleteSourceWait();
        CommittingDurationDoesNotRetargetAnExistingWait();
        CooldownSurvivesReleaseAndConsumesEveryOtherFreshAction();
        DueSourceScrollAtDeadlinePrecedesFreshSelect();
        ConsumedOtherActionHoldsDoNotBlockEstablishedNavigationAtDeadline();
        PendingRebasesCapturedWaitWithoutChangingDurationOrReplayingEdges();
        MenuIdentityChangesReseedButCursorRevisionsKeepHeldCadence();
        FocusOpenAndModeTransitionsDiscardInheritedCooldownHolds();
        RollbackCancelsCooldownEvenWhenTheNavigationKeyWasReleased();
        NearLimitTimesUseElapsedSubtractionWithoutDeadlineOverflow();
        InvalidDirectDurationRejectsBeforeKeyReadsAndReseedsOnRecovery();
        LegacyCallsKeepDefaultDurationAndMissingMenuIdentity();
        std::cout << "Passed 12 scroll-animation input checks\n";
        return 0;
    } catch (const std::exception& error) {
        std::cerr << "FAIL " << error.what() << '\n';
        return 1;
    }
}
