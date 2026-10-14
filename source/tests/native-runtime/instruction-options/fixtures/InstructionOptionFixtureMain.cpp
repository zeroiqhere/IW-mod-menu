#include "InstructionOptionFixtureTests.h"
#include <exception>
#include <iostream>

int main(int argc, char** argv) {
    // The separate directory cannot replace or extend the ordinary preview inventory
    if (argc != 2) { std::cerr << "Expected one instruction option fixture directory\n"; return 2; }
    try {
        iw::overlay::tests::instructionOptions::ActualInstructionGraphKeepsCurrentFlagsPaintAndCallerWaitsSeparate(argv[1]);
        std::cout << "PASS ActualInstructionGraphKeepsCurrentFlagsPaintAndCallerWaitsSeparate\n1 passed, 0 failed\n";
        return 0;
    } catch (const std::exception& error) {
        std::cerr << "FAIL ActualInstructionGraphKeepsCurrentFlagsPaintAndCallerWaitsSeparate: " << error.what()
            << "\n0 passed, 1 failed\n";
        return 1;
    }
}
