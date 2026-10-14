#include "NativeInputRouter.h"
#include <iostream>
#include <set>
#include <stdexcept>

using namespace iw::overlay::runtime;
namespace {
void Require(bool condition, const char* message) {
    if (!condition) throw std::runtime_error(message);
}
// Owned saved keys exercise routing without reading a window or desktop
struct Keys {
    NativeInputRouter router{Bindings{{2}, {86, 6}, {2, 38}, {1, 40}, {70}, {51}, {52}, {1}, {38}, {40}, {71}, {69}}};
    std::set<int> held;
    bool foreground = true, open = true, pending = false, quickExit = true;
    std::uint64_t navigation = 1, page = 1, now = 0;
    InputMode mode = InputMode::MenuCredits;
    auto Poll() { return router.Poll(foreground, open, pending, [&](int key) { return held.contains(key); },
        now, navigation, quickExit, false, mode, 120, page); }
    void Release() { held.clear(); Poll(); }
};
void CreditsLeafFinishesOncePerFreshAggregateMeleePress() {
    MenuCreditsInput input; input.Reset(true);
    Require(!input.Poll(true, false, false), "Entry inherited a held melee exit");
    input.Poll(false, false, false);
    Require(input.Poll(true, false, false) == Input::FinishMenuCredits, "Fresh saved melee did not finish credits");
    input.Poll(false, false, false);
    Require(input.Poll(true, false, false) == Input::FinishMenuCredits, "A released deliberate retry was blocked");
}
void SavedMeleeUsesEveryRemappedKeyWithoutGuessingV() {
    NativeInputRouter router(Bindings{{2}, {69, 5}, {2}, {1}, {70}});
    std::set<int> keys; auto poll = [&] { return router.Poll(true, true, false,
        [&](int key) { return keys.contains(key); }, 0, 1, false, false, InputMode::MenuCredits); };
    poll(); keys = {86}; Require(!poll(), "Unbound V became an assumed credits exit");
    keys = {5}; Require(poll() == Input::FinishMenuCredits, "Remapped mouse melee lost its exact action");
}
void AimUseMovementGrenadesChoicesAndQuickExitCannotActDuringCredits() {
    Keys keys; keys.Poll(); keys.held = {1, 2, 38, 40, 70, 51, 52, 71, 69};
    Require(!keys.Poll(), "Non-melee action escaped the credits modal route");
    keys.now = 99999; Require(!keys.Poll(), "Ignored credits keys started a normal repeat");
    keys.held.insert(86); Require(keys.Poll() == Input::FinishMenuCredits, "Aim plus melee became Toggle or Quick Exit");
    keys.now += 1000; Require(!keys.Poll(), "Held credits melee became a Quick Exit close");
}
void StartupAndAlternateMeleeKeysRequireCompleteAggregateRelease() {
    Keys keys; keys.held = {86}; keys.Poll(); keys.held = {6};
    Require(!keys.Poll(), "Alternate held melee bypassed entry release");
    keys.Release(); keys.held = {6}; Require(keys.Poll() == Input::FinishMenuCredits, "Aggregate release did not recover exit");
    keys.held.insert(86); Require(!keys.Poll(), "A second saved key accelerated the same exit group");
}
void PendingMeleeEdgesAreConsumedWithoutDelayedReplay() {
    Keys keys; keys.Poll(); keys.pending = true; keys.held = {86}; Require(!keys.Poll(), "Pending credits command dispatched");
    keys.pending = false; keys.now = 1000; Require(!keys.Poll(), "Pending melee replayed after acknowledgement");
    keys.held = {6}; Require(!keys.Poll(), "Alternate pending hold bypassed aggregate release");
    keys.Release(); keys.held = {86}; Require(keys.Poll() == Input::FinishMenuCredits, "Fresh exit was lost after pending release");
}
void FocusReturnAndNavigationOrPageChangesReseedHeldMelee() {
    Keys keys; keys.Poll(); keys.foreground = false; keys.held = {86}; Require(!keys.Poll(), "Focus loss dispatched credits exit");
    keys.foreground = true; keys.Poll(); Require(!keys.Poll(), "Focus return replayed a held exit");
    keys.Release(); keys.held = {6}; ++keys.navigation; Require(!keys.Poll(), "New navigation inherited held exit");
    keys.Release(); keys.held = {86}; ++keys.page; keys.Poll(); Require(!keys.Poll(), "New page inherited held exit");
    keys.Release(); keys.held = {6}; Require(keys.Poll() == Input::FinishMenuCredits, "Fresh context exit did not recover");
}
void UnlockAndReopenDoNotLeakCreditsExitIntoBackOrSelect() {
    Keys keys; keys.Poll(); keys.held = {86}; Require(keys.Poll() == Input::FinishMenuCredits, "Fresh exit failed before transition");
    keys.mode = InputMode::MenuLocked; keys.Poll(); keys.mode = InputMode::Menu; keys.Poll();
    keys.now = 1000; Require(!keys.Poll(), "Held credits finish leaked into Back or Close");
    keys.Release(); keys.held = {70}; Require(keys.Poll() == Input::Select, "Parent retained credits routing");
    keys.open = false; keys.Poll(); keys.held = {86}; keys.open = true; keys.mode = InputMode::MenuCredits; keys.Poll();
    Require(!keys.Poll(), "Reopening credits inherited a held exit");
}
void KeyReaderFailureClosesTheFreshExitRouteUntilReleased() {
    Keys keys; keys.Poll(); bool rejected = false;
    try { keys.router.Poll(true, true, false, [](int) -> bool { throw std::runtime_error("Owned key failure"); },
        0, 1, false, false, InputMode::MenuCredits); }
    catch (const std::runtime_error&) { rejected = true; }
    Require(rejected, "Key-reader failure was swallowed");
    keys.held = {86}; keys.Poll(); Require(!keys.Poll(), "Failed key read retained a fresh exit edge");
    keys.Release(); keys.held = {6}; Require(keys.Poll() == Input::FinishMenuCredits, "Released key failure did not recover");
}
void LegacyBindingsKeepCreditsExitWithoutNewCapabilities() {
    NativeInputRouter router(Bindings{{2}, {86}, {2}, {1}, {70}});
    router.Poll(true, true, false, [](int) { return false; }, 0, 1, false, false, InputMode::MenuCredits);
    Require(router.Poll(true, true, false, [](int key) { return key == 86; },
        1, 1, false, false, InputMode::MenuCredits) == Input::FinishMenuCredits,
        "Optional editor bindings incorrectly became credits requirements");
}
void AcknowledgedExitCanRetryOnlyAfterAggregateReleaseInTheSameContext() {
    Keys keys; keys.Poll(); keys.held = {86};
    Require(keys.Poll() == Input::FinishMenuCredits, "First exit did not dispatch");
    CommandGate gate(0); gate.MarkSent(1); keys.pending = gate.Pending(); keys.held.insert(6);
    Require(!keys.Poll(), "Pending alternate melee key duplicated the same aggregate press");
    // An acknowledged focus rejection leaves the owner on this same credits page
    gate.ObserveAck(1); keys.pending = gate.Pending(); Require(!keys.Poll(), "Acknowledgement deferred an automatic exit retry");
    keys.held = {6}; Require(!keys.Poll(), "Partial melee release bypassed aggregate freshness");
    keys.Release(); keys.held = {6};
    Require(keys.Poll() == Input::FinishMenuCredits && keys.navigation == 1 && keys.page == 1
        && gate.NextSequence() == 2, "A fresh same-context retry was blocked after acknowledgement");
}
}
int main() {
    try {
        CreditsLeafFinishesOncePerFreshAggregateMeleePress();
        SavedMeleeUsesEveryRemappedKeyWithoutGuessingV();
        AimUseMovementGrenadesChoicesAndQuickExitCannotActDuringCredits();
        StartupAndAlternateMeleeKeysRequireCompleteAggregateRelease();
        PendingMeleeEdgesAreConsumedWithoutDelayedReplay();
        FocusReturnAndNavigationOrPageChangesReseedHeldMelee();
        UnlockAndReopenDoNotLeakCreditsExitIntoBackOrSelect();
        KeyReaderFailureClosesTheFreshExitRouteUntilReleased();
        LegacyBindingsKeepCreditsExitWithoutNewCapabilities();
        AcknowledgedExitCanRetryOnlyAfterAggregateReleaseInTheSameContext();
        std::cout << "Passed 10 menu-credits input checks\n";
        return 0;
    } catch (const std::exception& exception) { std::cerr << "FAIL " << exception.what() << '\n'; return 1; }
}
