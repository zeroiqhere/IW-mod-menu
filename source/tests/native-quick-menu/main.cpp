#include "TestSupport.h"
#include <iostream>
int main() {
    using namespace iw::overlay::tests::quick;
    struct Test { const char *name; void (*run)(); };
    const Test tests[] = {
        {"LivingMainHasPriorityAndConsumesSimultaneousQuickCompletion", LivingMainHasPriorityAndConsumesSimultaneousQuickCompletion},
        {"LivingQuickUsesConfiguredExactGroupsAndFreshFullCompletion", LivingQuickUsesConfiguredExactGroupsAndFreshFullCompletion},
        {"DeadQuickUsesFixedAimJumpIndependentOfConfiguredChords", DeadQuickUsesFixedAimJumpIndependentOfConfiguredChords},
        {"UnknownPlayerLifeNeverAuthorizesAnOpening", UnknownPlayerLifeNeverAuthorizesAnOpening},
        {"AuthorizationAndDisabledControlsBlockBothPolicies", AuthorizationAndDisabledControlsBlockBothPolicies},
        {"QuickDisableBlocksLivingAndDeadWithoutDeferringHeldInput", QuickDisableBlocksLivingAndDeadWithoutDeferringHeldInput},
        {"BothAggregateGroupsAreObservedBeforePriorityAndPendingReturns", BothAggregateGroupsAreObservedBeforePriorityAndPendingReturns},
        {"InstallationConsumesFullHoldsAndAllowsFreshPartialCompletion", InstallationConsumesFullHoldsAndAllowsFreshPartialCompletion},
        {"PendingAndBlockedCompletionsCannotOpenAfterRecovery", PendingAndBlockedCompletionsCannotOpenAfterRecovery},
        {"FocusRecoveryAndMenuClosureRequireFreshCompletion", FocusRecoveryAndMenuClosureRequireFreshCompletion},
        {"PolicyRevisionTupleAndEligibilityChangesReseedHeldChords", PolicyRevisionTupleAndEligibilityChangesReseedHeldChords},
        {"IndependentPoliciesMayShareAllActionsWithoutCrossTupleRejection", IndependentPoliciesMayShareAllActionsWithoutCrossTupleRejection},
        {"SavedBindingOwnershipCannotBeRetargetedByCallerMutation", SavedBindingOwnershipCannotBeRetargetedByCallerMutation},
        {"MultiplePhysicalKeysCannotDuplicateAnAggregateAction", MultiplePhysicalKeysCannotDuplicateAnAggregateAction},
        {"EveryCanonicalActionUsesItsExactSavedGroup", EveryCanonicalActionUsesItsExactSavedGroup},
        {"MissingDeadJumpBindingCannotAuthorizeFixedFallback", MissingDeadJumpBindingCannotAuthorizeFixedFallback},
        {"MalformedPoliciesAndEligibilityRejectBeforeReadingKeys", MalformedPoliciesAndEligibilityRejectBeforeReadingKeys},
        {"InvalidSavedBindingsRejectWithoutCreatingAnArbiter", InvalidSavedBindingsRejectWithoutCreatingAnArbiter},
        {"ResetAndKeyReaderFailureCannotLeakPreviouslyHeldState", ResetAndKeyReaderFailureCannotLeakPreviouslyHeldState},
    };
    try {
        for (const auto &test : tests) {
            test.run();
            std::cout << "PASS " << test.name << '\n';
        }
        std::cout << "Passed " << std::size(tests) << " closed-menu opening foundation tests\n";
        return 0;
    } catch (const std::exception &error) {
        std::cerr << "FAIL " << error.what() << '\n';
        return 1;
    }
}
