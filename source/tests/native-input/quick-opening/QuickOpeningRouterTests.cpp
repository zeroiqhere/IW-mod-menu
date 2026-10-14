#include "NativeInputRouter.h"
#include <array>
#include <iostream>
#include <set>
#include <stdexcept>
using namespace iw::overlay;
using namespace iw::overlay::runtime;
namespace {
void Require(bool value, const char* message) {
    if (!value)
        throw std::runtime_error(message);
}
template <class Call> void Reject(Call call) {
    try {
        call();
    } catch (const std::invalid_argument&) {
        return;
    }
    throw std::runtime_error("Expected direct-call rejection");
}
// Every callback below reads owned test keys, never actual desktop input
struct Fixture {
    Bindings bindings{{2},  {86, 6}, {2, 38}, {1, 40},  {70}, {51}, {52}, {1},
                      {38}, {40},    {71},    {69, 72}, {16}, {17}, {32}};
    NativeInputRouter router{bindings};
    ClosedMenuOpeningPolicies policies;
    LocalMenuEligibility eligibility{1, PlayerLifeState::Alive, true, false, false};
    bool typed = true, dual = true, mainPresent = true, focus = true, open = false, pending = false;
    MenuSurface surface = MenuSurface::Main;
    InputMode mode = InputMode::Menu;
    std::uint64_t navigation = 1, menu = 1, now = 0;
    std::set<int> held;
    std::array<unsigned, 256> reads{};
    std::optional<Input> Poll() {
        reads.fill(0);
        return router.Poll(
            focus, open, pending,
            [&](int key) {
                ++reads[key];
                return held.contains(key);
            },
            now++, navigation, false, true, mode, 120, menu, mainPresent ? &policies.main : nullptr, 0, 0,
            dual ? &policies : nullptr, typed ? &eligibility : nullptr, surface);
    }
    void Release() {
        held.clear();
        Poll();
    }
};
void VerifiedLivingMainAndQuickProduceDistinctScopedIntents() {
    Fixture f;
    f.Poll();
    f.held = {2, 86};
    Require(f.Poll() == Input::OpenMain, "Verified Main still used legacy Toggle");
    f.Release();
    f.held = {2, 69};
    Require(f.Poll() == Input::OpenQuick, "Configured Quick did not reach the actual router");
    Require(!f.Poll(), "A held Quick chord repeated");
    f.Release();
    f.policies.quick.actions = {OpeningAction::Sprint, OpeningAction::Use, OpeningAction::Jump};
    f.Poll();
    f.held = {16, 70};
    Require(!f.Poll(), "Configured three-action Quick accepted a partial chord");
    f.held.insert(32);
    Require(f.Poll() == Input::OpenQuick, "Exact configured Quick groups were ignored");
}
void HeldMainRetainsPriorityAndConsumesSimultaneousQuick() {
    Fixture f;
    f.Poll();
    f.held = {2, 86, 69};
    Require(f.Poll() == Input::OpenMain, "Simultaneous chords lost Main priority");
    f.held.erase(86);
    Require(!f.Poll(), "Consumed Quick replayed when Main released");
    f.Release();
    f.held = {2, 86};
    f.Poll();
    f.held.insert(69);
    Require(!f.Poll(), "Fresh Quick bypassed an already held Main");
}
void DeadFallbackUsesAimJumpWithMainOnlyExactBindings() {
    Bindings b{{2}, {86}, {2, 38}, {1, 40}, {70}};
    b.jump = {32};
    NativeInputRouter router(b);
    MainOpeningControls main{1, {OpeningAction::Aim, OpeningAction::Melee}};
    LocalMenuEligibility life{1, PlayerLifeState::Dead, true, false, false};
    std::set<int> held;
    auto poll = [&] {
        return router.Poll(
            true, false, false, [&](int key) { return held.contains(key); }, 0, 1, false, true,
            InputMode::Menu, 120, 1, &main, 0, 0, nullptr, &life);
    };
    poll();
    held = {2, 86};
    Require(!poll(), "Dead Main-only route used living chord");
    held = {2, 32};
    Require(poll() == Input::OpenQuick, "Missing configured Quick blocked proven dead fallback");
    Require(!poll(), "Dead fallback repeated while held");
    life.life = PlayerLifeState::Alive;
    held.clear();
    poll();
    held = {2, 32};
    Require(!poll(), "Main-only policy invented a living configured Quick chord");
    held.clear();
    poll();
    held = {2, 86};
    Require(poll() == Input::OpenMain, "Main-only verified living Main was lost");
}
void MissingJumpNeverReceivesAnInventedFallbackKey() {
    Bindings b{{2}, {86}, {2, 38}, {1, 40}, {70}};
    NativeInputRouter router(b);
    MainOpeningControls main{1, {OpeningAction::Aim, OpeningAction::Melee}};
    LocalMenuEligibility life{1, PlayerLifeState::Dead, true, false, false};
    std::set<int> held;
    auto poll = [&] {
        return router.Poll(
            true, false, false, [&](int key) { return held.contains(key); }, 0, 1, false, true,
            InputMode::Menu, 120, 1, &main, 0, 0, nullptr, &life);
    };
    poll();
    held = {2, 86, 32};
    Require(!poll(), "Dead opening guessed Jump or fell through legacy Main");
}
void UnknownFactsDenyOpeningWhileAbsentFactsKeepLegacyMain() {
    Fixture f;
    f.eligibility.life = PlayerLifeState::Unknown;
    f.Poll();
    f.held = {2, 86, 69, 32};
    Require(!f.Poll(), "Unknown life authorized a menu");
    f.typed = false;
    f.Poll();
    Require(!f.Poll(), "Legacy transition replayed holds");
    f.Release();
    f.held = {2, 86};
    Require(f.Poll() == Input::Toggle, "Absent eligibility removed bootstrap Main");
    Fixture missing;
    missing.dual = false;
    missing.mainPresent = false;
    missing.Poll();
    missing.held = {2, 86, 32};
    Require(!missing.Poll(), "Eligibility without a captured policy invented an opening epoch");
}
void NullPermissionAndBlockedFactsConsumeHeldCompletions() {
    for (unsigned fault = 0; fault < 5; ++fault) {
        Fixture f;
        if (fault == 0)
            f.eligibility.hasMenu.reset();
        if (fault == 1)
            f.eligibility.hasMenu = false;
        if (fault == 2)
            f.eligibility.controlsDisabled.reset();
        if (fault == 3)
            f.eligibility.controlsDisabled = true;
        if (fault == 4)
            f.eligibility.blocked = true;
        f.Poll();
        f.held = {2, 86, 69};
        Require(!f.Poll(), "Unproven permission or blocked owner dispatched");
        f.eligibility = {2, PlayerLifeState::Alive, true, false, false};
        Require(!f.Poll() && !f.Poll(), "Owner recovery replayed held opening");
        f.Release();
        f.held = {2, 69};
        Require(f.Poll() == Input::OpenQuick, "Fresh authorized completion was lost");
    }
}
void RouterReadsEveryExternalKeyOnlyOncePerImmutableSample() {
    Fixture f;
    f.Poll();
    f.held = {2, 69};
    Require(f.Poll() == Input::OpenQuick, "Sampling proof did not exercise Quick dispatch");
    for (unsigned count : f.reads)
        Require(count <= 1, "Arbitrator reread external key IO");
    Require(f.reads[2] == 1 && f.reads[86] == 1 && f.reads[69] == 1 && f.reads[32] == 1,
            "Priority path skipped another saved group");
    // A failed external sample cannot complete later from the partly captured key array
    bool failed = false;
    try {
        f.router.Poll(
            true, false, false, [](int) -> bool { throw std::runtime_error("Owned key sample failed"); },
            f.now, f.navigation, false, true, InputMode::Menu, 120, f.menu, &f.policies.main, 0, 0,
            &f.policies, &f.eligibility);
    } catch (const std::runtime_error&) {
        failed = true;
    }
    Require(failed, "Failed external sample was silently accepted");
    Require(!f.Poll() && !f.Poll(), "Key sample recovery replayed a carried Quick chord");
    f.Release();
    f.held = {2, 69};
    Require(f.Poll() == Input::OpenQuick, "Fresh completion after key sample failure was lost");
}
void InitialFullHoldIsSeededButFreshPartialCompletionOpens() {
    Fixture full;
    full.held = {2, 69};
    Require(!full.Poll() && !full.Poll(), "Installing a full hold opened");
    Fixture partial;
    partial.held = {2};
    partial.Poll();
    partial.held.insert(69);
    Require(partial.Poll() == Input::OpenQuick, "Fresh partial completion did not open");
    partial.held.insert(72);
    Require(!partial.Poll(), "Second physical key in the same Quick group created another edge");
}
void OwnerNavigationPolicyAndSurfaceChangesReseedHeldGestures() {
    for (unsigned change = 0; change < 4; ++change) {
        Fixture f;
        f.Poll();
        f.held = {2, 69};
        if (change == 0)
            ++f.eligibility.revision;
        if (change == 1)
            ++f.navigation;
        if (change == 2)
            ++f.policies.revision;
        if (change == 3)
            f.surface = MenuSurface::Quick;
        Require(!f.Poll() && !f.Poll(), "Changed owner context carried a held completion");
        f.Release();
        f.held = {2, 69};
        Require(f.Poll() == Input::OpenQuick, "Fresh changed-context completion was lost");
    }
}
void PendingAckAndFocusRecoveryNeverReplayCompletedChords() {
    Fixture f;
    f.Poll();
    f.pending = true;
    f.held = {2, 69};
    Require(!f.Poll(), "Pending command opened another menu");
    f.pending = false;
    Require(!f.Poll() && !f.Poll(), "ACK was treated as permission to replay");
    f.Release();
    f.held = {2, 69};
    f.focus = false;
    Require(!f.Poll(), "Background Quick opened");
    f.focus = true;
    Require(!f.Poll() && !f.Poll(), "Focus recovery replayed held Quick");
    f.Release();
    f.held = {2, 69};
    Require(f.Poll() == Input::OpenQuick, "Fresh focus retry was lost");
    f.Release();
    f.policies.quickMenuDisabled = true;
    f.Poll();
    f.held = {2, 69};
    Require(!f.Poll(), "Disable Quick Menu did not block a living opening");
    f.Release();
    f.eligibility.life = PlayerLifeState::Dead;
    f.Poll();
    f.held = {2, 32};
    Require(!f.Poll(), "Disable Quick Menu did not block the dead fallback");
}
void DeadJumpBackRequiresOpenQuickAndFreshSavedJump() {
    Fixture f;
    f.open = true;
    f.surface = MenuSurface::Quick;
    f.eligibility.life = PlayerLifeState::Dead;
    f.Poll();
    f.held = {32};
    Require(f.Poll() == Input::JumpBack, "Dead Quick Jump did not use scoped Back");
    Require(!f.Poll(), "Held Jump repeated Back");
    f.Release();
    f.surface = MenuSurface::Main;
    f.Poll();
    f.held = {32};
    Require(!f.Poll(), "Main route sent a Quick Jump command");
    f.Release();
    f.surface = MenuSurface::Quick;
    f.eligibility.life = PlayerLifeState::Alive;
    f.Poll();
    f.held = {32};
    Require(!f.Poll(), "Living Jump became a menu command");
}
void BlockedOpenMenuConsumesOrdinaryActionsUntilFreshRelease() {
    Fixture f;
    f.open = true;
    f.eligibility.blocked = true;
    f.Poll();
    f.held = {70, 86};
    Require(!f.Poll(), "Blocked owner still dispatched Select or Back");
    f.eligibility.blocked = false;
    Require(!f.Poll() && !f.Poll(), "Blocked recovery replayed an ordinary edge");
    f.Release();
    f.held = {70};
    Require(f.Poll() == Input::Select, "Fresh ordinary Select was lost");
    f.Release();
    f.held = {1};
    Require(f.Poll() == Input::Down, "Verified ordinary scroll was lost");
    ++f.navigation;
    f.now += 195;
    Require(f.Poll() == Input::Down,
            "Captured navigation epoch incorrectly canceled an ordinary held repeat");
}
void LockedAndModalMenusDoNotBorrowDeadJumpOrClosedChords() {
    for (auto mode : {InputMode::MenuLocked, InputMode::WidthEditor, InputMode::InstructionPositionEditor,
                      InputMode::MenuPositionEditor, InputMode::MenuCredits}) {
        Fixture f;
        f.open = true;
        f.surface = MenuSurface::Quick;
        f.eligibility.life = PlayerLifeState::Dead;
        f.mode = mode;
        f.Poll();
        f.held = {32};
        Require(!f.Poll(), "Modal route borrowed ordinary dead Jump");
        f.mode = InputMode::Menu;
        f.Poll();
        Require(!f.Poll(), "Returning from a modal replayed Jump");
        if (mode != InputMode::MenuLocked) {
            f.Release();
            f.mode = mode;
            f.eligibility.controlsDisabled = true;
            f.Poll();
            f.held = {86};
            const Input expected = mode == InputMode::WidthEditor ? Input::FinishWidthEditor
                                   : mode == InputMode::InstructionPositionEditor
                                       ? Input::FinishInstructionPositionEditor
                                   : mode == InputMode::MenuPositionEditor ? Input::FinishMenuPositionEditor
                                                                           : Input::FinishMenuCredits;
            Require(f.Poll() == expected, "Ordinary monitor lock disabled its dedicated editor exit");
        }
    }
}
void InvalidEligibilityAndSurfaceRejectBeforeReadingExternalKeys() {
    Fixture f;
    unsigned reads = 0;
    for (auto fact : {LocalMenuEligibility{0, PlayerLifeState::Alive, true, false, false},
                      LocalMenuEligibility{1, static_cast<PlayerLifeState>(99), true, false, false}}) {
        Reject([&] {
            f.router.Poll(
                true, false, false,
                [&](int) {
                    ++reads;
                    return false;
                },
                0, 1, false, true, InputMode::Menu, 120, 1, &f.policies.main, 0, 0, &f.policies, &fact);
        });
    }
    Reject([&] {
        f.router.Poll(
            true, false, false,
            [&](int) {
                ++reads;
                return false;
            },
            0, 1, false, true, InputMode::Menu, 120, 1, &f.policies.main, 0, 0, &f.policies, &f.eligibility,
            static_cast<MenuSurface>(99));
    });
    Require(reads == 0, "Malformed owner facts reached desktop key IO");
}
} // namespace
int main() {
    struct Case {
        const char* name;
        void (*run)();
    };
    const Case cases[] = {
#define CASE(name) {#name, name}
        CASE(VerifiedLivingMainAndQuickProduceDistinctScopedIntents),
        CASE(HeldMainRetainsPriorityAndConsumesSimultaneousQuick),
        CASE(DeadFallbackUsesAimJumpWithMainOnlyExactBindings),
        CASE(MissingJumpNeverReceivesAnInventedFallbackKey),
        CASE(UnknownFactsDenyOpeningWhileAbsentFactsKeepLegacyMain),
        CASE(NullPermissionAndBlockedFactsConsumeHeldCompletions),
        CASE(RouterReadsEveryExternalKeyOnlyOncePerImmutableSample),
        CASE(InitialFullHoldIsSeededButFreshPartialCompletionOpens),
        CASE(OwnerNavigationPolicyAndSurfaceChangesReseedHeldGestures),
        CASE(PendingAckAndFocusRecoveryNeverReplayCompletedChords),
        CASE(DeadJumpBackRequiresOpenQuickAndFreshSavedJump),
        CASE(BlockedOpenMenuConsumesOrdinaryActionsUntilFreshRelease),
        CASE(LockedAndModalMenusDoNotBorrowDeadJumpOrClosedChords),
        CASE(InvalidEligibilityAndSurfaceRejectBeforeReadingExternalKeys)
#undef CASE
    };
    unsigned passed = 0;
    for (const auto& check : cases) {
        try {
            check.run();
            ++passed;
            std::cout << "PASS " << check.name << '\n';
        } catch (const std::exception& error) {
            std::cerr << "FAIL " << check.name << ": " << error.what() << '\n';
        }
    }
    std::cout << passed << '/' << std::size(cases) << " Quick opening router checks passed\n";
    return passed == std::size(cases) ? 0 : 1;
}
