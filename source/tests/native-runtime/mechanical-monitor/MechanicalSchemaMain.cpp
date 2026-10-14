#include "MechanicalSchemaSupport.h"
#include <array>
#include <iostream>

int main() {
    using namespace iw::overlay::tests::mechanical;
    struct Test { const char* name; void (*run)(); };
    const std::array tests{
        Test{"LegacyAbsenceKeepsTheCurrentReceiptFreeSnapshot", LegacyAbsenceKeepsTheCurrentReceiptFreeSnapshot},
        Test{"AllMechanicalKindsKeepWaitingReadyAndUnsignedBounds", AllMechanicalKindsKeepWaitingReadyAndUnsignedBounds},
        Test{"ClosedBackKeepsRetainedRootAndChildWithoutGrantingOpeningAuthority", ClosedBackKeepsRetainedRootAndChildWithoutGrantingOpeningAuthority},
        Test{"ExactReceiptShapesRejectWrongTypesMissingAndExtraFields", ExactReceiptShapesRejectWrongTypesMissingAndExtraFields},
        Test{"ReceiptRequiresCurrentKnownAdmissionAndOrdinaryChoice", ReceiptRequiresCurrentKnownAdmissionAndOrdinaryChoice},
        Test{"WaitingReceiptRequiresItsExactWaitAndAcknowledgment", WaitingReceiptRequiresItsExactWaitAndAcknowledgment},
        Test{"ValidCompetingPhasesCannotShareMechanicalContinuation", ValidCompetingPhasesCannotShareMechanicalContinuation},
        Test{"DecodedDuplicatesAndUnsignedOverflowRejectInBothReaders", DecodedDuplicatesAndUnsignedOverflowRejectInBothReaders},
        Test{"LegacyCommandsRemainByteExactWhenMenuRevisionIsOmitted", LegacyCommandsRemainByteExactWhenMenuRevisionIsOmitted},
        Test{"MechanicalCommandsSerializeAllThreeCapturedEpochs", MechanicalCommandsSerializeAllThreeCapturedEpochs},
        Test{"SuppliedMenuRevisionRequiresNavigationAndPositiveAdmission", SuppliedMenuRevisionRequiresNavigationAndPositiveAdmission},
        Test{"MenuRevisionCannotBeAttachedToUnrelatedCommands", MenuRevisionCannotBeAttachedToUnrelatedCommands}};
    int failures = 0;
    for (const auto& test : tests) {
        try { test.run(); std::cout << "PASS " << test.name << '\n'; }
        catch (const std::exception& error) { ++failures; std::cout << "FAIL " << test.name << ": " << error.what() << '\n'; }
    }
    std::cout << tests.size() - failures << '/' << tests.size() << " mechanical protocol checks passed\n";
    return failures ? 1 : 0;
}
