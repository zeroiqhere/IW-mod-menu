#include "BasicChromeFixtureTests.h"
#include <exception>
#include <fstream>
#include <iostream>
int main(int argc, char** argv) {
    // The optional report is an owned test output for isolated Windows execution
    if (argc < 2 || argc > 3) { std::cerr << "Expected a Basic chrome fixture directory and optional report\n"; return 2; }
    std::ofstream report;
    if (argc == 3) { report.open(argv[2]); if (!report) return 2; }
    auto& output = argc == 3 ? static_cast<std::ostream&>(report) : std::cout;
    try {
        iw::overlay::tests::chrome::ActualBasicChromeGraphKeepsCreationSeparateFromRowsCallbacksAndClose(argv[1]);
        output << "PASS ActualBasicChromeGraphKeepsCreationSeparateFromRowsCallbacksAndClose\n1 passed, 0 failed\n";
        return 0;
    } catch (const std::exception& error) {
        output << "FAIL ActualBasicChromeGraphKeepsCreationSeparateFromRowsCallbacksAndClose: "
            << error.what() << "\n0 passed, 1 failed\n";
        return 1;
    }
}
