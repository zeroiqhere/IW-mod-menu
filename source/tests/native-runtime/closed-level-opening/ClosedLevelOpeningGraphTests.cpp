#include "ClosedLevelOpeningGraphSupport.h"
#include <array>
#include <iostream>

int main(int argc, char** argv) {
    // The runner reads an existing export directory and cannot create game state
    if (argc != 2) return 2;
    using namespace iw::overlay::tests::closedLevelGraph;
    const std::array tests{
        std::pair{"ActualClosedInventoryMatchesLiteralPermissionsTimingAndBothReaders", ActualClosedInventoryMatchesLiteralPermissionsTimingAndBothReaders},
        std::pair{"ActualMainBackResumesHeldOpeningAtTwoFiftyAndCreatesChromeAtFifty", ActualMainBackResumesHeldOpeningAtTwoFiftyAndCreatesChromeAtFifty},
        std::pair{"ActualLivingQuickCreatesImmediatelyAndKeepsItsOwnFiveFiftyWait", ActualLivingQuickCreatesImmediatelyAndKeepsItsOwnFiveFiftyWait},
        std::pair{"ActualDeadAimJumpReopensOnlyAfterItsOwnBackWait", ActualDeadAimJumpReopensOnlyAfterItsOwnBackWait},
        std::pair{"ActualPermissionRecoveryUsesCurrentAuthorityWithoutANewChordEdge", ActualPermissionRecoveryUsesCurrentAuthorityWithoutANewChordEdge},
        std::pair{"ActualQuickDisabledKeepsMainPriorityAndDeniesLivingQuick", ActualQuickDisabledKeepsMainPriorityAndDeniesLivingQuick},
        std::pair{"ActualUnknownAndMissingFactsDenySourceLevelsWithoutLegacyBootstrap", ActualUnknownAndMissingFactsDenySourceLevelsWithoutLegacyBootstrap},
        std::pair{"ActualNavigationReplacementRejectsTheOriginalOpeningScope", ActualNavigationReplacementRejectsTheOriginalOpeningScope},
        std::pair{"ActualPolicyReplacementRejectsTheOriginalOpeningScope", ActualPolicyReplacementRejectsTheOriginalOpeningScope},
        std::pair{"ActualAdmissionReplacementRejectsTheOriginalOpeningScope", ActualAdmissionReplacementRejectsTheOriginalOpeningScope},
        std::pair{"ActualQueuedFocusLossRetiresThePendingOpenWithoutNativeReplay", ActualQueuedFocusLossRetiresThePendingOpenWithoutNativeReplay},
        std::pair{"ActualQueuedAdmissionReplacementNeverReusesTheOldCommandScope", ActualQueuedAdmissionReplacementNeverReusesTheOldCommandScope},
        std::pair{"ActualQueuedOwnerReplacementNeedsItsNewAdmissionScope", ActualQueuedOwnerReplacementNeedsItsNewAdmissionScope}};
    int failed = 0;
    // Independent graph failures remain visible without hiding later regressions
    for (const auto& [name, run] : tests) {
        try { run(argv[1]); std::cout << "PASS " << name << '\n'; }
        catch (const std::exception& error) { ++failed; std::cout << "FAIL " << name << ": " << error.what() << '\n'; }
    }
    std::cout << tests.size() - failed << '/' << tests.size() << " actual closed-level opening checks passed\n";
    // A single failed graph rejects the complete source gate
    return failed ? 1 : 0;
}
