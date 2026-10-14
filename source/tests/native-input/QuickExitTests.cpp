#include "NativeInputRouter.h"

#include <iostream>
#include <limits>
#include <set>
#include <stdexcept>
#include <string>

using iw::overlay::runtime::Bindings;
using iw::overlay::runtime::Input;
using iw::overlay::runtime::NativeInputRouter;

namespace {
// The real router receives only owned keys and a deterministic clock
struct Keys {
    NativeInputRouter router{Bindings{{2}, {86, 5}, {2, 38}, {1, 40}, {70}, {51}, {52}}};
    std::set<int> held;
    bool allowed = true, pending = false, open = true, quick = true, root = false;
    std::uint64_t now = 0, context = 1;
    auto Poll() { return router.Poll(allowed, open, pending,
        [&](int key) { return held.contains(key); }, now, context, quick, root); }
    void Seed() { Poll(); }
    void Press() { held = {86}; }
    void Release() { held.clear(); }
};
void Require(bool value, const char* message) {
    if (!value) throw std::runtime_error(message);
}
void ShortTapWaitsForReleaseAndReturnsOnePage() {
    Keys keys; keys.Seed(); keys.Press();
    Require(!keys.Poll(), "Quick Exit must defer a nested tap until release");
    keys.now = 89; Require(!keys.Poll(), "A held knife must not close before 90 ms");
    keys.Release(); Require(keys.Poll() == Input::Back, "A short tap must return one page");
    Require(!keys.Poll(), "A released tap must not repeat");
}
void HeldMeleeClosesAtNinetyMillisecondsOnlyOnce() {
    Keys keys; keys.Seed(); keys.Press(); keys.Poll();
    keys.now = 89; Require(!keys.Poll(), "The hold must respect its boundary");
    keys.now = 90; Require(keys.Poll() == Input::Close, "Ten source counts require nine waits, nominal 90 ms");
    keys.now = 1000; Require(!keys.Poll(), "A hold must close only once");
    keys.Release(); Require(!keys.Poll(), "Release after close must not navigate");
    keys.Press(); Require(!keys.Poll(), "A new hold must arm after release");
    keys.now = 1090; Require(keys.Poll() == Input::Close, "A new hold must work normally");
}
void ReleaseAtOrAfterThresholdRemainsOnePageBack() {
    for (const auto elapsed : {90u, 1000u}) {
        Keys keys; keys.Seed(); keys.Press(); keys.Poll();
        keys.now = elapsed; keys.Release();
        Require(keys.Poll() == Input::Back, "Elapsed time cannot prove a knife remained held until the threshold");
        Require(!keys.Poll(), "Late release must produce only one Back");
    }
}
void StartupAlternateKnifeCannotBypassRequiredRelease() {
    Keys keys; keys.Press(); keys.Seed(); keys.held.insert(5);
    Require(!keys.Poll(), "Another saved knife cannot bypass aggregate startup release");
    keys.now = 1000; Require(!keys.Poll(), "Startup chord must never arm a hold");
    keys.Release(); keys.Poll(); keys.held = {5}; keys.Poll(); keys.Release();
    Require(keys.Poll() == Input::Back, "Alternate fresh knife must recover after aggregate release");
}
void RootAndDisabledPreferenceKeepImmediateBack() {
    Keys root; root.root = true; root.Seed(); root.Press();
    Require(root.Poll() == Input::Back, "Root knife must close immediately through normal Back");
    root.now = 500; Require(!root.Poll(), "Root knife must not repeat");
    Keys disabled; disabled.quick = false; disabled.Seed(); disabled.Press();
    Require(disabled.Poll() == Input::Back, "Disabled Quick Exit must preserve immediate Back");
    disabled.now = 500; Require(!disabled.Poll(), "Default Back must remain edge-triggered");
}
void ActiveHoldSuppressesNavigationSelectionAndChoices() {
    Keys keys; keys.Seed(); keys.Press();
    Require(!keys.Poll(), "Knife alone must begin its source hold");
    keys.held.insert({2, 70, 51});
    keys.now = 20; Require(!keys.Poll(), "Other held actions cannot leak during the gesture");
    keys.Release(); keys.now = 30;
    Require(keys.Poll() == Input::Back, "Only the gesture result may dispatch");
    keys.now = 500; Require(!keys.Poll(), "Suppressed inputs cannot replay after release");
}
void PendingCommandCancelsHoldAndConsumesFreshPress() {
    Keys keys; keys.Seed(); keys.Press(); keys.Poll();
    keys.pending = true; keys.now = 60; Require(!keys.Poll(), "Pending work must cancel the gesture");
    keys.pending = false; keys.now = 1000; Require(!keys.Poll(), "Cancelled hold cannot replay after acknowledgement");
    keys.Release(); Require(!keys.Poll(), "Cancelled tap cannot navigate on release");
    keys.pending = true; keys.Press(); Require(!keys.Poll(), "Fresh knife while pending must be consumed");
    keys.pending = false; Require(!keys.Poll(), "Consumed knife cannot arm after acknowledgement");
    keys.Release(); keys.Poll(); keys.Press(); keys.Poll(); keys.now = 1090;
    Require(keys.Poll() == Input::Close, "Fresh input after release must recover");
}
void NavigationChangesCancelHeldAndReleasedGestures() {
    Keys keys; keys.Seed(); keys.Press(); keys.Poll();
    ++keys.context; keys.now = 90; Require(!keys.Poll(), "A hold cannot close a changed page");
    keys.now = 900; Require(!keys.Poll(), "Changed page must require release");
    keys.Release(); Require(!keys.Poll(), "Changed page must not inherit a tap");
    keys.Press(); keys.Poll(); ++keys.context; keys.Release();
    Require(!keys.Poll(), "Context change on release must discard the old tap");
}
void PreferenceAndRootChangesCancelExistingGesture() {
    Keys keys; keys.Seed(); keys.Press(); keys.Poll();
    keys.quick = false; Require(!keys.Poll(), "Disabling Quick Exit cannot convert a hold to Back");
    keys.Release(); Require(!keys.Poll(), "Mode change discards the pending tap");
    keys.quick = true; keys.Poll(); keys.Press(); keys.Poll();
    keys.root = true; keys.now = 90; Require(!keys.Poll(), "Changing root identity must cancel a hold");
    keys.Release(); keys.Poll(); keys.Press();
    Require(keys.Poll() == Input::Back, "A fresh root knife must still close immediately");
}
void FocusLossAndMenuChangesRequireRelease() {
    Keys keys; keys.Seed(); keys.Press(); keys.Poll();
    keys.allowed = false; Require(!keys.Poll(), "Focus loss must cancel the hold");
    keys.allowed = true; keys.now = 1000; Require(!keys.Poll(), "Focus return reseeds held keys");
    keys.now = 2000; Require(!keys.Poll(), "Reseeded knife cannot later close");
    keys.Release(); Require(!keys.Poll(), "Reseeded release must not become Back");
    keys.Press(); keys.Poll(); keys.open = false; keys.Poll(); keys.open = true; keys.Poll();
    keys.now = 3000; Require(!keys.Poll(), "Reopening cannot inherit a knife hold");
}
void StartupHeldKnifeNeverArmsUntilRelease() {
    Keys keys; keys.Press(); keys.Seed(); keys.now = 1000;
    Require(!keys.Poll(), "Startup held knife must never close");
    keys.Release(); Require(!keys.Poll(), "Startup release is not a deliberate Back");
    keys.Press(); keys.Poll(); keys.Release(); Require(keys.Poll() == Input::Back, "Fresh tap must work after release");
}
void ClockRollbackCancelsAndLargeTimestampsDoNotOverflow() {
    Keys keys; keys.now = 100; keys.Seed(); keys.Press(); keys.Poll();
    keys.now = 99; Require(!keys.Poll(), "Clock rollback must cancel a hold");
    keys.now = 1000; Require(!keys.Poll(), "Rollback cannot become a delayed close");
    keys.Release(); Require(!keys.Poll(), "Rollback release cannot become Back");
    keys.now = std::numeric_limits<std::uint64_t>::max() - 90; keys.Press(); keys.Poll();
    keys.now = std::numeric_limits<std::uint64_t>::max();
    Require(keys.Poll() == Input::Close, "Elapsed subtraction must work near uint64 maximum");
}
void AlternateMeleeKeysShareOneGesture() {
    Keys keys; keys.Seed(); keys.held = {5}; Require(!keys.Poll(), "Alternate saved knife must arm");
    keys.held = {5, 86}; keys.now = 40; Require(!keys.Poll(), "Second knife must not restart the gesture");
    keys.held = {86}; keys.now = 90; Require(keys.Poll() == Input::Close, "Any held knife must retain the original start");
}
void KeyReadFailureDiscardsPendingGesture() {
    Keys keys; keys.Seed(); keys.Press(); keys.Poll();
    try {
        keys.router.Poll(true, true, false, [](int) -> bool { throw std::runtime_error("Owned key failure"); }, 10, 1, true, false);
        throw std::logic_error("Key failure did not propagate");
    } catch (const std::runtime_error&) {}
    keys.now = 1000; Require(!keys.Poll(), "Key failure must reseed rather than replay");
    keys.Release(); Require(!keys.Poll(), "Failed gesture release cannot navigate");
}
void CloseCommandHasDistinctLocalProtocolName() {
    Require(std::string(iw::overlay::runtime::InputName(Input::Close)) == "close", "Close command must be distinct from shutdown");
}
}
int main() {
    try {
        ShortTapWaitsForReleaseAndReturnsOnePage();
        HeldMeleeClosesAtNinetyMillisecondsOnlyOnce();
        ReleaseAtOrAfterThresholdRemainsOnePageBack();
        StartupAlternateKnifeCannotBypassRequiredRelease();
        RootAndDisabledPreferenceKeepImmediateBack();
        ActiveHoldSuppressesNavigationSelectionAndChoices();
        PendingCommandCancelsHoldAndConsumesFreshPress();
        NavigationChangesCancelHeldAndReleasedGestures();
        PreferenceAndRootChangesCancelExistingGesture();
        FocusLossAndMenuChangesRequireRelease();
        StartupHeldKnifeNeverArmsUntilRelease();
        ClockRollbackCancelsAndLargeTimestampsDoNotOverflow();
        AlternateMeleeKeysShareOneGesture();
        KeyReadFailureDiscardsPendingGesture();
        CloseCommandHasDistinctLocalProtocolName();
        std::cout << "Passed 15 Quick Exit input checks\n";
    } catch (const std::exception& error) {
        std::cerr << "FAIL " << error.what() << '\n'; return 1;
    }
}
