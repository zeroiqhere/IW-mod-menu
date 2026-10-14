#include "PlayerNavigationFixtureTests.h"
#include <exception>
#include <iostream>

int main(int argc, char** argv) {
    if (argc != 2) { std::cerr << "Expected one actual player navigation directory\n"; return 2; }
    try {
        iw::overlay::tests::playerMenus::ActualPlayerNavigationKeepsSourceInventoryScopesAndCallerLocks(argv[1]);
        std::cout << "PASS ActualPlayerNavigationKeepsSourceInventoryScopesAndCallerLocks\n1 passed, 0 failed\n";
        return 0;
    } catch (const std::exception& error) {
        std::cerr << "FAIL ActualPlayerNavigationKeepsSourceInventoryScopesAndCallerLocks: " << error.what() << "\n0 passed, 1 failed\n";
        return 1;
    }
}
