#include "NativeInputRouter.h"
#include "OpeningActionBindings.h"
#include "ScrollSpacerCounts.h"
#include <iostream>
#include <limits>
#include <set>
#include <stdexcept>
using namespace iw::overlay;
using namespace iw::overlay::runtime;
namespace {
void Require(bool value, const char *message) {
    if (!value) {
        throw std::runtime_error(message);
    }
}
template <class Call> void Reject(Call call) {
    try {
        call();
    } catch (const std::exception &) {
        return;
    }
    throw std::runtime_error("Expected rejection");
}
struct Keys {
    Bindings bindings{{2},  {86, 6}, {2, 38}, {1, 40}, {70}, {51}, {52}, {1},
                      {38}, {40},    {71},    {69},    {16}, {17}, {32}};
    NativeInputRouter router{bindings};
    MainOpeningControls policy{1, {OpeningAction::Aim, OpeningAction::Melee}};
    std::set<int> held;
    bool focus = true, open = false, pending = false, typed = true;
    std::uint64_t now = 0, page = 1;
    InputMode mode = InputMode::Menu;
    int duration = 120;
    unsigned upSpacers = 0, downSpacers = 0;
    auto Poll() {
        return router.Poll(
            focus, open, pending, [&](int key) { return held.contains(key); }, now, 1, false, true, mode,
            duration, page, typed ? &policy : nullptr, upSpacers, downSpacers);
    }
    void Release() {
        held.clear();
        Poll();
    }
};
void EveryActionUsesItsExactSavedGroup() {
    for (unsigned id = 0; id < OpeningActionCount; ++id) {
        Keys keys;
        const auto action = static_cast<OpeningAction>(id);
        keys.policy.actions = {action};
        keys.Poll();
        keys.held = {254};
        Require(!keys.Poll(), "An unbound key opened a menu");
        keys.held = {OpeningActionKeys(keys.bindings, action).front()};
        Require(keys.Poll() == Input::Toggle, "An exact saved opening action was ignored");
        Require(!keys.Poll(), "A held single action repeated Toggle");
    }
}
void FullChordCompletesInEitherOrderAndNeedsEveryAction() {
    for (bool aimFirst : {false, true}) {
        Keys keys;
        keys.policy.actions.push_back(OpeningAction::Sprint);
        keys.Poll();
        keys.held = {aimFirst ? 2 : 86};
        Require(!keys.Poll(), "Partial chord opened");
        keys.held.insert(aimFirst ? 86 : 2);
        Require(!keys.Poll(), "Two of three groups opened");
        keys.held.insert(16);
        Require(keys.Poll() == Input::Toggle, "Fresh completion failed");
        Require(!keys.Poll(), "Complete hold repeated");
    }
}
void MultiplePhysicalKeysDoNotCreateAnotherAggregatePress() {
    Keys keys;
    keys.policy.actions = {OpeningAction::Melee};
    keys.Poll();
    keys.held = {6};
    Require(keys.Poll() == Input::Toggle, "Remapped melee did not open");
    keys.held.insert(86);
    Require(!keys.Poll(), "Second bound key duplicated opening");
    keys.held = {86};
    Require(!keys.Poll(), "Changing held bound key duplicated opening");
    keys.Release();
    keys.held = {86};
    Require(keys.Poll() == Input::Toggle, "Complete release did not rearm");
}
void InstallationConsumesFullHoldButAllowsFreshPartialCompletion() {
    Keys full;
    full.held = {2, 86};
    full.Poll();
    Require(!full.Poll(), "Installation inherited a full chord");
    full.Release();
    full.held = {2, 86};
    Require(full.Poll() == Input::Toggle, "Fresh full chord lost");
    Keys partial;
    partial.held = {2};
    partial.Poll();
    partial.held.insert(86);
    Require(partial.Poll() == Input::Toggle, "Genuinely fresh partial completion was blocked");
}
void PolicyRevisionTupleAndLegacyChangesReseedWholeChord() {
    Keys keys;
    keys.Poll();
    keys.held = {2, 86};
    ++keys.policy.revision;
    Require(!keys.Poll() && !keys.Poll(), "New policy revision carried a full hold");
    keys.Release();
    keys.held = {2, 86};
    Require(keys.Poll() == Input::Toggle, "Revision fresh retry failed");
    keys.policy.actions = {OpeningAction::Melee};
    Require(!keys.Poll(), "Changed tuple at same revision inherited hold");
    keys.typed = false;
    Require(!keys.Poll(), "Legacy fallback inherited hold");
    keys.Release();
    keys.held = {2};
    keys.Poll();
    keys.held.insert(86);
    Require(keys.Poll() == Input::Toggle, "Legacy fallback lost its normal aim plus fresh knife gesture");
}
void PendingCompletionIsConsumedBeforeAcknowledgement() {
    Keys keys;
    keys.Poll();
    keys.pending = true;
    keys.held = {2, 86};
    Require(!keys.Poll(), "Pending opening escaped");
    keys.pending = false;
    Require(!keys.Poll(), "ACK replayed a complete held chord");
    keys.held = {2, 6};
    Require(!keys.Poll(), "Alternate knife bypassed consumed aggregate hold");
    keys.Release();
    keys.held = {2};
    keys.pending = true;
    keys.Poll();
    keys.pending = false;
    keys.held.insert(86);
    Require(keys.Poll() == Input::Toggle, "Fresh completion after ACK was lost");
}
void FocusMenuModeAndPageTransitionsDiscardCarriedHold() {
    Keys keys;
    keys.Poll();
    keys.focus = false;
    keys.held = {2, 86};
    Require(!keys.Poll(), "Focus loss opened");
    keys.focus = true;
    keys.Poll();
    Require(!keys.Poll(), "Focus recovery opened held chord");
    keys.mode = InputMode::MenuLocked;
    keys.Poll();
    keys.mode = InputMode::Menu;
    keys.Poll();
    Require(!keys.Poll(), "Unlock reopened held chord");
    ++keys.page;
    keys.Poll();
    Require(!keys.Poll(), "Page change inherited hold");
    keys.open = true;
    keys.Poll();
    keys.open = false;
    keys.Poll();
    Require(!keys.Poll(), "Menu closure inherited hold");
    keys.Release();
    keys.held = {2, 86};
    Require(keys.Poll() == Input::Toggle, "Fresh context retry failed");
}
void SingleMeleeBackCannotImmediatelyReopenClosedMenu() {
    Keys keys;
    keys.open = true;
    keys.policy.actions = {OpeningAction::Melee};
    keys.Poll();
    keys.held = {86};
    Require(keys.Poll() == Input::Back, "Ordinary Back was changed by opening policy");
    keys.open = false;
    keys.Poll();
    Require(!keys.Poll(), "Back immediately reopened menu");
    keys.Release();
    keys.held = {6};
    Require(keys.Poll() == Input::Toggle, "Fresh knife did not reopen");
}
void LegacyFreshAimDoesNotSubstituteForFreshKnife() {
    Keys legacy;
    legacy.typed = false;
    legacy.Poll();
    legacy.held = {86};
    legacy.Poll();
    legacy.held.insert(2);
    Require(!legacy.Poll(), "Legacy mode stopped requiring fresh knife");
    Keys explicitPolicy;
    explicitPolicy.Poll();
    explicitPolicy.held = {86};
    explicitPolicy.Poll();
    explicitPolicy.held.insert(2);
    Require(explicitPolicy.Poll() == Input::Toggle, "Explicit aggregate policy missed fresh aim completion");
}
void InvalidAndUnavailablePoliciesFailBeforeKeyReads() {
    Keys keys;
    unsigned reads = 0;
    for (auto invalid : {MainOpeningControls{0, {OpeningAction::Aim}}, MainOpeningControls{1, {}},
                         MainOpeningControls{1, {OpeningAction::Aim, OpeningAction::Aim}},
                         MainOpeningControls{1, {OpeningAction::Frag, OpeningAction::SecondaryOffhand}},
                         MainOpeningControls{1, {static_cast<OpeningAction>(999)}}}) {
        Reject([&] {
            keys.router.Poll(
                true, false, false,
                [&](int) {
                    ++reads;
                    return false;
                },
                0, 0, false, true, InputMode::Menu, 120, 0, &invalid);
        });
    }
    auto unavailable = keys.bindings;
    unavailable.jump.clear();
    NativeInputRouter router(unavailable);
    const MainOpeningControls jump{1, {OpeningAction::Jump}};
    Reject([&] {
        router.Poll(
            true, false, false,
            [&](int) {
                ++reads;
                return false;
            },
            0, 0, false, true, InputMode::Menu, 120, 0, &jump);
    });
    Require(reads == 0, "Invalid policy reached desktop key IO");
}
void SourceSpacerDelayIsCapturedAndDoesNotCountDisabledRows() {
    MenuSnapshot model;
    model.rows = {
        {"A", "", true}, {"Unavailable", "UNAVAILABLE", false}, {"", "", false, true}, {"B", "", true}};
    model.selectedIndex = 1;
    const auto counts = CountScrollSpacers(model);
    Require(counts.up == 0 && counts.down == 1, "Spacer count confused an unavailable row with a blank");
    model.selectedIndex = 0;
    Require(CountScrollSpacers(model).down == 0, "Disabled ordinary row was skipped");
    Keys keys;
    keys.open = true;
    keys.downSpacers = 1;
    keys.Poll();
    keys.held = {1};
    Require(keys.Poll() == Input::Down, "Scroll admission failed");
    keys.now = 254;
    keys.duration = 100;
    keys.downSpacers = 0;
    Require(!keys.Poll(), "Committed duration retroactively shortened captured spacer wait");
    keys.now = 255;
    Require(keys.Poll() == Input::Down, "Source half-wait deadline was missed");
    keys.now = 429;
    Require(!keys.Poll(), "Following no-spacer repeat accelerated");
    keys.now = 430;
    Require(keys.Poll() == Input::Down, "New no-spacer duration was not captured");
}
void SpacerCountsWrapBoundariesAndRejectInvalidDirectGeometry() {
    MenuSnapshot model;
    model.rows = {{"A", "", true}, {"", "", false, true}, {"", "", false, true}};
    const auto counts = CountScrollSpacers(model);
    Require(counts.up == 2 && counts.down == 2, "Wrapped spacers were not counted");
    model.rows.resize(128, {"", "", false, true});
    const auto maximum = CountScrollSpacers(model);
    Require(maximum.up == 127 && maximum.down == 127, "Maximum bounded skip count changed");
    model.selectedIndex = 1;
    Reject([&] { CountScrollSpacers(model); });
    Keys keys;
    unsigned reads = 0;
    Reject([&] {
        keys.router.Poll(
            true, true, false,
            [&](int) {
                ++reads;
                return false;
            },
            0, 0, false, true, InputMode::Menu, 120, 0, nullptr, 128, 0);
    });
    Require(reads == 0, "Invalid skip count reached key IO");
}
void SpacerWaitUsesCapturedDurationAtUnsignedClockBoundary() {
    for (int duration : {100, 120, 250}) {
        Keys keys;
        keys.open = true;
        keys.duration = duration;
        keys.downSpacers = 127;
        const auto interval = static_cast<std::uint64_t>(duration + 75 + duration / 2 * 127);
        keys.now = std::numeric_limits<std::uint64_t>::max() - interval;
        keys.Poll();
        keys.held = {1};
        Require(keys.Poll() == Input::Down, "Boundary scroll was not admitted");
        keys.now = std::numeric_limits<std::uint64_t>::max() - 1;
        Require(!keys.Poll(), "Maximum spacer wait expired early");
        ++keys.now;
        Require(keys.Poll() == Input::Down, "Maximum spacer wait missed its safe subtraction deadline");
    }
}
void SpacerWaitConsumesPendingFocusAndRollbackHoldsWithoutReplay() {
    Keys keys;
    keys.open = true;
    keys.downSpacers = 1;
    keys.Poll();
    keys.held = {1};
    Require(keys.Poll() == Input::Down, "Initial spacer movement was lost");
    keys.now = 200;
    keys.pending = true;
    keys.held.insert(70);
    Require(!keys.Poll(), "Pending select escaped the spacer wait");
    keys.pending = false;
    keys.now = 454;
    Require(!keys.Poll(), "Pending pause shortened the captured spacer interval");
    keys.now = 455;
    Require(keys.Poll() == Input::Down, "Pending ACK replayed Select instead of the held scroll");
    keys.focus = false;
    keys.now = 500;
    keys.Poll();
    keys.focus = true;
    keys.Poll();
    keys.now = 1000;
    Require(!keys.Poll(), "Focus recovery carried a spacer scroll hold");
    keys.Release();
    keys.held = {1};
    Require(keys.Poll() == Input::Down, "Fresh focus retry was lost");
    keys.now = 999;
    Require(!keys.Poll(), "Clock rollback dispatched a carried scroll");
    keys.now = 2000;
    keys.Poll();
    Require(!keys.Poll(), "Clock rollback replayed its old held scroll");
    keys.Release();
    keys.held = {1};
    Require(keys.Poll() == Input::Down, "Rollback release did not restore a fresh movement");
}

} // namespace
int main() {
    struct Case {
        const char *name;
        void (*run)();
    };
    const Case checks[] = {
        {"SpacerWaitUsesCapturedDurationAtUnsignedClockBoundary",
         SpacerWaitUsesCapturedDurationAtUnsignedClockBoundary},
        {"SpacerWaitConsumesPendingFocusAndRollbackHoldsWithoutReplay",
         SpacerWaitConsumesPendingFocusAndRollbackHoldsWithoutReplay},
        {"EveryActionUsesItsExactSavedGroup", EveryActionUsesItsExactSavedGroup},
        {"FullChordCompletesInEitherOrderAndNeedsEveryAction",
         FullChordCompletesInEitherOrderAndNeedsEveryAction},
        {"MultiplePhysicalKeysDoNotCreateAnotherAggregatePress",
         MultiplePhysicalKeysDoNotCreateAnotherAggregatePress},
        {"InstallationConsumesFullHoldButAllowsFreshPartialCompletion",
         InstallationConsumesFullHoldButAllowsFreshPartialCompletion},
        {"PolicyRevisionTupleAndLegacyChangesReseedWholeChord",
         PolicyRevisionTupleAndLegacyChangesReseedWholeChord},
        {"PendingCompletionIsConsumedBeforeAcknowledgement",
         PendingCompletionIsConsumedBeforeAcknowledgement},
        {"FocusMenuModeAndPageTransitionsDiscardCarriedHold",
         FocusMenuModeAndPageTransitionsDiscardCarriedHold},
        {"SingleMeleeBackCannotImmediatelyReopenClosedMenu",
         SingleMeleeBackCannotImmediatelyReopenClosedMenu},
        {"LegacyFreshAimDoesNotSubstituteForFreshKnife", LegacyFreshAimDoesNotSubstituteForFreshKnife},
        {"InvalidAndUnavailablePoliciesFailBeforeKeyReads", InvalidAndUnavailablePoliciesFailBeforeKeyReads},
        {"SourceSpacerDelayIsCapturedAndDoesNotCountDisabledRows",
         SourceSpacerDelayIsCapturedAndDoesNotCountDisabledRows},
        {"SpacerCountsWrapBoundariesAndRejectInvalidDirectGeometry",
         SpacerCountsWrapBoundariesAndRejectInvalidDirectGeometry}};
    unsigned passed = 0;
    for (const auto &check : checks) {
        try {
            check.run();
            ++passed;
            std::cout << "PASS " << check.name << '\n';
        } catch (const std::exception &error) {
            std::cerr << "FAIL " << check.name << ": " << error.what() << '\n';
            return 1;
        }
    }
    std::cout << passed << '/' << std::size(checks) << " main opening input checks passed\n";
}
