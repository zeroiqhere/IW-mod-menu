#include "../../native/render/SmoothRainbow.h"
#include <array>
#include <functional>
#include <iostream>
#include <limits>
#include <stdexcept>
#include <string>
#include <vector>

namespace {
using iw::overlay::SampleSmoothRainbow;
using iw::overlay::layout::Color;
void Require(bool result, const char* message) { if (!result) { throw std::runtime_error(message); } }
bool Equal(Color a, Color b) { return a.red == b.red && a.green == b.green && a.blue == b.blue && a.alpha == b.alpha; }
void Expect(std::uint64_t clock, Color color) { Require(Equal(SampleSmoothRainbow(clock), color), "A source sector or rounded channel differs"); }
}
int main() {
    std::vector<std::pair<std::string, std::function<void()>>> checks;
    checks.emplace_back("Zero clock starts the explicit native phase at source brightness red", [] { Expect(0, {242, 0, 0, 255}); });
    checks.emplace_back("All six source color sectors use the exact component formulas", [] {
        Expect(830, {242, 121, 0, 255}); Expect(2500, {121, 242, 0, 255});
        Expect(4170, {0, 242, 122, 255}); Expect(5830, {0, 122, 242, 255});
        Expect(7500, {121, 0, 242, 255}); Expect(9170, {242, 0, 121, 255});
    });
    checks.emplace_back("Ten-millisecond steps hold each color until the next source tick", [] {
        Expect(9, {242, 0, 0, 255}); Expect(10, {242, 1, 0, 255});
        for (std::uint64_t tick = 0; tick < 1000; ++tick) {
            Require(Equal(SampleSmoothRainbow(tick * 10), SampleSmoothRainbow(tick * 10 + 9)),
                "The color changed within a source ten-millisecond step");
        }
    });
    checks.emplace_back("Nominal ten-second wrap returns to red without a saturated brightness jump", [] {
        Expect(9990, {242, 0, 1, 255}); Expect(10000, {242, 0, 0, 255});
        Require(Equal(SampleSmoothRainbow(0), SampleSmoothRainbow(10000)), "The source cycle did not repeat");
    });
    checks.emplace_back("Every sampled channel remains bounded by source brightness and stays opaque", [] {
        for (std::uint64_t tick = 0; tick < 1000; ++tick) {
            const auto color = SampleSmoothRainbow(tick * 10);
            Require(color.red <= 242 && color.green <= 242 && color.blue <= 242 && color.alpha == 255,
                "A color exceeded source brightness or changed alpha");
            Require(color.red == 242 || color.green == 242 || color.blue == 242, "A sector lost its full-value channel");
            Require(color.red == 0 || color.green == 0 || color.blue == 0, "A sector lost its zero channel");
        }
    });
    checks.emplace_back("A full native uptime is reduced before color arithmetic", [] {
        const auto maximum = std::numeric_limits<std::uint64_t>::max();
        Require(Equal(SampleSmoothRainbow(maximum), SampleSmoothRainbow(maximum % 10000)), "Maximum clock lost modulo precision");
        Require(Equal(SampleSmoothRainbow(maximum - 1), SampleSmoothRainbow((maximum - 1) % 10000)), "Adjacent maximum clock lost precision");
    });
    checks.emplace_back("Large clock multiples preserve exact periodic colors", [] {
        constexpr std::uint64_t base = 12345678900000000ULL;
        for (std::uint64_t tick = 0; tick < 1000; ++tick) {
            Require(Equal(SampleSmoothRainbow(base + tick * 10), SampleSmoothRainbow(tick * 10)), "A large clock changed cycle phase");
        }
    });
    checks.emplace_back("Independent samples retain phase across reordered navigation and closed intervals", [] {
        const auto first = SampleSmoothRainbow(1234), later = SampleSmoothRainbow(9123);
        SampleSmoothRainbow(0); SampleSmoothRainbow(99999);
        Require(Equal(SampleSmoothRainbow(1234), first) && Equal(SampleSmoothRainbow(9123), later),
            "Another draw or closed interval changed the absolute clock phase");
    });
    checks.emplace_back("Every source tick matches an independent rational channel oracle", [] {
        for (std::uint64_t tick = 0; tick < 1000; ++tick) {
            // 0.95 times 255 is 969 divided by 4, so this oracle needs no floating point
            const auto sector = (6 * tick) / 1000, fraction = (6 * tick) % 1000;
            const auto rising = static_cast<std::uint8_t>((969 * fraction + 2000) / 4000);
            const auto falling = static_cast<std::uint8_t>((969 * (1000 - fraction) + 2000) / 4000);
            Color expected{0, 0, 0, 255};
            switch (sector) {
                case 0: expected.red = 242; expected.green = rising; break;
                case 1: expected.red = falling; expected.green = 242; break;
                case 2: expected.green = 242; expected.blue = rising; break;
                case 3: expected.green = falling; expected.blue = 242; break;
                case 4: expected.red = rising; expected.blue = 242; break;
                default: expected.red = 242; expected.blue = falling; break;
            }
            Require(Equal(SampleSmoothRainbow(tick * 10), expected), "A source tick differs from rational brightness rounding");
        }
    });
    unsigned passed = 0;
    for (const auto& [name, check] : checks) {
        try { check(); ++passed; std::cout << "PASS " << name << '\n'; }
        catch (const std::exception& error) { std::cerr << "FAIL " << name << ": " << error.what() << '\n'; return 1; }
    }
    std::cout << "PASS " << passed << " pure Smooth Rainbow color checks\n";
}
