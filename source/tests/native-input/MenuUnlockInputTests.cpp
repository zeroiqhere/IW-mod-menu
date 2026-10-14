#include "NativeInputRouter.h"

#include <iostream>
#include <set>
#include <stdexcept>

using iw::overlay::runtime::Bindings;
using iw::overlay::runtime::Input;
using iw::overlay::runtime::InputMode;
using iw::overlay::runtime::NativeInputRouter;
namespace {
struct Keys {
    NativeInputRouter router{Bindings{{2}, {86, 6}, {2, 38}, {1, 40}, {70}, {51}, {52}, {1}}};
    std::set<int> held;
    InputMode mode = InputMode::Menu;
    bool open = true, focused = true, pending = false;
    std::uint64_t now = 0;
    auto Poll() { return router.Poll(focused, open, pending,
        [&](int key) { return held.contains(key); }, now, 7, true, false, mode); }
};
void Require(bool condition, const char* message) {
    if (!condition) throw std::runtime_error(message);
}
void LockedModeConsumesEverySavedInputWithoutCommands() {
    Keys keys; keys.mode = InputMode::MenuLocked; keys.Poll();
    for (int key : {1, 2, 38, 40, 51, 52, 70, 86, 6}) {
        keys.held = {key};
        Require(!keys.Poll(), "A paused menu must consume every ordinary input");
        keys.now += 1000;
        Require(!keys.Poll(), "A paused hold must not scroll, select, resize or close");
        keys.held.clear(); keys.Poll();
    }
}
void UnlockReseedsHeldSelectUntilAFreshPress() {
    Keys keys; keys.mode = InputMode::MenuLocked; keys.Poll(); keys.held = {70}; keys.Poll();
    keys.mode = InputMode::Menu; keys.now = 150;
    Require(!keys.Poll(), "Unlock cannot replay a selection pressed during the pause");
    keys.now = 1000; Require(!keys.Poll(), "Held selection remains disarmed after unlocking");
    keys.held.clear(); keys.Poll(); keys.held = {70};
    Require(keys.Poll() == Input::Select, "A fresh selection must work after the pause");
}
void EnteringThePauseCancelsAnArmedScrollRepeat() {
    Keys keys; keys.Poll(); keys.held = {1};
    Require(keys.Poll() == Input::Down, "The normal menu must first arm its scroll repeat");
    keys.mode = InputMode::MenuLocked; keys.now = 195; Require(!keys.Poll(), "Pause must cancel a due repeat");
    keys.mode = InputMode::Menu; keys.now = 345; Require(!keys.Poll(), "Unlock must reseed held scrolling");
    keys.now = 1000; Require(!keys.Poll(), "A cancelled scroll cannot burst after the pause");
    keys.held.clear(); keys.Poll(); keys.held = {1};
    Require(keys.Poll() == Input::Down, "Fresh scroll recovers after the pause");
}
void EnteringThePauseCancelsQuickExitAndChoiceHolds() {
    for (int key : {86, 52}) {
        Keys keys; keys.Poll(); keys.held = {key}; keys.Poll();
        keys.mode = InputMode::MenuLocked; keys.now = 90; Require(!keys.Poll(), "Pause cancels the old gesture");
        keys.mode = InputMode::Menu; keys.now = 250; Require(!keys.Poll(), "Unlock consumes the held gesture");
        keys.now = 1000; Require(!keys.Poll(), "Quick Exit or a choice cannot follow the pause");
        keys.held.clear(); Require(!keys.Poll(), "Releasing a cancelled hold cannot produce Back");
    }
}
void FocusAndPendingChangesCannotQueueALockedInput() {
    Keys keys; keys.mode = InputMode::MenuLocked; keys.Poll();
    keys.pending = true; keys.held = {70}; keys.Poll();
    keys.focused = false; Require(!keys.Poll(), "Lost focus remains disarmed");
    keys.focused = true; keys.pending = false; keys.Poll();
    keys.mode = InputMode::Menu; Require(!keys.Poll(), "Focus recovery cannot replay a paused key");
    Require(!keys.Poll(), "Acknowledgement cannot queue a paused selection");
}
void ClosedLockedFramesCannotOpenTheMenu() {
    Keys keys; keys.mode = InputMode::MenuLocked; keys.open = false; keys.Poll();
    keys.held = {2, 86}; Require(!keys.Poll(), "Locked input cannot become an opening combination");
    keys.now = 1000; Require(!keys.Poll(), "A held closed combination remains disarmed");
}
void AFailedKeyReadDisarmsTheLockedContext() {
    Keys keys; keys.mode = InputMode::MenuLocked; keys.Poll();
    bool rejected = false;
    try {
        keys.router.Poll(true, true, false, [](int) -> bool { throw std::runtime_error("Owned key failure"); },
            0, 7, true, false, InputMode::MenuLocked);
    } catch (const std::runtime_error&) { rejected = true; }
    Require(rejected, "Key failure must still be reported while controls are paused");
    keys.held = {70}; keys.mode = InputMode::Menu;
    Require(!keys.Poll() && !keys.Poll(), "Failed reads cannot leak a held key into the resumed menu");
}
}
int main() {
    const auto tests = {LockedModeConsumesEverySavedInputWithoutCommands, UnlockReseedsHeldSelectUntilAFreshPress,
        EnteringThePauseCancelsAnArmedScrollRepeat, EnteringThePauseCancelsQuickExitAndChoiceHolds,
        FocusAndPendingChangesCannotQueueALockedInput, ClosedLockedFramesCannotOpenTheMenu,
        AFailedKeyReadDisarmsTheLockedContext};
    try {
        for (const auto test : tests) test();
        std::cout << "Passed " << tests.size() << " menu-unlock input checks\n";
        return 0;
    } catch (const std::exception& failure) {
        std::cerr << "FAIL " << failure.what() << '\n';
        return 1;
    }
}
