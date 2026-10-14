#include "FooterObservationGraphTests.h"
#include <array>
#include <fstream>
#include <iostream>
#include <stdexcept>
int main(int argc, char** argv) {
    if (argc != 2 && argc != 3) return 2;
    std::ofstream dump;
    if (argc == 3) {
        dump.open(argv[2]);
        if (!dump) return 2;
    }
    struct Case {
        const char* name;
        void (*run)(const std::filesystem::path&, std::ostream*);
    };
    const std::array<Case, 2> cases{{
        {"ActualFooterCreationSamplesSurviveFailedAndThrowingGraphicsPreparation", iw::overlay::tests::footer_observation::ActualFooterCreationSamplesSurviveFailedAndThrowingGraphicsPreparation},
        {"ActualFooterChronologySeparatesColorObservationFromPaintAndGameAuthority", iw::overlay::tests::footer_observation::ActualFooterChronologySeparatesColorObservationFromPaintAndGameAuthority}
    }};
    unsigned passed = 0, failed = 0;
    for (const auto& test : cases) {
        try {
            test.run(argv[1], dump ? &dump : nullptr);
            std::cout << "PASS " << test.name << '\n';
            ++passed;
        } catch (const std::exception& exception) {
            std::cerr << "FAIL " << test.name << ": " << exception.what() << '\n';
            ++failed;
        }
    }
    std::cout << passed << " passed, " << failed << " failed\n";
    return failed == 0 ? 0 : 1;
}
