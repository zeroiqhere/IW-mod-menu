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
struct Keys {
    NativeInputRouter router{Bindings{{2}, {86}, {2, 38}, {1, 40}, {70}, {51}, {52}}};
    std::set<int> held;
    bool allowed = true, open = true, pending = false;
    std::uint64_t now = 0, context = 1;
    auto Poll() { return router.Poll(allowed, open, pending,
        [&](int key) { return held.contains(key); }, now, context); }
};
void FreshSavedChoiceKeysUseExactCommandNames() {
    Keys keys; keys.Poll(); keys.held = {51};
    Require(keys.Poll() == Input::PreviousChoice, "Saved previous-choice press was not routed");
    Require(std::string(InputName(Input::PreviousChoice)) == "previousChoice", "Previous command name differs");
    keys.held.clear(); keys.Poll(); keys.held = {52};
    Require(keys.Poll() == Input::NextChoice, "Saved next-choice press was not routed");
    Require(std::string(InputName(Input::NextChoice)) == "nextChoice", "Next command name differs");
}
void HeldChoiceRepeatsAfter180MillisecondsWithoutBursts() {
    Keys keys; keys.Poll(); keys.held = {52}; keys.Poll();
    keys.now = 179; Require(!keys.Poll(), "A choice repeated before the source interval");
    keys.now = 180; Require(keys.Poll() == Input::NextChoice, "Held choice did not repeat at 180ms");
    keys.now = 5000; Require(keys.Poll() == Input::NextChoice, "Slow polling lost the held direction");
    Require(!keys.Poll(), "Slow polling emitted a catch-up burst");
}
void OpposingChoiceKeysAreNeutralAndAllowFreshUse() {
    Keys keys; keys.Poll(); keys.held = {51, 52};
    Require(!keys.Poll(), "Opposing choice keys changed a draft");
    keys.now = 1000; Require(!keys.Poll(), "Opposing held choices repeated");
    keys.held.insert(70); Require(keys.Poll() == Input::Select, "Neutral choices swallowed fresh Use");
}
void FocusAndClosedMenuCannotReplayHeldChoices() {
    Keys keys; keys.Poll(); keys.held = {51}; keys.Poll();
    keys.allowed = false; Require(!keys.Poll(), "Lost focus emitted a choice");
    keys.allowed = true; keys.now = 1000; Require(!keys.Poll(), "Focus return replayed a choice");
    keys.now = 2000; Require(!keys.Poll(), "Held choice rearmed without release");
    keys.held.clear(); keys.Poll(); keys.open = false; keys.Poll(); keys.held = {52};
    Require(!keys.Poll(), "Closed menu emitted a choice");
    keys.open = true; Require(!keys.Poll(), "Opening replayed a held choice");
}
void PendingChoicesConsumeFreshEdgesAndDelayExistingRepeat() {
    // An acknowledged command cannot replay the press consumed while it was pending
    Keys keys; keys.Poll(); keys.pending = true; keys.held = {51};
    Require(!keys.Poll(), "Pending command emitted a choice");
    keys.pending = false; keys.now = 1000;
    Require(!keys.Poll(), "A consumed fresh edge armed a repeat");
    keys.held.clear(); keys.Poll(); keys.held = {51}; keys.Poll();
    keys.pending = true; keys.now = 1300; Require(!keys.Poll(), "Pending repeat was emitted");
    keys.pending = false; Require(!keys.Poll(), "Ack replayed an expired choice repeat");
    keys.now = 1479; Require(!keys.Poll(), "Pending delay was shorter than 180ms");
    keys.now = 1480; Require(keys.Poll() == Input::PreviousChoice, "Held choice did not recover after ack");
}
void ChangedSelectionContextRequiresChoiceRelease() {
    // A new row owns a new deliberate press even when the previous key remains held
    Keys keys; keys.Poll(); keys.held = {52}; keys.Poll();
    keys.context = 2; keys.now = 500;
    Require(!keys.Poll(), "Changing rows replayed a held choice");
    keys.now = 1000; Require(!keys.Poll(), "New row inherited the previous choice repeat");
    keys.held.clear(); keys.Poll(); keys.held = {52};
    Require(keys.Poll() == Input::NextChoice, "Released choice did not work in its new row");
}
void FreshUseDisarmsChoiceRepeats() {
    Keys keys; keys.Poll(); keys.held = {52}; keys.Poll(); keys.now = 180; keys.held.insert(70);
    Require(keys.Poll() == Input::Select, "Source Use must precede a due slider");
    keys.held = {52}; keys.now = 1000;
    Require(!keys.Poll(), "Committed choice continued without release");
}
void DueChoicePrecedesKnifeAndKeepsItsOwnRepeat() {
    Keys keys; keys.Poll(); keys.held = {52}; keys.Poll(); keys.now = 180; keys.held.insert(86);
    Require(keys.Poll() == Input::NextChoice, "Knife overtook the source slider branch");
    keys.held = {52}; keys.now = 1000;
    Require(keys.Poll() == Input::NextChoice, "Discarded knife ended the source slider repeat");
    keys.held.clear();keys.Poll();keys.now = 1180;keys.held = {86};
    Require(keys.Poll() == Input::Back, "A later deliberate knife failed after slider release");
}
void NavigationDoesNotCarryHeldChoicesIntoAnotherRow() {
    Keys keys; keys.Poll(); keys.held = {52}; keys.Poll(); keys.held.insert(1);
    Require(keys.Poll() == Input::Down, "Fresh navigation did not retain priority");
    keys.held = {52}; keys.now = 1000;
    Require(!keys.Poll(), "Navigation carried a held choice to another row");
}
void ChoiceClockRollbackDisarmsAndLargeValuesRemainBounded() {
    Keys keys; keys.now = 100; keys.Poll(); keys.held = {51}; keys.Poll();
    keys.now = 99; Require(!keys.Poll(), "Clock rollback emitted a choice");
    keys.now = 1000; Require(!keys.Poll(), "Clock rollback rearmed a choice");
    keys.held.clear(); keys.Poll(); keys.now = std::numeric_limits<std::uint64_t>::max() - 179;
    keys.held = {51}; Require(keys.Poll() == Input::PreviousChoice, "Large clock rejected a fresh choice");
    keys.now = std::numeric_limits<std::uint64_t>::max();
    Require(!keys.Poll(), "Large clock wrapped the repeat deadline");
}
void MissingOptionalKeysDoNotInventChoiceBindings() {
    NativeInputRouter router{Bindings{{2}, {86}, {2}, {1}, {70}}};
    router.Poll(true, true, false, [](int) { return false; }, 0);
    Require(!router.Poll(true, true, false, [](int key) { return key == 51 || key == 52; }, 1000),
        "Missing optional groups guessed number-key bindings");
}
void OptionalChoicesRejectOverlappingDuplicateAndOutOfRangeKeys() {
    for (int variation = 0; variation < 4; ++variation) {
        Bindings bindings{{2}, {86}, {2}, {1}, {70}, {51}, {52}};
        if (variation == 0) bindings.previousChoice = {70};
        if (variation == 1) bindings.nextChoice = {51};
        if (variation == 2) bindings.previousChoice = {51, 51};
        if (variation == 3) bindings.nextChoice = {255};
        bool rejected = false;
        try { ValidateBindings(bindings); } catch (const std::invalid_argument&) { rejected = true; }
        Require(rejected, "An ambiguous or invalid optional choice binding was accepted");
    }
}
void KeyReadFailureReseedsHeldChoicesBeforeAnyFurtherInput() {
    Keys keys; keys.Poll(); keys.held = {52}; keys.Poll();
    bool failed = false;
    try {
        keys.router.Poll(true, true, false, [](int key) {
            if (key == 52) throw std::runtime_error("owned key source failed");
            return false;
        }, 180, keys.context);
    } catch (const std::runtime_error&) { failed = true; }
    Require(failed, "Key source failure was silently accepted");
    keys.now = 1000;
    Require(!keys.Poll(), "Failed key reads replayed the held choice on recovery");
    keys.now = 2000;
    Require(!keys.Poll(), "Recovered held choices rearmed before release");
}
}

int main() {
    try {
        FreshSavedChoiceKeysUseExactCommandNames();
        HeldChoiceRepeatsAfter180MillisecondsWithoutBursts();
        OpposingChoiceKeysAreNeutralAndAllowFreshUse();
        FocusAndClosedMenuCannotReplayHeldChoices();
        PendingChoicesConsumeFreshEdgesAndDelayExistingRepeat();
        ChangedSelectionContextRequiresChoiceRelease();
        FreshUseDisarmsChoiceRepeats();
        DueChoicePrecedesKnifeAndKeepsItsOwnRepeat();
        NavigationDoesNotCarryHeldChoicesIntoAnotherRow();
        ChoiceClockRollbackDisarmsAndLargeValuesRemainBounded();
        MissingOptionalKeysDoNotInventChoiceBindings();
        OptionalChoicesRejectOverlappingDuplicateAndOutOfRangeKeys();
        KeyReadFailureReseedsHeldChoicesBeforeAnyFurtherInput();
        std::cout << "Passed 13 native choice-input checks\n";
    } catch (const std::exception& error) {
        std::cerr << "FAIL " << error.what() << '\n';
        return 1;
    }
}
