#include "NativeInputRouter.h"
#include <iostream>
#include <limits>
#include <set>
#include <stdexcept>

using namespace iw::overlay::runtime;
namespace {
void Require(bool value, const char* message) {
    if (!value) throw std::runtime_error(message);
}
Bindings CompleteBindings() {
    return {{2}, {86, 6}, {2, 38, 87}, {1, 40, 83}, {70}, {51, 37}, {52, 39},
        {1}, {38, 87}, {40, 83}, {71, 69}, {81}};
}
// All timing checks use owned key state and a deterministic clock
struct Keys {
    NativeInputRouter router{CompleteBindings()};
    std::set<int> held;
    bool foreground = true, open = true, pending = false;
    std::uint64_t now = 0, context = 1, page = 1;
    InputMode mode = InputMode::MenuPositionEditor;
    auto Poll() { return router.Poll(foreground, open, pending,
        [&](int key) { return held.contains(key); }, now, context, true, false, mode, 120, page); }
    void Release(std::uint64_t elapsed = 125) { held.clear(); now += elapsed; Poll(); }
};
void ExactSavedMovementAndGrenadeGroupsDispatchSevenDistinctCommands() {
    Keys keys; keys.Poll();
    for (const auto& [key, action] : {std::pair{87, Input::MoveMenuUp}, {83, Input::MoveMenuDown},
        {37, Input::MoveMenuLeft}, {39, Input::MoveMenuRight}, {69, Input::IncreaseMenuPositionStep},
        {81, Input::DecreaseMenuPositionStep}, {6, Input::FinishMenuPositionEditor}}) {
        keys.Release(); keys.held = {key}; Require(keys.Poll() == action, "An exact saved editor action changed");
    }
}
void SourcePriorityChoosesDownRightAndSmokeBeforeFragAndMelee() {
    Keys keys; keys.Poll(); keys.held = {38, 40, 51, 52, 71, 81, 86};
    Require(keys.Poll() == Input::MoveMenuDown, "Down must win before horizontal, grenades and exit");
    keys.held.erase(40); keys.now = 25;
    Require(keys.Poll() == Input::MoveMenuUp, "Up must win before horizontal and offsets");
    keys.held.erase(38); keys.now = 50;
    Require(keys.Poll() == Input::MoveMenuRight, "Right must win horizontal conflicts");
    keys.held.erase(52); keys.now = 75;
    Require(keys.Poll() == Input::MoveMenuLeft, "Left must win before offsets");
    keys.held.erase(51); keys.now = 100;
    Require(keys.Poll() == Input::DecreaseMenuPositionStep, "Smoke must decrease before frag increase");
    keys.held.erase(81); keys.now = 225;
    Require(keys.Poll() == Input::IncreaseMenuPositionStep, "Frag must win before melee");
    keys.held.erase(71); keys.now = 350;
    Require(keys.Poll() == Input::FinishMenuPositionEditor, "Melee must finish after all higher actions stop");
    keys.now = 1000; Require(!keys.Poll(), "Held exit must not repeat");
}
void MovementWaitIsTwentyFiveMillisecondsWithoutAlternateKeyAcceleration() {
    Keys keys; keys.Poll(); keys.held = {38}; keys.Poll();
    keys.now = 24; Require(!keys.Poll(), "Movement repeated before 25ms");
    keys.held.insert(87); Require(!keys.Poll(), "A second key accelerated one action group");
    keys.now = 25; Require(keys.Poll() == Input::MoveMenuUp, "Movement lost the exact boundary");
    keys.now = 9999; Require(keys.Poll() == Input::MoveMenuUp, "Delayed movement failed to emit once");
    Require(!keys.Poll(), "Delayed movement emitted a catchup burst");
}
void GrenadeWaitIsOneHundredTwentyFiveMillisecondsIncludingBoundedNoOps() {
    Keys keys; keys.Poll(); keys.held = {71}; keys.Poll();
    keys.now = 124; Require(!keys.Poll(), "Offset changed before the source 100+25ms wait");
    keys.now = 125; Require(keys.Poll() == Input::IncreaseMenuPositionStep, "Offset omitted its exact repeat boundary");
    // Native input does not invent a shorter interval when the controller clamps step ten
    keys.now = 249; Require(!keys.Poll(), "Bounded offset repeated early");
    keys.now = 250; Require(keys.Poll() == Input::IncreaseMenuPositionStep, "A bound must retain source offset cadence");
}
void ReleasedActionsRetainGlobalWaitAndConsumeReleasedMovementAndExitEdges() {
    Keys keys; keys.Poll(); keys.held = {71}; keys.Poll();
    keys.now = 1; keys.held.clear(); keys.Poll();
    keys.now = 2; keys.held = {38, 86}; Require(!keys.Poll(), "Release canceled a captured offset wait");
    keys.now = 3; keys.held.erase(38); keys.Poll();
    keys.now = 125; Require(!keys.Poll(), "A released movement or wait-time exit replayed at deadline");
    keys.now = 1000; Require(!keys.Poll(), "A consumed hold replayed after a long pause");
    keys.Release(); keys.held = {86};
    Require(keys.Poll() == Input::FinishMenuPositionEditor, "A new released exit failed to recover");
}
void FreshEdgesAtDeadlineAreAcceptedAndEstablishedLowerHoldsRemainEligible() {
    Keys keys; keys.Poll(); keys.held = {38, 71}; keys.Poll();
    keys.now = 24; keys.held.erase(38); Require(!keys.Poll(), "Offset ran inside movement wait");
    keys.now = 25; Require(keys.Poll() == Input::IncreaseMenuPositionStep, "Established offset hold lost eligibility");
    keys.held.clear(); keys.now = 150; keys.Poll(); keys.held = {40};
    Require(keys.Poll() == Input::MoveMenuDown, "An exact-deadline fresh direction was swallowed");
}
void PendingConsumesNewEdgesAndRebasesCapturedWaitWithoutBursts() {
    Keys keys; keys.Poll(); keys.pending = true; keys.held = {81}; keys.Poll();
    keys.pending = false; keys.now = 1000; Require(!keys.Poll(), "Pending fresh offset replayed after ack");
    keys.Release(); keys.held = {71}; keys.Poll();
    keys.pending = true; keys.now += 100; keys.Poll(); keys.pending = false;
    keys.now += 124; Require(!keys.Poll(), "An established offset resumed before a complete rebased wait");
    ++keys.now; Require(keys.Poll() == Input::IncreaseMenuPositionStep, "Acknowledged offset failed to resume");
    Require(!keys.Poll(), "Pending acknowledgement produced a queued burst");
}
void EntryFocusContextPageAndModeChangesRequireAggregateRelease() {
    Keys keys; keys.held = {38}; keys.Poll(); keys.held.insert(71); keys.now = 1000;
    Require(!keys.Poll(), "Entry hold gained a second-action bypass");
    keys.Release(); keys.held = {38}; Require(keys.Poll() == Input::MoveMenuUp, "Released entry failed to recover");
    ++keys.context; keys.now += 25; Require(!keys.Poll(), "Navigation context inherited movement");
    keys.Release(); keys.held = {71}; keys.Poll(); ++keys.page; keys.now += 125;
    Require(!keys.Poll(), "Page identity inherited an offset hold");
    keys.Release(); keys.held = {38}; keys.Poll(); keys.foreground = false; keys.Poll();
    keys.foreground = true; keys.now += 1000; keys.Poll(); Require(!keys.Poll(), "Focus return replayed movement");
    keys.Release(); keys.held = {86}; keys.Poll(); keys.mode = InputMode::MenuLocked; keys.Poll();
    keys.mode = InputMode::Menu; keys.Poll(); keys.now += 1000;
    Require(!keys.Poll(), "Editor exit leaked as Back or Quick Exit");
    keys.open = false; keys.Poll(); keys.open = true; keys.mode = InputMode::MenuPositionEditor; keys.Poll();
    Require(!keys.Poll(), "Reopening leaked held exit into the editor");
}
void AimAttackUseAndChoiceAliasesCannotSubstituteForExactActions() {
    Keys keys; keys.Poll(); keys.held = {1, 2, 70};
    Require(!keys.Poll(), "Gameplay aliases became movement or finish");
    keys.now = 1000; Require(!keys.Poll(), "Ignored gameplay keys acquired delayed editor actions");
    keys.held = {38}; Require(keys.Poll() == Input::MoveMenuUp, "Exact movement was suppressed by ignored aliases");
}
void RollbackAndKeyFailureCancelActivityAndUnsignedLimitsRemainBounded() {
    Keys keys; keys.now = 100; keys.Poll(); keys.held = {71}; keys.Poll(); keys.now = 99;
    Require(!keys.Poll(), "Rollback crossed unsigned elapsed bounds");
    keys.now = 1000; Require(!keys.Poll(), "Rollback retained an offset repeat");
    keys.Release(); keys.held = {38}; keys.Poll(); bool rejected = false;
    try { keys.router.Poll(true, true, false, [](int) -> bool { throw std::runtime_error("Owned key failure"); },
        keys.now, 1, false, false, InputMode::MenuPositionEditor); }
    catch (const std::runtime_error&) { rejected = true; }
    Require(rejected, "A key failure was swallowed"); keys.Poll(); Require(!keys.Poll(), "Failed read retained action eligibility");
    keys.Release(); keys.now += 1000; keys.Poll(); --keys.now; keys.held = {38};
    Require(!keys.Poll(), "Idle rollback dispatched a fresh movement on an old clock");
    keys.Release(); keys.now = std::numeric_limits<std::uint64_t>::max() - 125; keys.held = {71}; keys.Poll();
    keys.now = std::numeric_limits<std::uint64_t>::max();
    Require(keys.Poll() == Input::IncreaseMenuPositionStep, "Unsigned-limit offset timing overflowed");
}
void MissingAnyExactGroupFailsBeforeReadingKeysAndLegacyMenuStillWorks() {
    for (int missing = 0; missing < 6; ++missing) {
        auto bindings = CompleteBindings();
        std::vector<int>* groups[]{&bindings.moveUp, &bindings.moveDown, &bindings.previousChoice,
            &bindings.nextChoice, &bindings.frag, &bindings.secondaryOffhand};
        groups[missing]->clear(); NativeInputRouter router(bindings); bool read = false, rejected = false;
        try { router.Poll(true, true, false, [&](int) { read = true; return false; },
            0, 1, false, false, InputMode::MenuPositionEditor); }
        catch (const std::invalid_argument&) { rejected = true; }
        Require(rejected && !read, "Incomplete saved controls reached key IO or enabled editor");
    }
    NativeInputRouter legacy(Bindings{{2}, {86}, {2}, {1}, {70}});
    legacy.Poll(true, true, false, [](int) { return false; }, 0);
    Require(legacy.Poll(true, true, false, [](int key) { return key == 70; }, 1) == Input::Select,
        "Optional grenade groups broke legacy navigation");
}
void HeldMovementChangesFollowSourcePriorityAtTheWaitDeadline() {
    Keys keys; keys.Poll(); keys.held = {38}; keys.Poll();
    keys.now = 20; keys.held.insert(40); Require(!keys.Poll(), "Held Down shortened the captured wait");
    keys.now = 25; Require(keys.Poll() == Input::MoveMenuDown, "Held Down did not win at the source deadline");
    keys.now = 30; keys.held.insert(52); keys.held.insert(86); keys.Poll();
    keys.now = 50; Require(keys.Poll() == Input::MoveMenuDown, "Horizontal or consumed melee bypassed vertical priority");
    keys.held = {86}; keys.now = 75; Require(!keys.Poll(), "Wait-time melee became delayed exit");
    keys.Release(); keys.held = {71}; keys.Poll(); keys.now += 20; keys.held.insert(81); keys.Poll();
    keys.now += 105; Require(keys.Poll() == Input::DecreaseMenuPositionStep, "Held smoke did not win at the offset deadline");
}
}
int main() {
    try {
        ExactSavedMovementAndGrenadeGroupsDispatchSevenDistinctCommands();
        SourcePriorityChoosesDownRightAndSmokeBeforeFragAndMelee();
        MovementWaitIsTwentyFiveMillisecondsWithoutAlternateKeyAcceleration();
        GrenadeWaitIsOneHundredTwentyFiveMillisecondsIncludingBoundedNoOps();
        ReleasedActionsRetainGlobalWaitAndConsumeReleasedMovementAndExitEdges();
        FreshEdgesAtDeadlineAreAcceptedAndEstablishedLowerHoldsRemainEligible();
        PendingConsumesNewEdgesAndRebasesCapturedWaitWithoutBursts();
        EntryFocusContextPageAndModeChangesRequireAggregateRelease();
        AimAttackUseAndChoiceAliasesCannotSubstituteForExactActions();
        RollbackAndKeyFailureCancelActivityAndUnsignedLimitsRemainBounded();
        MissingAnyExactGroupFailsBeforeReadingKeysAndLegacyMenuStillWorks();
        HeldMovementChangesFollowSourcePriorityAtTheWaitDeadline();
        std::cout << "Passed 12 menu-position input checks\n";
        return 0;
    } catch (const std::exception& exception) { std::cerr << "FAIL " << exception.what() << '\n'; return 1; }
}
