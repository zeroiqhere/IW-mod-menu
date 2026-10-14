#include "../../native/render/WidthAnimation.h"
#include <cmath>
#include <functional>
#include <iostream>
#include <limits>
#include <stdexcept>
#include <string>
#include <vector>

namespace {
using iw::overlay::WidthAnimation;
void Equal(float actual, float expected) {
    if (!std::isfinite(actual) || std::abs(actual - expected) > 0.001f) {
        throw std::runtime_error("Unexpected presentation width " + std::to_string(actual)
            + "; expected " + std::to_string(expected));
    }
}
void Require(bool value, const char* message) { if (!value) throw std::runtime_error(message); }
}
int main() {
    const std::vector<std::pair<std::string, std::function<void()>>> tests{
        {"Initial snapshots snap to their confirmed widths", [] {
            for (int width : {200, 260, 500}) {
                WidthAnimation animation; Equal(animation.Update(width, true, true, 1, 10), float(width));
                Equal(animation.Update(width, true, true, 1, 100), float(width));
            }
        }},
        {"Confirmed editor width changes interpolate for exactly fifty milliseconds", [] {
            WidthAnimation animation;
            Equal(animation.Update(260, true, true, 1, 0), 260);
            Equal(animation.Update(500, true, true, 1, 100), 260);
            Equal(animation.Update(500, true, true, 1, 112), 317.6f);
            Equal(animation.Update(500, true, true, 1, 125), 380);
            Equal(animation.Update(500, true, true, 1, 149), 495.2f);
            Equal(animation.Update(500, true, true, 1, 150), 500);
            Equal(animation.Update(500, true, true, 1, 1000), 500);
        }},
        {"Reversing before completion starts from the current displayed width", [] {
            WidthAnimation animation; animation.Update(260, true, true, 1, 0);
            animation.Update(500, true, true, 1, 0);
            Equal(animation.Update(200, true, true, 1, 25), 380);
            Equal(animation.Update(200, true, true, 1, 50), 290);
            Equal(animation.Update(200, true, true, 1, 75), 200);
        }},
        {"Initial entry and navigation changes snap rather than animate another context", [] {
            WidthAnimation animation; animation.Update(260, true, true, 1, 0);
            animation.Update(500, true, true, 1, 0);
            Equal(animation.Update(400, true, true, 2, 25), 400);
            Equal(animation.Update(500, true, false, 2, 26), 500);
            Equal(animation.Update(200, true, true, 2, 27), 200);
            Equal(animation.Update(400, false, true, 2, 28), 400);
            Equal(animation.Update(260, true, true, 2, 29), 260);
        }},
        {"Clock rollback disarms the old interval before unsigned subtraction", [] {
            WidthAnimation animation; animation.Update(260, true, true, 1, 100);
            animation.Update(500, true, true, 1, 110);
            Equal(animation.Update(500, true, true, 1, 135), 380);
            Equal(animation.Update(500, true, true, 1, 120), 500);
            Equal(animation.Update(200, true, true, 1, 121), 500);
            Equal(animation.Update(200, true, true, 1, 146), 350);
        }},
        {"Clock wrap and navigation maximum remain bounded", [] {
            const auto maximum = std::numeric_limits<std::uint64_t>::max();
            WidthAnimation animation; animation.Update(260, true, true, maximum, maximum - 25);
            animation.Update(500, true, true, maximum, maximum - 25);
            Equal(animation.Update(500, true, true, maximum, maximum - 1), 375.2f);
            Equal(animation.Update(500, true, true, maximum, 0), 500);
            Equal(animation.Update(200, true, true, 0, 1), 200);
        }},
        {"Every interpolation sample stays monotonic without overshoot", [] {
            for (const auto endpoints : std::vector<std::pair<int, int>>{{200, 500}, {500, 200}}) {
                WidthAnimation animation; animation.Update(endpoints.first, true, true, 1, 0);
                animation.Update(endpoints.second, true, true, 1, 0);
                float previous = float(endpoints.first);
                for (std::uint64_t now = 1; now <= 100; ++now) {
                    const float current = animation.Update(endpoints.second, true, true, 1, now);
                    Require(current >= 200 && current <= 500, "An animation sample escaped source bounds");
                    Require(endpoints.second > endpoints.first ? current >= previous : current <= previous,
                        "A fixed target changed direction during interpolation");
                    previous = current;
                }
                Equal(previous, float(endpoints.second));
            }
        }},
        {"Reset and invalid local widths cannot reuse an old interval", [] {
            WidthAnimation animation; animation.Update(260, true, true, 1, 0);
            animation.Update(500, true, true, 1, 0); animation.Reset();
            Equal(animation.Update(200, true, true, 1, 25), 200);
            animation.Reset(); Equal(animation.Update(std::numeric_limits<int>::min(), true, true, 1, 0), 200);
            animation.Reset(); Equal(animation.Update(std::numeric_limits<int>::max(), true, true, 1, 0), 500);
            animation.Update(200, true, true, 1, 0);
            Equal(animation.Update(200, true, true, 1, std::numeric_limits<std::uint64_t>::max()), 200);
        }}
    };
    unsigned passed = 0;
    for (const auto& [name, test] : tests) {
        try { test(); ++passed; std::cout << "PASS " << name << '\n'; }
        catch (const std::exception& error) { std::cerr << "FAIL " << name << ": " << error.what() << '\n'; return 1; }
    }
    std::cout << "PASS " << passed << " portable width animation checks\n";
}
