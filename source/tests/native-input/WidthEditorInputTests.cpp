#include "NativeInputRouter.h"

#include <iostream>
#include <limits>
#include <set>
#include <stdexcept>
#include <string>

using iw::overlay::runtime::Bindings;
using iw::overlay::runtime::Input;
using iw::overlay::runtime::InputMode;
using iw::overlay::runtime::NativeInputRouter;
namespace {
// These keys model separate attack and action-slot groups without reading a game
struct Keys {
    NativeInputRouter router{Bindings{{2}, {86, 6}, {2, 38}, {1, 65, 40}, {70}, {51}, {52}, {1, 65}}};
    std::set<int> held;
    bool allowed = true, pending = false, open = true;
    std::uint64_t now = 0, revision = 1;
    InputMode mode = InputMode::WidthEditor;
    auto Poll() { return router.Poll(allowed, open, pending,
        [&](int key) { return held.contains(key); }, now, revision, true, false, mode); }
    void Seed() { Poll(); }
};
void Require(bool value, const char* message) {
    if (!value) throw std::runtime_error(message);
}
void AttackAndAimChangeWidthWithSeventyFiveMillisecondRepeat() {
    Keys keys; keys.Seed(); keys.held = {1};
    Require(keys.Poll() == Input::IncreaseWidth, "Attack must resize rather than scroll the menu");
    keys.now = 74; Require(!keys.Poll(), "Width repeat must include the source loop wait");
    keys.now = 75; Require(keys.Poll() == Input::IncreaseWidth, "Width repeat uses 50+25 ms");
    keys.now = 1000; Require(keys.Poll() == Input::IncreaseWidth, "A delayed poll emits one width change");
    Require(!keys.Poll(), "Width changes must not catch up in a burst");
    keys.held.clear(); keys.Poll(); keys.held = {2};
    Require(keys.Poll() == Input::DecreaseWidth, "ADS must decrease the width");
    keys.now = 1075; Require(keys.Poll() == Input::DecreaseWidth, "ADS repeats at the same width interval");
}
void StepAdjustmentUsesOneHundredTwentyFiveMilliseconds() {
    Keys keys; keys.Seed(); keys.held = {52};
    Require(keys.Poll() == Input::IncreaseWidthStep, "Slot 4 increases the editor offset");
    keys.now = 124; Require(!keys.Poll(), "Offset repeat must not omit the monitor wait");
    keys.now = 125; Require(keys.Poll() == Input::IncreaseWidthStep, "Offset repeat uses 100+25 ms");
    keys.held.clear(); keys.Poll(); keys.held = {51};
    Require(keys.Poll() == Input::DecreaseWidthStep, "Slot 3 decreases the editor offset");
    keys.now = 250; Require(keys.Poll() == Input::DecreaseWidthStep, "Decrement uses the same offset interval");
}
void SourceAttackAndStepPrioritiesRemainDeterministic() {
    Keys keys; keys.Seed(); keys.held = {1, 2, 51, 52, 86};
    Require(keys.Poll() == Input::IncreaseWidth, "Source attack wins over ADS, slots and melee");
    keys.held.clear(); keys.Poll(); keys.held = {2, 51, 52};
    Require(keys.Poll() == Input::DecreaseWidth, "ADS wins over offset changes");
    keys.held.clear(); keys.Poll(); keys.held = {51, 52};
    Require(keys.Poll() == Input::DecreaseWidthStep, "Source slot 3 wins over slot 4");
}
void MeleeFinishesImmediatelyWithoutQuickExitOrFeatureSelection() {
    Keys keys; keys.Seed(); keys.held = {86, 70};
    Require(keys.Poll() == Input::FinishWidthEditor, "Editor knife returns to customization immediately");
    keys.now = 1000; Require(!keys.Poll(), "Holding editor knife cannot invoke Quick Exit or repeat finish");
}
void OtherMenuInputsCannotDispatchFromTheEditor() {
    Keys keys; keys.Seed(); keys.held = {38, 40, 70};
    Require(!keys.Poll(), "Action slots 1/2 and Use are not width-editor controls");
    keys.now = 1000; Require(!keys.Poll(), "Ignored held menu inputs cannot later select or resize");
    keys.held = {65}; Require(keys.Poll() == Input::IncreaseWidth, "A remapped attack key must resize");
}
void EditorEntryAndReturnReseedAllHeldActions() {
    Keys keys; keys.mode = InputMode::Menu; keys.Seed(); keys.held = {1, 86};
    keys.mode = InputMode::WidthEditor; ++keys.revision;
    Require(!keys.Poll(), "Entering the editor cannot inherit a firing or knife hold");
    keys.now = 1000; Require(!keys.Poll(), "Inherited editor actions require aggregate release");
    keys.held.clear(); keys.Poll(); keys.held = {86};
    Require(keys.Poll() == Input::FinishWidthEditor, "A fresh knife can finish after release");
    keys.mode = InputMode::Menu; ++keys.revision;
    Require(!keys.Poll(), "Returning to customization cannot inherit knife Back");
    keys.now = 2000; Require(!keys.Poll(), "An editor finish cannot become a root close");
}
void StartupHeldActionsRequireCompleteRelease() {
    Keys keys; keys.held = {1}; keys.Seed(); keys.held.insert(65);
    Require(!keys.Poll(), "Another attack key cannot bypass startup release");
    keys.now = 1000; Require(!keys.Poll(), "Startup firing must never resize");
    keys.held.clear(); keys.Poll(); keys.held = {65};
    Require(keys.Poll() == Input::IncreaseWidth, "Fresh alternate attack recovers after release");
}
void ContextChangesCancelHeldRepeatsUntilRelease() {
    Keys keys; keys.Seed(); keys.held = {52}; keys.Poll();
    ++keys.revision; keys.now = 125;
    Require(!keys.Poll(), "A changed editor context cannot inherit an offset repeat");
    ++keys.revision; keys.now = 1000;
    Require(!keys.Poll(), "Returning to the same editor still requires release");
    keys.held.clear(); keys.Poll(); keys.held = {52};
    Require(keys.Poll() == Input::IncreaseWidthStep, "New input works after cancellation");
}
void PendingFreshInputsAreConsumedAndExistingRepeatsResumeAfterAFullInterval() {
    Keys keys; keys.Seed(); keys.pending = true; keys.held = {1}; keys.Poll();
    keys.pending = false; keys.now = 1000;
    Require(!keys.Poll(), "A fresh pending attack cannot replay after acknowledgement");
    keys.held.clear(); keys.Poll(); keys.held = {1};
    Require(keys.Poll() == Input::IncreaseWidth, "A fresh attack arms a new repeat");
    keys.pending = true; keys.now = 1010; keys.Poll();
    keys.pending = false; keys.now = 1084; Require(!keys.Poll(), "Acknowledged resize needs a full interval");
    keys.now = 1085; Require(keys.Poll() == Input::IncreaseWidth, "Held resize resumes after 75 ms");
}
void FocusAndClosedMenuCancelAllEditorActivity() {
    Keys keys; keys.Seed(); keys.held = {2}; keys.Poll();
    keys.allowed = false; Require(!keys.Poll(), "Lost focus must stop width commands");
    keys.allowed = true; keys.now = 1000; keys.Poll(); keys.now = 2000;
    Require(!keys.Poll(), "Returning focus must not resume old editor input");
    keys.held.clear(); keys.Poll(); keys.held = {1}; keys.Poll();
    keys.open = false; keys.Poll(); keys.open = true; keys.Poll(); keys.now = 3000;
    Require(!keys.Poll(), "A reopened menu cannot inherit an editor resize");
}
void ClockRollbackCancelsAndLargeTimestampsStayBounded() {
    Keys keys; keys.now = 100; keys.Seed(); keys.held = {1}; keys.Poll();
    keys.now = 99; Require(!keys.Poll(), "Clock rollback must stop resize repeats");
    keys.now = 1000; Require(!keys.Poll(), "Rollback cannot become a delayed width change");
    keys.held.clear(); keys.Poll(); keys.now = std::numeric_limits<std::uint64_t>::max() - 75;
    keys.held = {1}; Require(keys.Poll() == Input::IncreaseWidth, "Near-limit time accepts a fresh action");
    keys.now = std::numeric_limits<std::uint64_t>::max();
    Require(keys.Poll() == Input::IncreaseWidth, "Elapsed subtraction preserves the width interval near uint64 max");
}
void AlternateAttackKeysDoNotAccelerateAnExistingHold() {
    Keys keys; keys.Seed(); keys.held = {1}; keys.Poll(); keys.now = 10; keys.held.insert(65);
    Require(!keys.Poll(), "Another attack edge cannot skip the repeat interval");
    keys.now = 75; Require(keys.Poll() == Input::IncreaseWidth, "Aggregate attack holds retain their first repeat time");
    keys.held = {65}; keys.now = 100; Require(!keys.Poll(), "Releasing one alternate key is not a new press");
}
void KeyReaderFailureDiscardsThePreviousEditorHold() {
    Keys keys; keys.Seed(); keys.held = {1}; keys.Poll();
    try {
        keys.router.Poll(true, true, false, [](int) -> bool { throw std::runtime_error("Owned key failure"); },
            10, 1, true, false, InputMode::WidthEditor);
        throw std::logic_error("Key error was not propagated");
    } catch (const std::runtime_error&) {}
    keys.now = 1000; Require(!keys.Poll(), "Key failure requires a fresh seed");
    keys.now = 2000; Require(!keys.Poll(), "Failed key reads cannot retain resize activity");
}
void LegacyBindingsCannotGuessAnAttackControlForTheEditor() {
    NativeInputRouter legacy{Bindings{{2}, {86}, {2}, {1, 40}, {70}, {51}, {52}}};
    try {
        legacy.Poll(true, true, false, [](int) { return false; }, 0, 1, false, false, InputMode::WidthEditor);
    } catch (const std::invalid_argument&) { return; }
    throw std::runtime_error("A legacy combined Down group cannot identify exact attack controls");
}
void ExactEditorCommandsHaveDistinctProtocolNames() {
    const auto pairs = {std::pair{Input::IncreaseWidth, "increaseWidth"}, {Input::DecreaseWidth, "decreaseWidth"},
        {Input::IncreaseWidthStep, "increaseWidthStep"}, {Input::DecreaseWidthStep, "decreaseWidthStep"},
        {Input::FinishWidthEditor, "finishWidthEditor"}};
    for (const auto& [input, expected] : pairs) {
        Require(std::string(iw::overlay::runtime::InputName(input)) == expected, "Editor input names must be exact");
    }
}
}
int main() {
    try {
        AttackAndAimChangeWidthWithSeventyFiveMillisecondRepeat();
        StepAdjustmentUsesOneHundredTwentyFiveMilliseconds();
        SourceAttackAndStepPrioritiesRemainDeterministic();
        MeleeFinishesImmediatelyWithoutQuickExitOrFeatureSelection();
        OtherMenuInputsCannotDispatchFromTheEditor();
        EditorEntryAndReturnReseedAllHeldActions();
        StartupHeldActionsRequireCompleteRelease();
        ContextChangesCancelHeldRepeatsUntilRelease();
        PendingFreshInputsAreConsumedAndExistingRepeatsResumeAfterAFullInterval();
        FocusAndClosedMenuCancelAllEditorActivity();
        ClockRollbackCancelsAndLargeTimestampsStayBounded();
        AlternateAttackKeysDoNotAccelerateAnExistingHold();
        KeyReaderFailureDiscardsThePreviousEditorHold();
        LegacyBindingsCannotGuessAnAttackControlForTheEditor();
        ExactEditorCommandsHaveDistinctProtocolNames();
        std::cout << "Passed 15 width-editor input checks\n";
    } catch (const std::exception& error) { std::cerr << "FAIL " << error.what() << '\n'; return 1; }
}
