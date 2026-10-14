#include "NativeInputRouter.h"

#include <iostream>
#include <limits>
#include <set>
#include <stdexcept>

using iw::overlay::runtime::Bindings;
using iw::overlay::runtime::Input;
using iw::overlay::runtime::NativeInputRouter;

namespace {
// A small key source checks real routing without window or game access
struct Keys {
    NativeInputRouter router{Bindings{{2}, {86}, {2, 38}, {1, 40}, {70}}};
    std::set<int> held;
    bool allowed = true;
    bool pending = false;
    std::uint64_t now = 0;
    auto Poll() { return router.Poll(allowed, true, pending,
        [&](int key) { return held.contains(key); }, now); }
};
void Require(bool value, const char* message) {
    if (!value) throw std::runtime_error(message);
}
void OpposingDirectionsDoNotMoveTheCursor() {
    Keys keys;
    keys.Poll();
    keys.held = {2, 1};
    Require(!keys.Poll(), "Aim and fire together must not scroll");
    Require(!keys.Poll(), "Opposing held directions must remain neutral");
}
void OpposingDirectionsStillAllowSelection() {
    Keys keys;
    keys.Poll();
    keys.held = {2, 1, 70};
    Require(keys.Poll() == Input::Select, "Neutral directions must allow a fresh select");
    Require(!keys.Poll(), "A held select must not repeatedly toggle features");
}
void HeldScrollRepeatsAtTheReferenceInterval() {
    Keys keys;
    keys.Poll();
    keys.held = {2};
    Require(keys.Poll() == Input::Up, "A fresh aim must scroll up");
    keys.now = 145;
    Require(!keys.Poll(), "The scroll branch alone must not skip the source monitor wait");
    keys.now = 194;
    Require(!keys.Poll(), "Scrolling repeated before its reference interval");
    keys.now = 195;
    Require(keys.Poll() == Input::Up, "Held aim must repeat after the full 195ms source cycle");
    keys.now = 390;
    Require(keys.Poll() == Input::Up, "Holding aim must keep scrolling");
    keys.now = 5000;
    Require(keys.Poll() == Input::Up, "A slow poll must emit one scroll");
    Require(!keys.Poll(), "A delayed poll must not emit a catch-up burst");
}
void PendingCommandsAndFocusLossDoNotReplayInput() {
    Keys keys;
    keys.Poll();
    keys.held = {1};
    Require(keys.Poll() == Input::Down, "Fresh fire must scroll down");
    keys.pending = true;
    keys.now = 200;
    Require(!keys.Poll(), "A pending command must suppress held scrolling");
    keys.pending = false;
    Require(!keys.Poll(), "The pending repeat must not replay after acknowledgement");
    keys.now = 395;
    Require(keys.Poll() == Input::Down, "Acknowledged held scrolling must resume after a full interval");
    keys.allowed = false;
    keys.now = 500;
    Require(!keys.Poll(), "Lost focus must suppress navigation");
    keys.allowed = true;
    Require(!keys.Poll(), "Focus return must reseed held keys");
    keys.now = 900;
    Require(!keys.Poll(), "Held keys must not resume after focus return without a release");
}
void HeldSelectionNeverRepeatsFeatureActions() {
    Keys keys;
    keys.Poll();
    keys.held = {70};
    Require(keys.Poll() == Input::Select, "Fresh use must select");
    keys.now = 1000;
    Require(!keys.Poll(), "Holding use must never repeatedly change game state");
}
void DueSourceScrollPrecedesFreshSelect() {
    Keys keys;
    keys.Poll();
    keys.held = {2};
    Require(keys.Poll() == Input::Up, "Fresh aim must start scroll repeat");
    keys.now = 195;
    keys.held = {2, 70};
    Require(keys.Poll() == Input::Up, "Use must follow the due source scroll branch");
    Require(!keys.Poll(), "One source branch cannot dispatch a second action on the same poll");
}
void ReleasedAndPendingFreshDirectionsNeverReplay() {
    Keys keys;
    keys.Poll();
    keys.pending = true;
    keys.held = {2};
    Require(!keys.Poll(), "Fresh directions while pending must be consumed");
    keys.pending = false;
    keys.now = 1000;
    Require(!keys.Poll(), "Consumed directions must not arm a delayed repeat");
    keys.held = {};
    keys.Poll();
    keys.held = {2};
    Require(keys.Poll() == Input::Up, "A released key must accept a new press");
    keys.held = {};
    keys.now = 2000;
    Require(!keys.Poll(), "Released directions must stop repeats");
}
void ClockRollbackDisarmsRepeatsAndLargeValuesDoNotOverflow() {
    Keys keys;
    keys.now = 100;
    keys.Poll();
    keys.held = {1};
    Require(keys.Poll() == Input::Down, "Fresh fire must start a repeat");
    keys.now = 99;
    Require(!keys.Poll(), "A clock rollback must disarm repeats");
    keys.now = 1000;
    Require(!keys.Poll(), "Rollback must not become a delayed navigation event");
    keys.held = {};
    keys.Poll();
    keys.now = std::numeric_limits<std::uint64_t>::max() - 194;
    keys.held = {1};
    Require(keys.Poll() == Input::Down, "Large clock values must accept fresh presses");
    keys.now = std::numeric_limits<std::uint64_t>::max();
    Require(!keys.Poll(), "Large clock values must not wrap the repeat deadline");
}
void NeutralDirectionsAllowSourceUseBeforeMelee() {
    Keys keys;
    keys.Poll();
    keys.held = {2, 1, 70, 86};
    Require(keys.Poll() == Input::Select, "Source Use must precede melee after neutral scrolling");
}
}

int main() {
    try {
        OpposingDirectionsDoNotMoveTheCursor();
        OpposingDirectionsStillAllowSelection();
        NeutralDirectionsAllowSourceUseBeforeMelee();
        HeldScrollRepeatsAtTheReferenceInterval();
        PendingCommandsAndFocusLossDoNotReplayInput();
        HeldSelectionNeverRepeatsFeatureActions();
        DueSourceScrollPrecedesFreshSelect();
        ReleasedAndPendingFreshDirectionsNeverReplay();
        ClockRollbackDisarmsRepeatsAndLargeValuesDoNotOverflow();
        std::cout << "Passed 9 native input parity checks\n";
    } catch (const std::exception& error) {
        std::cerr << "FAIL " << error.what() << '\n';
        return 1;
    }
}
