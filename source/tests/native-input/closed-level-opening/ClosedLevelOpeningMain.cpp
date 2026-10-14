#include "ClosedLevelOpeningSupport.h"
#include <iostream>
#include <string_view>
#include <utility>
#include <vector>
namespace {
std::vector<std::pair<std::string_view, void (*)()>> checks;
void Add(const char *name, void (*run)()) { checks.emplace_back(name, run); }
} // namespace
int main() {
  using namespace iw::overlay::runtime::tests::closedLevel;
  RegisterArbitrationChecks(Add);
  RegisterRouterChecks(Add);
  int failed = 0;
  for (const auto &[name, run] : checks) {
    try {
      run();
      std::cout << "PASS " << name << '\n';
    } catch (const std::exception &error) {
      ++failed;
      std::cout << "FAIL " << name << ": " << error.what() << '\n';
    }
  }
  std::cout << checks.size() - failed << '/' << checks.size()
            << " closed-level opening checks passed\n";
  return failed ? 1 : 0;
}
