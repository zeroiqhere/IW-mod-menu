#pragma once
#include "../../native/runtime/quick/ClosedMenuOpeningArbitrator.h"
#include "../../native/runtime/OpeningActionBindings.h"
#include <set>
#include <stdexcept>

namespace iw::overlay::tests::quick {
using namespace runtime;
using runtime::quick::ClosedMenuOpeningArbitrator;
inline void Check(bool condition, const char *message) {
    if (!condition) throw std::runtime_error(message);
}
template <class Call> void Reject(Call call) {
    try { call(); } catch (const std::exception &) { return; }
    throw std::runtime_error("Expected invalid input rejection");
}
inline Bindings SavedBindings() {
    // The fixture uses explicit saved groups with the same allowed aliases as production
    return {{2}, {86, 6}, {2, 38}, {1, 40}, {70}, {51}, {52}, {1},
            {38}, {40}, {71}, {69}, {16}, {17}, {32}};
}
struct Keys {
    Bindings bindings = SavedBindings();
    ClosedMenuOpeningArbitrator arbiter{bindings};
    ClosedMenuOpeningContext context{{PlayerLifeState::Alive, true, false}, true, false, false, false};
    ClosedMenuOpeningPolicies policies;
    std::set<int> held;
    std::set<int> observed;
    std::optional<ClosedMenuOpeningIntent> Poll() {
        observed.clear();
        return arbiter.Poll(context, policies, [&](int key) {
            observed.insert(key);
            return held.contains(key);
        });
    }
    void Release() { held.clear(); Poll(); }
};
void LivingMainHasPriorityAndConsumesSimultaneousQuickCompletion();
void LivingQuickUsesConfiguredExactGroupsAndFreshFullCompletion();
void DeadQuickUsesFixedAimJumpIndependentOfConfiguredChords();
void UnknownPlayerLifeNeverAuthorizesAnOpening();
void AuthorizationAndDisabledControlsBlockBothPolicies();
void QuickDisableBlocksLivingAndDeadWithoutDeferringHeldInput();
void BothAggregateGroupsAreObservedBeforePriorityAndPendingReturns();
void InstallationConsumesFullHoldsAndAllowsFreshPartialCompletion();
void PendingAndBlockedCompletionsCannotOpenAfterRecovery();
void FocusRecoveryAndMenuClosureRequireFreshCompletion();
void PolicyRevisionTupleAndEligibilityChangesReseedHeldChords();
void IndependentPoliciesMayShareAllActionsWithoutCrossTupleRejection();
void SavedBindingOwnershipCannotBeRetargetedByCallerMutation();
void MultiplePhysicalKeysCannotDuplicateAnAggregateAction();
void EveryCanonicalActionUsesItsExactSavedGroup();
void MissingDeadJumpBindingCannotAuthorizeFixedFallback();
void MalformedPoliciesAndEligibilityRejectBeforeReadingKeys();
void InvalidSavedBindingsRejectWithoutCreatingAnArbiter();
void ResetAndKeyReaderFailureCannotLeakPreviouslyHeldState();
} // namespace iw::overlay::tests::quick
