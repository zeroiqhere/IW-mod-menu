#include "BooleanPaintFixtureTests.h"
#include <array>
#include <exception>
#include <iostream>
#include <utility>

int main(int argc, char** argv) {
    // This argument names a separate inventory and cannot alter ordinary preview counts
    if (argc != 2) { std::cerr << "Expected one boolean paint fixture directory\n"; return 2; }
    using namespace iw::overlay::tests::booleanPaint;
    using Test = void (*)(const std::filesystem::path&);
    const std::array<std::pair<const char*, Test>, 2> tests{{
        {"ActualBooleanPaintGraphKeepsCurrentResultsSeparateFromCopiedHudValues", ActualBooleanPaintGraphKeepsCurrentResultsSeparateFromCopiedHudValues},
        {"ActualCohortBooleanPaintKeepsAggregateAuthoritySeparateFromDisplaySamples", ActualCohortBooleanPaintKeepsAggregateAuthoritySeparateFromDisplaySamples}
    }};
    int failures = 0;
    for (const auto& [name, test] : tests) {
        try {
            test(argv[1]);
            std::cout << "PASS " << name << "\n";
        } catch (const std::exception& error) {
            ++failures;
            std::cerr << "FAIL " << name << ": " << error.what() << "\n";
        }
    }
    std::cout << tests.size() - failures << " passed, " << failures << " failed\n";
    return failures == 0 ? 0 : 1;
}
