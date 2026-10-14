#include "../../native/render/credits/CreditsCatalog.h"
#include "../../native/render/credits/CreditsTimeline.h"
#include <cmath>
#include <functional>
#include <iostream>
#include <limits>
#include <stdexcept>
#include <string>
#include <vector>

namespace {
using namespace iw::overlay::credits;
void Require(bool value, const char* reason) { if (!value) throw std::runtime_error(reason); }
void Equal(float actual, float expected) { Require(std::abs(actual - expected) < .001f, "A credit fade or center differs"); }
const Line& Find(const Frame& frame, std::size_t index) {
    for (std::size_t line = 0; line < frame.count; ++line) if (frame.lines[line].index == index) return frame.lines[line];
    throw std::runtime_error("A scheduled credit line is absent");
}
}
int main() {
    std::vector<std::pair<std::string, std::function<void()>>> checks;
    checks.emplace_back("Catalog retains26 source slots with21 lines and six group headings", [] {
        unsigned texts = 0, separators = 0, titles = 0;
        for (const auto& row : Catalog()) { texts += !row.separator; separators += row.separator; titles += row.title; }
        Require(texts == 21 && separators == 5 && titles == 6, "Source catalog grouping differs");
        Require(Catalog()[0].text == "^1Apparition (BO3 reference)" && Catalog()[1].text == "Original ^1Black Ops 3 Zombies Menu",
            "The original BO3 credits were presented as an IW authorship claim");
    });
    checks.emplace_back("First line starts invisible at source center212 then fades and moves for ten seconds", [] {
        auto start = Sample(0); Require(start.count == 1, "The first scheduled line is missing");
        Equal(Find(start, 0).center, 212); Equal(Find(start, 0).opacity, 0); Equal(Find(start, 0).fontScale, 1.4f);
        Equal(Find(Sample(250), 0).center, 207); Equal(Find(Sample(250), 0).opacity, .5f);
        Equal(Find(Sample(500), 0).opacity, 1); Equal(Find(Sample(5000), 0).center, 112);
        Equal(Find(Sample(9750), 0).center, 17); Equal(Find(Sample(9750), 0).opacity, .5f);
        for (std::size_t line = 0; line < Sample(10000).count; ++line)
            Require(Sample(10000).lines[line].index != 0, "A finished line remained live");
    });
    checks.emplace_back("Five-sixths launch intervals do not accumulate integer rounding", [] {
        Require(Sample(833).count == 1 && Sample(834).count == 2, "The second launch boundary differs");
        Equal(Find(Sample(1000), 1).opacity, 1.0f / 3); Equal(Find(Sample(1000), 1).fontScale, 1.1f);
        Require(Sample(1666).count == 2 && Sample(1667).count == 3, "The third launch boundary differs");
    });
    checks.emplace_back("Space separators delay groups and reset heading font without drawing blank rows", [] {
        const std::size_t groups[]{0, 5, 11, 15, 18, 23};
        for (auto index : groups) {
            const double launch = Catalog()[index].launchSixths * 1000.0 / 6;
            Equal(Find(Sample(launch), index).fontScale, 1.4f); Equal(Find(Sample(launch), index).opacity, 0);
        }
        for (const auto& line : Sample(6000).lines) Require(!Catalog()[line.index].separator, "A blank group separator rendered");
        Require(Catalog()[5].launchSixths == 35 && Catalog()[23].launchSixths == 165,
            "Source two-and-a-half-second group gaps differ");
    });
    checks.emplace_back("All40000 millisecond samples stay bounded and never exceed nine live lines", [] {
        std::size_t maximum = 0;
        for (int elapsed = 0; elapsed < 40000; ++elapsed) {
            const auto frame = Sample(elapsed); maximum = std::max(maximum, frame.count);
            Require(frame.count <= MaximumLiveLines, "Credits exceeded the fixed live-line bound");
            for (std::size_t row = 0; row < frame.count; ++row) {
                const auto& line = frame.lines[row];
                Require(line.index < 26 && !Catalog()[line.index].separator && line.center >= 12 && line.center <= 212
                    && line.opacity >= 0 && line.opacity <= 1 && std::isfinite(line.center), "A live credit escaped its bounds");
            }
        }
        Require(maximum == 9, "The full source timeline was not exercised");
    });
    checks.emplace_back("The final line completes before the forty-second owner exit without restarting", [] {
        Require(Find(Sample(29167), 25).index == 25, "The final launch boundary differs");
        Require(Sample(39167).count == 0 && Sample(39999).count == 0 && Sample(40000).count == 0,
            "Credits restarted or outlived their fixed sequence");
    });
    checks.emplace_back("Invalid negative nonfinite and past-end elapsed values draw no lines", [] {
        for (double elapsed : {-1.0, 40000.0, std::numeric_limits<double>::infinity(),
            std::numeric_limits<double>::quiet_NaN(), std::numeric_limits<double>::max()})
            Require(Sample(elapsed).count == 0, "Invalid timing metadata invented credit rows");
    });
    checks.emplace_back("Source red markers form bounded white and red runs without leaking into another line", [] {
        const auto runs = SplitText(Catalog()[2].text);
        Require(runs.count == 2 && !runs.runs[0].red && runs.runs[1].red && runs.runs[1].text == "CF4_99",
            "Source inline red styling differs");
        const auto white = SplitText("Ideas"); Require(white.count == 1 && !white.runs[0].red, "Red state leaked into a later line");
        Require(SplitText("^1").count == 0 && SplitText("").count == 0, "An empty styled line produced a text run");
        Require(SplitText("^2literal").runs[0].text == "^2literal", "An unsupported code silently changed text");
    });
    unsigned passed = 0;
    for (const auto& [name, check] : checks) {
        try { check(); ++passed; std::cout << "PASS " << name << '\n'; }
        catch (const std::exception& error) { std::cerr << "FAIL " << name << ": " << error.what() << '\n'; return 1; }
    }
    std::cout << "PASS " << passed << " pure credits timeline checks\n";
}
