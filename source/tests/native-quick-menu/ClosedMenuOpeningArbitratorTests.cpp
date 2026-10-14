#include "TestSupport.h"
#include <limits>
namespace iw::overlay::tests::quick {
void LivingMainHasPriorityAndConsumesSimultaneousQuickCompletion() {
    Keys keys;
    keys.Poll();
    keys.held = {2, 86, 69};
    Check(keys.Poll() == ClosedMenuOpeningIntent::Main, "Simultaneous source chords did not prefer Main");
    keys.held.erase(86);
    Check(!keys.Poll(), "Losing Main replayed the already consumed Quick completion");
    keys.Release();
    keys.held = {2, 69};
    Check(keys.Poll() == ClosedMenuOpeningIntent::Quick, "Quick failed after a fresh full completion");
    keys.Release();
    keys.held = {2, 86};
    Check(keys.Poll() == ClosedMenuOpeningIntent::Main, "Main completion failed");
    keys.held.insert(69);
    Check(!keys.Poll(), "Fresh Quick bypassed a held source-priority Main chord");
}
void LivingQuickUsesConfiguredExactGroupsAndFreshFullCompletion() {
    Keys keys;
    keys.policies.quick = {1, {OpeningAction::Sprint, OpeningAction::Use, OpeningAction::Jump}};
    keys.Poll();
    keys.held = {2, 69};
    Check(!keys.Poll(), "Default physical chord bypassed the configured Quick policy");
    keys.held = {16, 70};
    Check(!keys.Poll(), "Partial Quick chord opened");
    keys.held.insert(32);
    Check(keys.Poll() == ClosedMenuOpeningIntent::Quick && !keys.Poll(), "Quick did not emit once per completion");
    keys.held.erase(16);
    keys.Poll();
    keys.held.insert(16);
    Check(keys.Poll() == ClosedMenuOpeningIntent::Quick, "A fresh action did not complete an existing partial hold");
}
void DeadQuickUsesFixedAimJumpIndependentOfConfiguredChords() {
    Keys keys;
    keys.context.eligibility.life = PlayerLifeState::Dead;
    keys.policies.main = {1, {OpeningAction::Use}};
    keys.policies.quick = {1, {OpeningAction::Sprint}};
    keys.Poll();
    keys.held = {70, 16};
    Check(!keys.Poll(), "Dead player used a configured living chord");
    keys.held = {2, 32};
    Check(keys.Poll() == ClosedMenuOpeningIntent::Quick && !keys.Poll(), "Dead fixed Aim+Jump fallback failed");
}
void UnknownPlayerLifeNeverAuthorizesAnOpening() {
    Keys keys;
    keys.context.eligibility.life = PlayerLifeState::Unknown;
    keys.Poll();
    keys.held = {2, 86, 69, 32};
    Check(!keys.Poll(), "Unknown player life was treated as alive or dead");
    keys.context.eligibility.life = PlayerLifeState::Alive;
    Check(!keys.Poll() && !keys.Poll(), "Eligibility recovery replayed a held chord");
    keys.Release();
    keys.held = {2, 86};
    Check(keys.Poll() == ClosedMenuOpeningIntent::Main, "Fresh completion failed after known eligibility");
}
void AuthorizationAndDisabledControlsBlockBothPolicies() {
    for (bool disabled : {false, true}) {
        Keys keys;
        keys.Poll();
        if (disabled) keys.context.eligibility.controlsDisabled = true;
        else keys.context.eligibility.hasMenu = false;
        keys.held = {2, 86, 69};
        Check(!keys.Poll(), "Unauthorized or disabled controls opened a menu");
        keys.context.eligibility = {PlayerLifeState::Alive, true, false};
        Check(!keys.Poll(), "Authorization recovery replayed an old completion");
        keys.Release();
        keys.held = {2, 69};
        Check(keys.Poll() == ClosedMenuOpeningIntent::Quick, "Fresh authorized Quick failed");
    }
}
void QuickDisableBlocksLivingAndDeadWithoutDeferringHeldInput() {
    for (auto life : {PlayerLifeState::Alive, PlayerLifeState::Dead}) {
        Keys keys;
        keys.context.eligibility.life = life;
        keys.policies.quickMenuDisabled = true;
        keys.Poll();
        keys.held = life == PlayerLifeState::Alive ? std::set<int>{2, 69} : std::set<int>{2, 32};
        Check(!keys.Poll(), "Disable Quick Menu failed");
        keys.policies.quickMenuDisabled = false;
        Check(!keys.Poll() && !keys.Poll(), "Quick re-enable replayed a disabled completion");
        const auto chord = keys.held;
        keys.Release(); keys.held = chord;
        Check(keys.Poll() == ClosedMenuOpeningIntent::Quick, "Quick did not rearm after release");
    }
    Keys main;
    main.policies.quickMenuDisabled = true;
    main.Poll(); main.held = {2, 86};
    Check(main.Poll() == ClosedMenuOpeningIntent::Main, "Quick disable suppressed Main");
}
void BothAggregateGroupsAreObservedBeforePriorityAndPendingReturns() {
    Keys keys;
    keys.Poll(); keys.held = {2, 86, 69};
    Check(keys.Poll() == ClosedMenuOpeningIntent::Main, "Main priority failed");
    Check(keys.observed.contains(2) && keys.observed.contains(86) && keys.observed.contains(69) &&
              keys.observed.contains(32), "Arbitration short-circuited another chord's saved keys");
    keys.context.commandPending = true;
    Check(!keys.Poll() && keys.observed.contains(69) && keys.observed.contains(32),
          "Pending return skipped observing aggregate completion");
}
void InstallationConsumesFullHoldsAndAllowsFreshPartialCompletion() {
    for (bool quick : {false, true}) {
        Keys full;
        full.held = quick ? std::set<int>{2, 69} : std::set<int>{2, 86};
        Check(!full.Poll() && !full.Poll(), "Installation inherited a full chord");
        Keys partial;
        partial.held = {2};
        Check(!partial.Poll(), "Installation dispatched a partial chord");
        partial.held.insert(quick ? 69 : 86);
        Check(partial.Poll() == (quick ? ClosedMenuOpeningIntent::Quick : ClosedMenuOpeningIntent::Main),
              "Fresh completion after a partial seed failed");
    }
}
void PendingAndBlockedCompletionsCannotOpenAfterRecovery() {
    for (auto life : {PlayerLifeState::Alive, PlayerLifeState::Dead}) {
        for (bool pending : {false, true}) {
            Keys keys;
            keys.context.eligibility.life = life;
            keys.Poll();
            keys.context.commandPending = pending;
            keys.context.blocked = !pending;
            keys.held = {2}; keys.Poll();
            const int completionKey = life == PlayerLifeState::Alive ? 69 : 32;
            keys.held.insert(completionKey);
            Check(!keys.Poll(), "Blocked completion created an intent");
            keys.context.commandPending = keys.context.blocked = false;
            Check(!keys.Poll() && !keys.Poll(), "Recovered input replayed a consumed completion");
            keys.held.erase(completionKey); keys.Poll(); keys.held.insert(completionKey);
            Check(keys.Poll() == ClosedMenuOpeningIntent::Quick, "Fresh aggregate completion failed after ACK");
        }
    }
}
void FocusRecoveryAndMenuClosureRequireFreshCompletion() {
    for (bool focusLoss : {false, true}) {
        Keys keys;
        keys.policies.main.actions = {OpeningAction::Melee};
        keys.Poll();
        keys.context.foregroundAllowed = !focusLoss;
        keys.context.menuOpen = !focusLoss;
        keys.held = {86};
        Check(!keys.Poll(), "Unfocused or open menu dispatched a closed-menu intent");
        keys.context.foregroundAllowed = true;
        keys.context.menuOpen = false;
        Check(!keys.Poll() && !keys.Poll(), "Back hold reopened Main after closure or focus recovery");
        keys.Release(); keys.held = {86};
        Check(keys.Poll() == ClosedMenuOpeningIntent::Main, "Fresh key failed after recovery");
    }
}
void PolicyRevisionTupleAndEligibilityChangesReseedHeldChords() {
    Keys maximum;
    maximum.policies.revision = std::numeric_limits<std::uint64_t>::max();
    maximum.policies.main.revision = maximum.policies.quick.revision = maximum.policies.revision;
    maximum.Poll(); maximum.held = {2, 86};
    Check(maximum.Poll() == ClosedMenuOpeningIntent::Main, "Positive maximum owner epochs were rejected");
    maximum.policies.revision = 1;
    Check(!maximum.Poll() && !maximum.Poll(), "Owner replacement reused a held completion across epoch bounds");
    for (int change = 0; change < 5; ++change) {
        Keys keys;
        keys.Poll(); keys.held = {2, 86, 69, 32};
        if (change == 0) ++keys.policies.main.revision;
        if (change == 1) keys.policies.quick.actions = {OpeningAction::Jump};
        if (change == 2) keys.context.eligibility.life = PlayerLifeState::Dead;
        if (change == 3) ++keys.policies.quick.revision;
        if (change == 4) ++keys.policies.revision;
        Check(!keys.Poll() && !keys.Poll(), "Policy or player eligibility change inherited a full hold");
        keys.Release();
        keys.held = change == 2 ? std::set<int>{2, 32} : std::set<int>{2, 86};
        Check(keys.Poll() == (change == 2 ? ClosedMenuOpeningIntent::Quick : ClosedMenuOpeningIntent::Main),
              "Changed policy did not admit a later fresh chord");
    }
}
void IndependentPoliciesMayShareAllActionsWithoutCrossTupleRejection() {
    Keys keys;
    keys.policies.main = {1, {OpeningAction::Melee}};
    keys.policies.quick = keys.policies.main;
    keys.Poll(); keys.held = {86};
    Check(keys.Poll() == ClosedMenuOpeningIntent::Main && !keys.Poll(),
          "Shared source tuples were rejected or produced two intents");
    keys.Release(); keys.held = {6};
    Check(keys.Poll() == ClosedMenuOpeningIntent::Main, "Shared tuple did not rearm once per aggregate press");
}
void SavedBindingOwnershipCannotBeRetargetedByCallerMutation() {
    auto bindings = SavedBindings();
    ClosedMenuOpeningArbitrator arbiter{bindings};
    bindings.aim = {90};
    const ClosedMenuOpeningContext context{{PlayerLifeState::Alive, true, false}, true};
    const ClosedMenuOpeningPolicies policies;
    arbiter.Poll(context, policies, [](int) { return false; });
    bool observedReplacement = false;
    Check(arbiter.Poll(context, policies, [&](int key) {
        observedReplacement = observedReplacement || key == 90;
        return key == 2 || key == 86;
    }) == ClosedMenuOpeningIntent::Main && !observedReplacement,
          "Caller mutation retargeted immutable saved action groups");
}
void MultiplePhysicalKeysCannotDuplicateAnAggregateAction() {
    Keys keys;
    keys.policies.quick.actions = {OpeningAction::Melee};
    keys.Poll(); keys.held = {6};
    Check(keys.Poll() == ClosedMenuOpeningIntent::Quick, "Alternate saved key failed");
    keys.held.insert(86);
    Check(!keys.Poll(), "Second physical key duplicated aggregate completion");
    keys.held.erase(6);
    Check(!keys.Poll(), "Changing held physical key reopened Quick");
    keys.Release(); keys.held = {86};
    Check(keys.Poll() == ClosedMenuOpeningIntent::Quick, "Full aggregate release did not rearm");
}
void EveryCanonicalActionUsesItsExactSavedGroup() {
    for (unsigned id = 0; id < OpeningActionCount; ++id) {
        Keys keys;
        const auto action = static_cast<OpeningAction>(id);
        keys.policies.quick.actions = {action};
        keys.Poll(); keys.held = {254};
        Check(!keys.Poll(), "An unrelated key opened Quick");
        keys.held = {OpeningActionKeys(keys.bindings, action).front()};
        Check(keys.Poll() == ClosedMenuOpeningIntent::Quick, "Canonical action used a guessed or combined key");
    }
}
void MissingDeadJumpBindingCannotAuthorizeFixedFallback() {
    auto bindings = SavedBindings(); bindings.jump.clear();
    ClosedMenuOpeningArbitrator arbiter{bindings};
    const ClosedMenuOpeningPolicies policies;
    const ClosedMenuOpeningContext context{{PlayerLifeState::Dead, true, false}, true};
    arbiter.Poll(context, policies, [](int) { return false; });
    Check(!arbiter.Poll(context, policies, [](int key) { return key == 2; }),
          "Unbound dead Jump was replaced with a guessed key");
}
void MalformedPoliciesAndEligibilityRejectBeforeReadingKeys() {
    for (int malformed = 0; malformed < 9; ++malformed) {
        Keys keys;
        if (malformed == 0) keys.policies.main.revision = 0;
        if (malformed == 1) keys.policies.quick.actions.clear();
        if (malformed == 2) keys.policies.quick.actions = {OpeningAction::Jump, OpeningAction::Jump};
        if (malformed == 3) keys.policies.quick.actions = {OpeningAction::Frag, OpeningAction::SecondaryOffhand};
        if (malformed == 4) keys.policies.quick.actions = {static_cast<OpeningAction>(255)};
        if (malformed == 5) keys.context.eligibility.life = static_cast<PlayerLifeState>(255);
        if (malformed == 6) keys.policies.main.actions = {OpeningAction::Aim, OpeningAction::Melee,
                                                        OpeningAction::Sprint, OpeningAction::Use};
        if (malformed == 7) keys.policies.main.actions.clear();
        if (malformed == 8) keys.policies.revision = 0;
        Reject([&] { keys.Poll(); });
        Check(keys.observed.empty(), "Invalid metadata read physical keys");
    }
    auto bindings = SavedBindings(); bindings.sprint.clear();
    ClosedMenuOpeningArbitrator arbiter{bindings};
    auto policies = ClosedMenuOpeningPolicies{}; policies.quick.actions = {OpeningAction::Sprint};
    unsigned calls = 0;
    Reject([&] { arbiter.Poll({}, policies, [&](int) { ++calls; return false; }); });
    Check(calls == 0, "Unavailable configured action reached the key reader");
}
void InvalidSavedBindingsRejectWithoutCreatingAnArbiter() {
    for (int malformed = 0; malformed < 4; ++malformed) {
        auto bindings = SavedBindings();
        if (malformed == 0) bindings.aim = {0};
        if (malformed == 1) bindings.secondaryOffhand = {69, 69};
        if (malformed == 2) bindings.jump = {86};
        if (malformed == 3) bindings.moveUp = {39};
        Reject([&] { ClosedMenuOpeningArbitrator arbiter{bindings}; });
    }
}
void ResetAndKeyReaderFailureCannotLeakPreviouslyHeldState() {
    Keys keys;
    keys.Poll(); keys.held = {2, 69};
    Check(keys.Poll() == ClosedMenuOpeningIntent::Quick, "Quick failed before reset");
    keys.arbiter.Reset();
    Check(!keys.Poll(), "Reset reused an already held completion");
    keys.Release();
    Reject([&] { keys.arbiter.Poll(keys.context, keys.policies, [](int) -> bool {
        throw std::runtime_error("Owned key reader failure");
    }); });
    keys.held = {2, 69};
    Check(!keys.Poll() && !keys.Poll(), "Key read failure leaked a fresh completion");
    keys.Release(); keys.held = {2, 69};
    Check(keys.Poll() == ClosedMenuOpeningIntent::Quick, "Fresh completion failed after key reader recovery");
}
} // namespace iw::overlay::tests::quick
