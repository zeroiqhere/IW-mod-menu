#include "Checks.h"
#include <iostream>
int main() {
    iw::overlay::tests::Checks checks;
    iw::overlay::tests::AddRowLayoutChecks(checks);
    iw::overlay::tests::AddRowLabelChecks(checks);
    iw::overlay::tests::AddRowFontChecks(checks);
    unsigned passed = 0;
    for (const auto &[name, check] : checks) {
        try {
            check();
            ++passed;
            std::cout << "PASS " << name << '\n';
        } catch (const std::exception &error) {
            std::cerr << "FAIL " << name << ": " << error.what() << '\n';
            return 1;
        }
    }
    std::cout << passed << '/' << checks.size() << " row text checks passed\n";
}
