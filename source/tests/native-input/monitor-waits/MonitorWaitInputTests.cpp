#include "NativeInputRouter.h"
#include <array>
#include <iostream>
#include <limits>
#include <set>
#include <stdexcept>

using namespace iw::overlay;
using namespace iw::overlay::runtime;
namespace {
void Require(bool value, const char* message) {
    if (!value) throw std::runtime_error(message);
}
struct Rig {
    Bindings bindings{{2}, {86}, {2, 38}, {1, 40}, {70}, {51}, {52}};
    NativeInputRouter router{bindings};
    std::set<int> held;
    bool open = true, focused = true, pending = false, quickExit = false, root = false;
    std::uint64_t now = 0, navigation = 7, menu = 8;
    InputMode mode = InputMode::Menu;
    std::optional<MenuMonitorWait> wait;
    int reads = 0;
    std::optional<Input> Poll() {
        return router.Poll(focused, open, pending, [&](int key) { ++reads; return held.contains(key); },
            now, navigation, quickExit, root, mode, 120, menu, nullptr, 0, 0, nullptr, nullptr,
            MenuSurface::Main, wait ? &*wait : nullptr);
    }
    void StartChoice() {
        Poll(); held = {51}; now = 1;
        Require(Poll() == Input::PreviousChoice, "The original choice was not admitted");
        wait = MenuMonitorWait{1, MenuMonitorWaitKind::Choice};
    }
};
void ChoiceWaitBlocksEveryOrdinaryBranchUntilOwnerRetiresIt() {
    for (const auto& keys : {std::set<int>{2}, {1}, {70}, {86}, {52}, {2, 86}, {1, 70, 86}}) {
        Rig rig; rig.StartChoice(); rig.held = keys; rig.now = 179;
        Require(!rig.Poll(), "Another monitor branch escaped the owned choice wait");
        rig.now = 1000;
        Require(!rig.Poll(), "A native elapsed estimate retired an authoritative wait");
    }
}
void ChoiceWaitPreservesOneHeldRepeatAtTheActualDeadline() {
    Rig rig; rig.StartChoice(); rig.now = 179;
    Require(!rig.Poll(), "Held choice repeated inside the mechanical pause");
    rig.wait.reset(); rig.now = 181;
    Require(rig.Poll() == Input::PreviousChoice, "The admitted hold was reseeded or given a second wait");
    Require(!rig.Poll(), "The released wait emitted a catch-up burst");
}
void ReleasedChoiceDoesNotEndItsWaitOrReplaySelection() {
    Rig rig; rig.StartChoice(); rig.held.clear(); rig.now = 20; Require(!rig.Poll(), "Release ended the monitor wait");
    rig.held = {70}; rig.now = 30; Require(!rig.Poll(), "Use escaped a released choice wait");
    rig.wait.reset(); rig.now = 181; Require(!rig.Poll(), "Use observed during the wait replayed after it");
    rig.held.clear(); rig.Poll(); rig.held = {70};
    Require(rig.Poll() == Input::Select, "Fresh selection stayed locked after the owner retired its wait");
}
void OppositeChoiceDoesNotCancelTheWholeMonitorWait() {
    Rig rig; rig.StartChoice(); rig.held = {52}; rig.now = 50;
    Require(!rig.Poll(), "Opposite choice bypassed the source branch wait");
    rig.held = {70}; rig.now = 80; Require(!rig.Poll(), "A changed direction released ordinary Use");
    rig.wait.reset(); rig.now = 181; Require(!rig.Poll(), "A discarded Use was queued by direction changes");
}
void KnifeObservedDuringWaitCannotStartALateQuickExit() {
    Rig rig; rig.quickExit = true; rig.StartChoice(); rig.held = {86}; rig.now = 10;
    Require(!rig.Poll(), "Knife started Quick Exit inside a choice wait");
    rig.wait.reset(); rig.now = 181; Require(!rig.Poll(), "Consumed knife started a hidden hold on wait retirement");
    rig.now = 1000; Require(!rig.Poll(), "Consumed knife closed the menu later");
    rig.held.clear(); rig.Poll(); rig.held = {86}; Require(!rig.Poll(), "A fresh enabled child knife must first hold");
    rig.now = 1090; Require(rig.Poll() == Input::Close, "A fresh admitted knife stopped working after the wait");
}
void RootBackWaitBlocksClosedOpeningUntilFreshRelease() {
    Rig rig; rig.Poll(); rig.open = false; rig.root = true;
    rig.wait = MenuMonitorWait{2, MenuMonitorWaitKind::Back}; rig.now = 1; rig.Poll();
    rig.held = {2, 86}; rig.now = 249;
    Require(!rig.Poll(), "Closing the root removed the remaining source Back wait");
    rig.wait.reset(); rig.now = 251; Require(!rig.Poll(), "A chord pressed inside the Back wait reopened later");
    rig.held.clear(); rig.Poll(); rig.held = {2, 86};
    Require(rig.Poll() == Input::Toggle, "A fresh post-wait opening chord failed");
}
void ChildBackWaitSurvivesItsOwnNavigationChange() {
    Rig rig; rig.Poll(); rig.wait = MenuMonitorWait{3, MenuMonitorWaitKind::Back};
    ++rig.navigation; ++rig.menu; rig.now = 1; rig.Poll();
    rig.held = {70}; rig.now = 249;
    Require(!rig.Poll(), "The accepted parent transition canceled its own mechanical wait");
    rig.wait.reset(); rig.now = 251; Require(!rig.Poll(), "A parent selection observed during Back replayed");
    rig.held.clear(); rig.Poll(); rig.held = {70};
    Require(rig.Poll() == Input::Select, "Fresh parent selection stayed locked after Back finished");
}
void NavigationReplacementCannotRetainAnOldChoiceRepeat() {
    Rig rig; rig.StartChoice(); rig.now = 50; rig.Poll();
    ++rig.navigation; ++rig.menu; rig.wait.reset(); rig.now = 181;
    Require(!rig.Poll(), "A replaced menu inherited an admitted choice hold");
    rig.now = 1000; Require(!rig.Poll(), "An old repeat appeared after replacement");
}
void FocusAndPendingAdmissionConsumeWaitEdges() {
    Rig rig; rig.StartChoice(); rig.pending = true; rig.held = {70}; rig.now = 50;
    Require(!rig.Poll(), "Pending admission emitted wait input");
    rig.focused = false; const int reads = rig.reads; Require(!rig.Poll(), "Focus loss emitted wait input");
    Require(rig.reads == reads, "Lost focus still read the keyboard");
    rig.focused = true; rig.pending = false; rig.now = 100; Require(!rig.Poll(), "Focus return inherited a held Use");
    rig.wait.reset(); rig.now = 181; Require(!rig.Poll(), "Focus recovery replayed a consumed wait edge");
}
void WaitClockRollbackRetiresInheritedRepeats() {
    Rig rig; rig.StartChoice(); rig.now = 150; Require(!rig.Poll(), "Choice escaped before rollback");
    rig.now = 10; Require(!rig.Poll(), "Rollback emitted a monitor action");
    rig.now = 11; rig.Poll(); rig.wait.reset(); rig.now = 500;
    Require(!rig.Poll(), "Rollback retained an old delayed choice repeat");
    rig.held.clear(); rig.Poll(); rig.held = {51};
    Require(rig.Poll() == Input::PreviousChoice, "Fresh choice failed after rollback retirement");
}
void InvalidWaitShapesFailBeforeReadingKeys() {
    for (const auto wait : {MenuMonitorWait{0, MenuMonitorWaitKind::Choice},
        MenuMonitorWait{1, static_cast<MenuMonitorWaitKind>(99)}}) {
        Rig rig; rig.wait = wait; bool rejected = false;
        try { rig.Poll(); } catch (const std::invalid_argument&) { rejected = true; }
        Require(rejected && rig.reads == 0, "Malformed monitor wait reached the key reader");
    }
    Rig rig; rig.open = false; rig.wait = MenuMonitorWait{1, MenuMonitorWaitKind::Choice};
    bool rejected = false; try { rig.Poll(); } catch (const std::invalid_argument&) { rejected = true; }
    Require(rejected && rig.reads == 0, "Closed choice wait reached ordinary input");
}
void NativeWaitDoesNotInventAnUnsignedDeadline() {
    Rig rig; rig.Poll(); rig.wait = MenuMonitorWait{std::numeric_limits<std::uint64_t>::max(), MenuMonitorWaitKind::Back};
    rig.now = std::numeric_limits<std::uint64_t>::max() - 1; rig.held = {70};
    Require(!rig.Poll(), "An unsigned boundary bypassed the authoritative pause");
    ++rig.now; Require(!rig.Poll(), "Native wait overflow invented readiness");
}
}
int main() {
    struct Test { const char* name; void (*run)(); };
    const std::array tests{
        Test{"ChoiceWaitBlocksEveryOrdinaryBranchUntilOwnerRetiresIt", ChoiceWaitBlocksEveryOrdinaryBranchUntilOwnerRetiresIt},
        Test{"ChoiceWaitPreservesOneHeldRepeatAtTheActualDeadline", ChoiceWaitPreservesOneHeldRepeatAtTheActualDeadline},
        Test{"ReleasedChoiceDoesNotEndItsWaitOrReplaySelection", ReleasedChoiceDoesNotEndItsWaitOrReplaySelection},
        Test{"OppositeChoiceDoesNotCancelTheWholeMonitorWait", OppositeChoiceDoesNotCancelTheWholeMonitorWait},
        Test{"KnifeObservedDuringWaitCannotStartALateQuickExit", KnifeObservedDuringWaitCannotStartALateQuickExit},
        Test{"RootBackWaitBlocksClosedOpeningUntilFreshRelease", RootBackWaitBlocksClosedOpeningUntilFreshRelease},
        Test{"ChildBackWaitSurvivesItsOwnNavigationChange", ChildBackWaitSurvivesItsOwnNavigationChange},
        Test{"NavigationReplacementCannotRetainAnOldChoiceRepeat", NavigationReplacementCannotRetainAnOldChoiceRepeat},
        Test{"FocusAndPendingAdmissionConsumeWaitEdges", FocusAndPendingAdmissionConsumeWaitEdges},
        Test{"WaitClockRollbackRetiresInheritedRepeats", WaitClockRollbackRetiresInheritedRepeats},
        Test{"InvalidWaitShapesFailBeforeReadingKeys", InvalidWaitShapesFailBeforeReadingKeys},
        Test{"NativeWaitDoesNotInventAnUnsignedDeadline", NativeWaitDoesNotInventAnUnsignedDeadline}};
    int failures = 0;
    for (const auto& test : tests) {
        try { test.run(); std::cout << "PASS " << test.name << '\n'; }
        catch (const std::exception& error) { ++failures; std::cout << "FAIL " << test.name << ": " << error.what() << '\n'; }
    }
    std::cout << tests.size() - failures << '/' << tests.size() << " monitor wait input checks passed\n";
    return failures ? 1 : 0;
}
