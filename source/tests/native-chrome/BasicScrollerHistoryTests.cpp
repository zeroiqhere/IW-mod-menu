#include "../../native/render/chrome/BasicScrollerHistory.h"
#include <functional>
#include <iostream>
#include <limits>
#include <stdexcept>
#include <vector>

using namespace iw::overlay;
namespace {
const layout::Color Blue{10, 20, 230, 255}, Red{220, 15, 30, 255}, Black{0, 0, 0, 255};
void Equal(std::optional<layout::Color> actual, layout::Color expected) {
    if (!actual || actual->red != expected.red || actual->green != expected.green
        || actual->blue != expected.blue || actual->alpha != expected.alpha)
        throw std::runtime_error("Unexpected source scroller RGB");
}
void StartsBlack() {
    BasicScrollerHistory history;
    Equal(history.Update(1, true, std::nullopt), Black);
}
void FixedRetainsSample() {
    BasicScrollerHistory history;
    Equal(history.Update(1, true, Blue), Blue);
    Equal(history.Update(1, true, std::nullopt), Blue);
}
void DirectUpdatesRgbOnly() {
    BasicScrollerHistory history; history.Update(1, true, Blue);
    // The color channel must not inherit a sampled alpha change
    Equal(history.Update(1, true, layout::Color{220, 15, 30, 0}), Red);
}
void AbsentPreservesSample() {
    BasicScrollerHistory history; history.Update(1, true, Blue);
    history.Update(1, false, Red);
    Equal(history.Update(1, true, std::nullopt), Blue);
}
void HiddenExistingStillSamples() {
    BasicScrollerHistory history; history.Update(1, true, Blue);
    // Pixel visibility is separate from the source object's existence
    Equal(history.Update(1, true, Red), Red);
    Equal(history.Update(1, true, std::nullopt), Red);
}
void NewCreationResets() {
    BasicScrollerHistory history; history.Update(1, true, Blue);
    Equal(history.Update(2, true, std::nullopt), Black);
}
void LegacyCannotRestoreOldHistory() {
    BasicScrollerHistory history; history.Update(1, true, Blue);
    if (history.Update(std::nullopt, true, Red)) throw std::runtime_error("Legacy invented history");
    Equal(history.Update(1, true, std::nullopt), Black);
}
void MissingAndRetiredCannotFabricateHistory() {
    BasicScrollerHistory history;
    if (history.Update(1, false, Blue)) throw std::runtime_error("Missing widget created history");
    Equal(history.Update(1, true, std::nullopt), Black);
    history.Update(2, true, Blue);
    if (history.Update(1, true, Red) || history.Update(2, true, Red))
        throw std::runtime_error("Regressing epoch revived history");
    Equal(history.Update(3, true, std::nullopt), Black);
    if (history.Update(0, true, Red) || history.Update(3, true, Red))
        throw std::runtime_error("Zero epoch revived history");
    // The largest valid creation uses no addition or wraparound
    const auto maximum = std::numeric_limits<std::uint64_t>::max();
    Equal(history.Update(maximum, true, Blue), Blue);
    Equal(history.Update(maximum, true, std::nullopt), Blue);
}
}
int main() {
    const std::vector<std::pair<const char*, std::function<void()>>> cases{
        {"New existing Basic chrome starts black", StartsBlack},
        {"Fixed themes retain the last observed direct Rainbow RGB", FixedRetainsSample},
        {"Each direct Rainbow observation replaces RGB without changing alpha", DirectUpdatesRgbOnly},
        {"Closed or absent widgets preserve color without creating a sample", AbsentPreservesSample},
        {"Hidden existing source scrollers still observe Rainbow", HiddenExistingStillSamples},
        {"Creation changes reset even when close or rebuild frames were missed", NewCreationResets},
        {"Absent epochs keep legacy callers stateless and cannot restore stale color", LegacyCannotRestoreOldHistory},
        {"Missing new widgets and regressing epochs cannot fabricate history", MissingAndRetiredCannotFabricateHistory}
    };
    unsigned passed = 0, failed = 0;
    for (const auto& [name, test] : cases) {
        try { test(); ++passed; std::cout << "PASS " << name << '\n'; }
        catch (const std::exception& error) { ++failed; std::cerr << "FAIL " << name << ": " << error.what() << '\n'; }
    }
    std::cout << passed << "/" << cases.size() << " Basic scroller history checks passed; " << failed << " failed\n";
    return failed ? 1 : 0;
}
