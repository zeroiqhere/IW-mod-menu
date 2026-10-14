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
    bool focused = true, open = true, pending = false;
    std::uint64_t now = 0, navigation = 7, menu = 20, ack = 10;
    InputMode mode = InputMode::Menu;
    LocalMenuEligibility eligibility{4, PlayerLifeState::Alive, true, false, false};
    MenuSurface surface = MenuSurface::Main;
    std::optional<MenuUseContinuation> continuation;
    std::optional<MenuOptionCallback> callback;
    int reads = 0;
    std::optional<Input> Poll() {
        UseMonitorObservation observation{continuation ? &*continuation : nullptr,
            callback ? &*callback : nullptr, ack};
        return router.Poll(focused, open, pending, [&](int key) { ++reads; return held.contains(key); },
            now, navigation, false, false, mode, 120, menu, nullptr, 0, 0, nullptr, &eligibility,
            surface, nullptr, &observation);
    }
    void Dispatch(bool capture = true) {
        Poll(); held = {70}; ++now;
        Require(Poll() == Input::Select, "The original fresh Use was not emitted");
        if (capture) router.CaptureUseDispatch(11);
        pending = true; ++now; Require(!Poll(), "A pending successful write emitted another command");
    }
    void Associate(MenuOptionCallbackKind kind = MenuOptionCallbackKind::Plain) {
        ack = 11; pending = false;
        continuation = MenuUseContinuation{1, 11, 4, kind};
        callback = MenuOptionCallback{1, kind, kind == MenuOptionCallbackKind::Plain
            ? MenuOptionCallbackPhase::Cooldown : MenuOptionCallbackPhase::Pending};
        eligibility.controlsDisabled = true; mode = InputMode::MenuLocked; now = 3;
        Require(!Poll(), "An associated callback escaped its source wait");
    }
    void Complete(std::uint64_t time = 251) {
        callback.reset(); eligibility.controlsDisabled = false; mode = InputMode::Menu; now = time;
    }
};
void PlainRetirementResumesHeldUseOnceWithoutANativeDeadline() {
    Rig rig; rig.Dispatch(); rig.Associate(); rig.now = 250; Require(!rig.Poll(), "Plain callback completed from a native elapsed estimate");
    rig.Complete(); Require(rig.Poll() == Input::Select, "Held Use did not resume on actual plain retirement");
    Require(!rig.Poll(), "An uncaptured repeat emitted a same-frame burst");
}
void BooleanRedrawKeepsUseThroughBothOwnLockTransitions() {
    Rig rig; rig.Dispatch(); rig.Associate(MenuOptionCallbackKind::Boolean);
    rig.callback->phase = MenuOptionCallbackPhase::Cooldown; ++rig.menu; rig.now = 181;
    Require(!rig.Poll(), "The boolean redraw became readiness");
    rig.now = 430; Require(!rig.Poll(), "Native430 retired a still-published callback");
    rig.Complete(431); Require(rig.Poll() == Input::Select, "Own bool redraw reseeded held Use");
}
void ImmediateAndScheduledRedrawKeepTheSameOriginalReceipt() {
    Rig rig; rig.Dispatch(); ++rig.menu; rig.Associate(MenuOptionCallbackKind::Boolean);
    ++rig.menu; rig.callback->phase = MenuOptionCallbackPhase::Cooldown; rig.now = 181;
    Require(!rig.Poll(), "Scheduled Instructions redraw admitted another Use");
    rig.Complete(431); Require(rig.Poll() == Input::Select, "Immediate plus scheduled redraw lost the original Use");
}
void LateFirstAckCanUseAnAlreadyRetiredCausalReceipt() {
    Rig rig; rig.Dispatch(); rig.pending = false; rig.ack = 11;
    rig.continuation = MenuUseContinuation{1, 11, 4, MenuOptionCallbackKind::Plain}; rig.now = 10000;
    Require(rig.Poll() == Input::Select, "A late actual ready receipt could not resume held Use");
}
void ReleaseAndRepressWithinTheSameWaitUseTheCurrentButtonState() {
    Rig rig; rig.Dispatch(); rig.Associate(); rig.held.clear(); rig.now = 60; Require(!rig.Poll(), "Release ended the callback pause");
    rig.held = {70}; rig.now = 120; Require(!rig.Poll(), "Repress bypassed the source wait");
    rig.Complete(); Require(rig.Poll() == Input::Select, "Source iteration ignored the current held Use after its wait");
}
void LongBusyAfterRetirementCannotQueueOrBurstUse() {
    Rig rig; rig.Dispatch(); rig.Associate(); rig.Complete(); rig.eligibility.blocked = true;
    Require(!rig.Poll(), "Busy work became a repeat permission");
    rig.now = 10000; Require(!rig.Poll(), "A long Busy wait accumulated a catch-up repeat");
    rig.eligibility.blocked = false; Require(rig.Poll() == Input::Select, "Original ready scope did not resume after Busy cleared");
    rig.router.CaptureUseDispatch(12); rig.pending = true; Require(!rig.Poll(), "The next pending write burst Use");
}
void AckBeforeQueuedStartWaitsForTheExactInvocationReceipt() {
    Rig rig; rig.Dispatch(); rig.pending = false; rig.ack = 11; rig.eligibility.blocked = true;
    Require(!rig.Poll(), "Busy ACK before callback start admitted a repeat");
    rig.Associate(); rig.Complete(); rig.eligibility.blocked = false;
    Require(rig.Poll() == Input::Select, "Queued same-sequence admission lost the captured write");
}
void FailedWriteAndIgnoredAckCannotBorrowLaterReceipts() {
    for (bool capture : {false, true}) {
        Rig rig; rig.Dispatch(capture); rig.pending = false; rig.ack = 11; rig.now = 20;
        Require(!rig.Poll(), "An ACK without an associated invocation replayed Use");
        rig.continuation = MenuUseContinuation{1, 11, 4, MenuOptionCallbackKind::Plain}; rig.now = 10000;
        Require(!rig.Poll(), "A failed write or ignored ACK borrowed a later receipt");
    }
}
void ForeignAckAndReplacementReceiptRetireTheOriginalHold() {
    for (int change : {0, 1}) {
        Rig rig; rig.Dispatch(); rig.Associate(); rig.Complete();
        if (change == 0) rig.ack = 12;
        else rig.continuation = MenuUseContinuation{2, 11, 4, MenuOptionCallbackKind::Plain};
        Require(!rig.Poll(), "A foreign acknowledgment or replacement invocation replayed the old Use");
        Require(!rig.Poll(), "A consumed foreign scope later armed a repeat");
    }
}
void FocusAndActorNavigationReplacementCannotTransferUse() {
    for (int change : {0, 1, 2, 3, 4}) {
        Rig rig; rig.Dispatch(); rig.Associate();
        if (change == 0) { rig.focused = false; Require(!rig.Poll(), "Focus loss emitted Use"); rig.focused = true; }
        else if (change == 1) ++rig.navigation;
        else if (change == 2) { ++rig.eligibility.revision; rig.continuation->admissionRevision = rig.eligibility.revision; }
        else if (change == 3) rig.surface = MenuSurface::Quick;
        else { rig.continuation.reset(); rig.callback.reset(); rig.mode = InputMode::Menu; rig.eligibility.controlsDisabled = false; }
        rig.Complete(); Require(!rig.Poll(), "A replaced source visit inherited held Use");
    }
}
void ReadyScrollingKeepsSourcePriorityOverHeldUse() {
    Rig rig; rig.Dispatch(); rig.Associate(); rig.held = {70, 38}; rig.now = 100; Require(!rig.Poll(), "Held scroll escaped a callback wait");
    rig.Complete(); Require(rig.Poll() == Input::Up, "Source resumed Use before an eligible held scroll");
}
void BackwardNativeClockConsumesTheOldInvocation() {
    Rig rig; rig.Dispatch(); rig.Associate(); rig.now = 200; Require(!rig.Poll(), "Callback ended before rollback");
    rig.now = 100; Require(!rig.Poll(), "Rollback emitted a retained Use");
    rig.Complete(1000); Require(!rig.Poll(), "Rollback retained an old callback hold");
}
void InvalidReceiptIsRejectedBeforeAnyKeyRead() {
    for (int change : {0, 1, 2, 3, 4}) {
        Rig rig; rig.continuation = MenuUseContinuation{1, 10, 4, MenuOptionCallbackKind::Plain};
        if (change == 0) rig.continuation->revision = 0;
        else if (change == 1) rig.continuation->sequence = 0;
        else if (change == 2) rig.continuation->admissionRevision = 5;
        else if (change == 3) rig.continuation->sequence = 11;
        else rig.continuation->kind = static_cast<MenuOptionCallbackKind>(99);
        bool rejected = false; try { rig.Poll(); } catch (const std::invalid_argument&) { rejected = true; }
        Require(rejected && rig.reads == 0, "An invalid Use receipt reached the key reader");
    }
}
void UnassociatedLegacyUseKeepsItsFreshPressContract() {
    Rig rig; rig.Poll(); rig.held = {70}; Require(rig.Poll() == Input::Select, "A legacy fresh Use stopped working");
    rig.now = std::numeric_limits<std::uint64_t>::max(); Require(!rig.Poll(), "Legacy omission invented a repeated feature action");
}
}
int main() {
    struct Test { const char* name; void (*run)(); };
    const std::array tests{
        Test{"PlainRetirementResumesHeldUseOnceWithoutANativeDeadline", PlainRetirementResumesHeldUseOnceWithoutANativeDeadline},
        Test{"BooleanRedrawKeepsUseThroughBothOwnLockTransitions", BooleanRedrawKeepsUseThroughBothOwnLockTransitions},
        Test{"ImmediateAndScheduledRedrawKeepTheSameOriginalReceipt", ImmediateAndScheduledRedrawKeepTheSameOriginalReceipt},
        Test{"LateFirstAckCanUseAnAlreadyRetiredCausalReceipt", LateFirstAckCanUseAnAlreadyRetiredCausalReceipt},
        Test{"ReleaseAndRepressWithinTheSameWaitUseTheCurrentButtonState", ReleaseAndRepressWithinTheSameWaitUseTheCurrentButtonState},
        Test{"LongBusyAfterRetirementCannotQueueOrBurstUse", LongBusyAfterRetirementCannotQueueOrBurstUse},
        Test{"AckBeforeQueuedStartWaitsForTheExactInvocationReceipt", AckBeforeQueuedStartWaitsForTheExactInvocationReceipt},
        Test{"FailedWriteAndIgnoredAckCannotBorrowLaterReceipts", FailedWriteAndIgnoredAckCannotBorrowLaterReceipts},
        Test{"ForeignAckAndReplacementReceiptRetireTheOriginalHold", ForeignAckAndReplacementReceiptRetireTheOriginalHold},
        Test{"FocusAndActorNavigationReplacementCannotTransferUse", FocusAndActorNavigationReplacementCannotTransferUse},
        Test{"ReadyScrollingKeepsSourcePriorityOverHeldUse", ReadyScrollingKeepsSourcePriorityOverHeldUse},
        Test{"BackwardNativeClockConsumesTheOldInvocation", BackwardNativeClockConsumesTheOldInvocation},
        Test{"InvalidReceiptIsRejectedBeforeAnyKeyRead", InvalidReceiptIsRejectedBeforeAnyKeyRead},
        Test{"UnassociatedLegacyUseKeepsItsFreshPressContract", UnassociatedLegacyUseKeepsItsFreshPressContract}};
    int failures = 0;
    for (const auto& test : tests) {
        try { test.run(); std::cout << "PASS " << test.name << '\n'; }
        catch (const std::exception& error) { ++failures; std::cout << "FAIL " << test.name << ": " << error.what() << '\n'; }
    }
    std::cout << tests.size() - failures << '/' << tests.size() << " Use continuation input checks passed\n";
    return failures ? 1 : 0;
}
