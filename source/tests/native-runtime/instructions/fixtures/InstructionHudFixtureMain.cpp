#include "InstructionHudFixtureTests.h"
#include <exception>
#include <fstream>
#include <iostream>
int main(int argc,char** argv) {
    if (argc<2 || argc>3) return 2;
    std::ofstream report;
    if (argc==3) { report.open(argv[2]); if(!report) return 2; }
    auto& output=argc==3?static_cast<std::ostream&>(report):std::cout;
    try {
        iw::overlay::tests::instructions::ActualInstructionHudGraphJoinsCopiedAdmissionCreationSavedRoutesAndWithdrawal(argv[1]);
        output << "PASS ActualInstructionHudGraphJoinsCopiedAdmissionCreationSavedRoutesAndWithdrawal\n1 passed, 0 failed\n";
        return 0;
    } catch(const std::exception& error) {
        output << "FAIL " << error.what() << "\n0 passed, 1 failed\n";return 1;
    }
}
