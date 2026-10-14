#include "NativeInputRouter.h"
#include <iostream>
#include <set>
#include <stdexcept>
using namespace iw::overlay;
using namespace iw::overlay::runtime;
namespace {
void Check(bool value, const char *message) { if (!value) throw std::runtime_error(message); }
template <class Call> void Reject(Call call) {
    try { call(); } catch (const std::exception &) { return; }
    throw std::runtime_error("Expected rejection");
}
struct Keys {
    Bindings bindings{{2}, {86, 6}, {2, 38}, {1, 40}, {70}, {51}, {52}, {1},
                      {38}, {40}, {71}, {69}, {16}, {17}, {32}};
    NativeInputRouter router{bindings};
    MainOpeningControls main{3, {OpeningAction::Aim, OpeningAction::Melee}};
    ClosedMenuOpeningPolicies pair{main, {2, {OpeningAction::Aim, OpeningAction::SecondaryOffhand}}, false, 7};
    std::set<int> held;
    bool open = false, pending = false, focus = true, dual = true;
    std::uint64_t now = 0;
    unsigned calls = 0;
    auto Poll() {
        calls = 0;
        return router.Poll(focus, open, pending, [&](int key) { ++calls; return held.contains(key); },
                           now, 10, false, false, InputMode::Menu, 120, 6, &main, 0, 0, dual ? &pair : nullptr);
    }
};
void GlobalQuickAndDisableTransactionsReseedHeldChoiceWithoutMainChanges() {
    for (int change = 0; change < 4; ++change) {
        Keys keys; keys.open = true; keys.Poll(); keys.held = {52};
        Check(keys.Poll() == Input::NextChoice, "Initial choice failed");
        if (change == 0) ++keys.pair.revision;
        if (change == 1) ++keys.pair.quick.revision;
        if (change == 2) keys.pair.quickMenuDisabled = true;
        if (change == 3) keys.pair.quick.actions = {OpeningAction::Jump};
        keys.now = 180;
        Check(!keys.Poll() && !keys.Poll(), "Unchanged Main concealed a stale global choice hold");
        keys.held.clear(); keys.Poll(); keys.held = {52};
        Check(keys.Poll() == Input::NextChoice, "Fresh choice failed after global transaction");
    }
}
void ActualMainRoutesWhileConfiguredQuickNeverActivates() {
    Keys keys; keys.Poll(); keys.held = {2, 69};
    Check(!keys.Poll(), "Parsed Quick configuration activated an unverified live gate");
    keys.held.clear(); keys.Poll(); keys.held = {2, 86};
    Check(keys.Poll() == Input::Toggle, "Actual mirrored Main failed to open");
}
void InvalidPairAndMirrorFailBeforeKeyCallbacksEvenWhenDisabled() {
    for (int malformed = 0; malformed < 5; ++malformed) {
        Keys keys;
        if (malformed == 0) keys.pair.revision = 0;
        if (malformed == 1) keys.pair.main.revision = 4;
        if (malformed == 2) keys.pair.quick.actions.clear();
        if (malformed == 3) keys.pair.quick.actions = {OpeningAction::Frag, OpeningAction::SecondaryOffhand};
        if (malformed == 4) {
            auto bindings = keys.bindings; bindings.sprint.clear();
            keys.router = NativeInputRouter{bindings};
            keys.pair.quick.actions = {OpeningAction::Sprint}; keys.pair.quickMenuDisabled = true;
        }
        Reject([&] { keys.Poll(); });
        Check(keys.calls == 0, "Malformed or unavailable pair read keys");
    }
}
void PairAppearancePendingFocusAndClosureCannotReplayMainCompletion() {
    Keys keys; keys.Poll(); keys.pending = true; keys.held = {2, 86};
    Check(!keys.Poll(), "Pending command admitted Main");
    keys.pending = false; ++keys.pair.revision;
    Check(!keys.Poll() && !keys.Poll(), "Global ACK recovery replayed Main");
    keys.focus = false; keys.Poll(); keys.focus = true;
    Check(!keys.Poll(), "Focus recovery replayed Main");
    keys.open = true; keys.Poll(); keys.open = false;
    Check(!keys.Poll(), "Menu closure replayed Main");
    keys.held.erase(86); keys.Poll(); keys.held.insert(86);
    Check(keys.Poll() == Input::Toggle, "Fresh partial completion failed after recovery");
}
void LegacyMainOnlyRetainsItsPolicyAndTransitionConsumesFullHold() {
    Keys keys; keys.dual = false; keys.Poll(); keys.held = {2, 86};
    Check(keys.Poll() == Input::Toggle, "Legacy typed Main behavior changed");
    keys.dual = true;
    Check(!keys.Poll() && !keys.Poll(), "Dual policy installation inherited a full Main hold");
    keys.held.erase(86); keys.Poll(); keys.held.insert(86);
    Check(keys.Poll() == Input::Toggle, "Dual installation required every constituent release");
}
}
int main() {
    struct Test { const char *name; void (*run)(); };
    const Test tests[] = {
        {"GlobalQuickAndDisableTransactionsReseedHeldChoiceWithoutMainChanges", GlobalQuickAndDisableTransactionsReseedHeldChoiceWithoutMainChanges},
        {"ActualMainRoutesWhileConfiguredQuickNeverActivates", ActualMainRoutesWhileConfiguredQuickNeverActivates},
        {"InvalidPairAndMirrorFailBeforeKeyCallbacksEvenWhenDisabled", InvalidPairAndMirrorFailBeforeKeyCallbacksEvenWhenDisabled},
        {"PairAppearancePendingFocusAndClosureCannotReplayMainCompletion", PairAppearancePendingFocusAndClosureCannotReplayMainCompletion},
        {"LegacyMainOnlyRetainsItsPolicyAndTransitionConsumesFullHold", LegacyMainOnlyRetainsItsPolicyAndTransitionConsumesFullHold}
    };
    try {
        for (const auto &test : tests) { test.run(); std::cout << "PASS " << test.name << '\n'; }
        std::cout << "Passed " << std::size(tests) << " dual opening policy input tests\n";
    } catch (const std::exception &error) { std::cerr << "FAIL " << error.what() << '\n'; return 1; }
}
