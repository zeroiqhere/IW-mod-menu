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
// Owned keys exercise the real router without reading desktop state
struct Keys {
    NativeInputRouter router{Bindings{{2}, {86, 6}, {2, 38, 87}, {1, 40, 83}, {70},
        {51, 37}, {52, 39}, {1}, {38, 87}, {40, 83}}};
    std::set<int> held;
    bool foreground = true, open = true, pending = false;
    std::uint64_t now = 0, revision = 1;
    InputMode mode = InputMode::InstructionPositionEditor;
    auto Poll() { return router.Poll(foreground, open, pending,
        [&](int key) { return held.contains(key); }, now, revision, true, false, mode); }
    void Release() { held.clear(); Poll(); }
};
void PositionLeafRequiresAReleasedSeedAndDispatchesFreshExactMovement() {
    InstructionPositionInput input;
    // A held key from entry must not become the first movement
    input.Reset({true});
    Require(!input.Poll({true}, false, false, 1000), "Startup movement must wait for release");
    input.Poll({}, false, false, 1000);
    Require(input.Poll({true}, false, false, 1000) == Input::MoveInstructionsUp,
        "The source position leaf ignored a fresh slot-one movement");
}
void ExactSavedActionSlotsMoveInAllFourDirections() {
    Keys keys; keys.Poll();
    for (const auto& [key, action] : {std::pair{87, Input::MoveInstructionsUp}, {83, Input::MoveInstructionsDown},
        {37, Input::MoveInstructionsLeft}, {39, Input::MoveInstructionsRight}}) {
        keys.Release(); keys.held = {key};
        Require(keys.Poll() == action, "A remapped position key lost its exact action");
    }
}
void VerticalPriorityAndDownRightConflictWinnersMatchTheSource() {
    Keys keys; keys.Poll(); keys.held = {38, 40, 51, 52, 86};
    Require(keys.Poll() == Input::MoveInstructionsDown, "Down must win vertical conflicts before horizontal or melee");
    keys.Release(); keys.held = {38, 52};
    Require(keys.Poll() == Input::MoveInstructionsUp, "Vertical movement must win over right");
    keys.Release(); keys.held = {51, 52};
    Require(keys.Poll() == Input::MoveInstructionsRight, "Right must win horizontal conflicts");
    // Releasing the winning direction exposes the remaining source branch
    keys.held = {51}; keys.now = 25;
    Require(keys.Poll() == Input::MoveInstructionsLeft, "A remaining eligible direction follows source held priority");
}
void PositionHoldsRepeatAtTwentyFiveMillisecondsWithoutCatchupBursts() {
    Keys keys; keys.Poll(); keys.held = {38}; keys.Poll();
    keys.now = 24; Require(!keys.Poll(), "Position repeat ran before the source loop interval");
    // Two saved keys still represent one action group
    keys.held.insert(87); Require(!keys.Poll(), "An alternate saved key accelerated the same action hold");
    keys.now = 25; Require(keys.Poll() == Input::MoveInstructionsUp, "Position repeat omitted the source 25ms wait");
    keys.now = 1000; Require(keys.Poll() == Input::MoveInstructionsUp, "A delayed poll must emit one movement");
    Require(!keys.Poll(), "A delayed position poll emitted a catchup burst");
}
void MeleeFinishesOnceOnlyAfterMovementStops() {
    Keys keys; keys.Poll(); keys.held = {40, 86, 70};
    Require(keys.Poll() == Input::MoveInstructionsDown, "Movement must suppress melee exit and Use");
    keys.held = {86}; keys.now = 25;
    Require(keys.Poll() == Input::FinishInstructionPositionEditor, "Melee must exit once movement stops");
    keys.now = 1000; Require(!keys.Poll(), "Held melee repeated finish or triggered Quick Exit");
}
void AimAttackAndUseCannotSubstituteForPositionBindings() {
    Keys keys; keys.Poll(); keys.held = {1, 2, 70};
    Require(!keys.Poll(), "Aim, attack or Use substituted for a saved movement action");
    keys.now = 1000; Require(!keys.Poll(), "Ignored gameplay keys later moved the instructions");
    keys.held = {87}; Require(keys.Poll() == Input::MoveInstructionsUp, "Exact remapped Up remained unavailable");
}
void StartupAndContextChangesRequireCompleteAggregateRelease() {
    Keys keys; keys.held = {38}; keys.Poll(); keys.held.insert(40); keys.now = 1000;
    Require(!keys.Poll(), "A second movement group bypassed startup release");
    keys.Release(); keys.held = {40}; Require(keys.Poll() == Input::MoveInstructionsDown, "Release did not recover position input");
    ++keys.revision; keys.now = 1025; Require(!keys.Poll(), "A new editor context inherited the old movement");
    keys.now = 2000; Require(!keys.Poll(), "Context cancellation replayed a held movement");
    keys.Release(); keys.held = {40}; Require(keys.Poll() == Input::MoveInstructionsDown, "A fresh context press was lost");
}
void PendingEdgesAreConsumedAndExistingHoldsResumeAfterAFullInterval() {
    Keys keys; keys.Poll(); keys.pending = true; keys.held = {38}; keys.Poll();
    // A pending fresh edge stays consumed even after acknowledgement
    keys.pending = false; keys.now = 1000;
    Require(!keys.Poll(), "A movement pressed during pending replayed after acknowledgement");
    keys.Release(); keys.held = {38}; keys.Poll(); keys.pending = true; keys.now = 1010; keys.Poll();
    keys.pending = false; keys.now = 1034; Require(!keys.Poll(), "Pending movement resumed before a full repeat interval");
    keys.now = 1035; Require(keys.Poll() == Input::MoveInstructionsUp, "An acknowledged existing hold failed to resume");
}
void FocusMenuAndPhaseTransitionsReseedHeldEditorActions() {
    Keys keys; keys.Poll(); keys.held = {40}; keys.Poll();
    keys.foreground = false; Require(!keys.Poll(), "Lost focus still moved instructions");
    keys.foreground = true; keys.now = 1000; keys.Poll(); keys.now = 2000;
    Require(!keys.Poll(), "Focus return resumed an old movement");
    keys.Release(); keys.held = {86}; keys.mode = InputMode::MenuLocked; keys.Poll();
    keys.mode = InputMode::InstructionPositionEditor; keys.Poll();
    Require(!keys.Poll(), "Entering-to-editing replayed a held exit");
    keys.Release(); keys.held = {86}; Require(keys.Poll() == Input::FinishInstructionPositionEditor, "Fresh exit after entry was lost");
    // The finish hold must not escape into Back on the returned page
    keys.mode = InputMode::MenuLocked; keys.Poll(); keys.mode = InputMode::Menu; keys.Poll();
    keys.now = 3000; Require(!keys.Poll(), "Editor exit leaked into normal Back or Close");
    keys.open = false; keys.Poll(); keys.open = true; keys.mode = InputMode::InstructionPositionEditor; keys.Poll();
    Require(!keys.Poll(), "Reopening inherited a held editor action");
}
void RollbackAndKeyReadFailureCancelPositionActivity() {
    Keys keys; keys.now = 100; keys.Poll(); keys.held = {38}; keys.Poll();
    keys.now = 99; Require(!keys.Poll(), "Rollback must disarm position repeat before unsigned subtraction");
    keys.now = 1000; Require(!keys.Poll(), "Rollback replayed a delayed movement");
    keys.Release(); keys.held = {38}; keys.Poll();
    // A reader failure resets the same state as focus loss
    bool rejected = false;
    try { keys.router.Poll(true, true, false, [](int) -> bool { throw std::runtime_error("Owned fixture read failure"); },
        1000, 1, false, false, InputMode::InstructionPositionEditor); }
    catch (const std::runtime_error&) { rejected = true; }
    Require(rejected, "The router swallowed a key-reader failure");
    keys.now = 2000; keys.Poll(); Require(!keys.Poll(), "A failed key read retained editor movement");
    keys.Release(); keys.now = std::numeric_limits<std::uint64_t>::max() - 25; keys.held = {38}; keys.Poll();
    keys.now = std::numeric_limits<std::uint64_t>::max();
    Require(keys.Poll() == Input::MoveInstructionsUp, "Near-limit timestamps lost the bounded source repeat");
}
void IncompleteSavedGroupsCannotEnablePositionEditing() {
    const Bindings complete{{2}, {86}, {2, 38}, {1, 40}, {70}, {51}, {52}, {1}, {38}, {40}};
    // Every missing direction disables the complete modal route
    for (int missing = 0; missing < 4; ++missing) {
        auto bindings = complete;
        if (missing == 0) bindings.moveUp.clear();
        if (missing == 1) bindings.moveDown.clear();
        if (missing == 2) bindings.previousChoice.clear();
        if (missing == 3) bindings.nextChoice.clear();
        NativeInputRouter router(bindings);
        bool rejected = false;
        try { router.Poll(true, true, false, [](int) { return false; }, 0, 1, false, false, InputMode::InstructionPositionEditor); }
        catch (const std::invalid_argument&) { rejected = true; }
        Require(rejected, "Incomplete saved controls invented a position route");
    }
}
}
int main() {
    try {
        PositionLeafRequiresAReleasedSeedAndDispatchesFreshExactMovement();
        ExactSavedActionSlotsMoveInAllFourDirections();
        VerticalPriorityAndDownRightConflictWinnersMatchTheSource();
        PositionHoldsRepeatAtTwentyFiveMillisecondsWithoutCatchupBursts();
        MeleeFinishesOnceOnlyAfterMovementStops();
        AimAttackAndUseCannotSubstituteForPositionBindings();
        StartupAndContextChangesRequireCompleteAggregateRelease();
        PendingEdgesAreConsumedAndExistingHoldsResumeAfterAFullInterval();
        FocusMenuAndPhaseTransitionsReseedHeldEditorActions();
        RollbackAndKeyReadFailureCancelPositionActivity();
        IncompleteSavedGroupsCannotEnablePositionEditing();
        std::cout << "Passed 11 instruction-position input checks\n";
        return 0;
    } catch (const std::exception& exception) {
        std::cerr << "FAIL " << exception.what() << '\n';
        return 1;
    }
}
