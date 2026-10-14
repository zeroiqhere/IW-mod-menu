#include "MonitorWaitSchemaTests.h"
#include <array>
#include <iostream>
#include <stdexcept>
int main(int argc, char** argv) {
    if (argc > 2) return 2;
    using namespace iw::overlay::tests::monitor;
    struct Test { const char* name; void (*run)(); };
    const std::array tests{
        Test{"LegacyPacketsKeepOrdinaryInputWithoutAWait", LegacyPacketsKeepOrdinaryInputWithoutAWait},
        Test{"ChoiceWaitCopiesPositiveBoundsWithoutInventingAModalOrEpoch", ChoiceWaitCopiesPositiveBoundsWithoutInventingAModalOrEpoch},
        Test{"BackWaitOwnsOpenParentsAndClosedRootsInBothFamilies", BackWaitOwnsOpenParentsAndClosedRootsInBothFamilies},
        Test{"InvalidMechanicalWaitShapesAreRejected", InvalidMechanicalWaitShapesAreRejected},
        Test{"MechanicalWaitCannotBorrowUnknownLocalAdmission", MechanicalWaitCannotBorrowUnknownLocalAdmission},
        Test{"MechanicalWaitRejectsEveryCompetingCallbackAndModal", MechanicalWaitRejectsEveryCompetingCallbackAndModal},
        Test{"DecodedDuplicateWaitFieldsCannotReplaceTheirOwner", DecodedDuplicateWaitFieldsCannotReplaceTheirOwner},
        Test{"PreviewAndRuntimeShareStrictWaitOwnership", PreviewAndRuntimeShareStrictWaitOwnership}};
    int failed = 0;
    for (const auto& test : tests) {
        try { test.run(); std::cout << "PASS " << test.name << '\n'; }
        catch (const std::exception& error) { ++failed; std::cout << "FAIL " << test.name << ": " << error.what() << '\n'; }
    }
    std::size_t count = tests.size();
    if (argc == 2) {
        const std::array actualTests{
            std::pair{"ActualControllerWaitPacketsMatchBothStrictReaders", ActualControllerWaitPacketsMatchBothStrictReaders},
            std::pair{"ActualControllerWaitChronologyJoinsNativeRouting", ActualControllerWaitChronologyJoinsNativeRouting}};
        count += actualTests.size();
        for (const auto& [name, run] : actualTests) {
            try { run(argv[1]); std::cout << "PASS " << name << '\n'; }
            catch (const std::exception& error) { ++failed; std::cout << "FAIL " << name << ": " << error.what() << '\n'; }
        }
    }
    std::cout << count - failed << '/' << count << " mechanical wait schema checks passed\n";
    return failed ? 1 : 0;
}
