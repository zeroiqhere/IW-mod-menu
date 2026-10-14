#include "CallbackFixtureTests.h"
#include <exception>
#include <iostream>

int main(int argc, char** argv) {
    // The caller supplies a separate callback inventory, never the preview directory
    if (argc != 2) {
        std::cerr << "Expected one callback fixture directory\n";
        return 2;
    }
    try {
        iw::overlay::tests::callback::ActualCallbacksKeepOwnedWaitsSeparateFromBackendWork(argv[1]);
        std::cout << "PASS ActualCallbacksKeepOwnedWaitsSeparateFromBackendWork\n1 passed, 0 failed\n";
        return 0;
    } catch (const std::exception& error) {
        std::cerr << "FAIL ActualCallbacksKeepOwnedWaitsSeparateFromBackendWork: " << error.what() << "\n0 passed, 1 failed\n";
        return 1;
    }
}
