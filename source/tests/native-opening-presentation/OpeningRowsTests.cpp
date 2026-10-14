#include "../../native/render/ScrollPresentation.h"
#include <cmath>
#include <functional>
#include <iostream>
#include <stdexcept>
#include <string>
#include <vector>

namespace {
using namespace iw::overlay;
void Require(bool condition, const char* message) { if (!condition) throw std::runtime_error(message); }
void Equal(float value, float expected) { Require(std::abs(value - expected) < .001f, "Opening or scroll takeover alpha differs"); }
ScrollRequest Request(std::size_t selected = 0, std::size_t count = 8, std::size_t first = 0) {
    ScrollRequest request; request.active = true; request.menuRevision = 3; request.focusRevision = 0;
    request.rowCount = count; request.selected = selected; request.first = first; request.shown = std::min<std::size_t>(count, 14);
    request.layout = {640, 480, 260, static_cast<float>(request.shown * 18), 14}; request.opening = MenuOpening{1, 0};
    return request;
}
float Alpha(const ScrollFrame& frame, std::size_t index) {
    for (std::size_t row = 0; row < frame.count; ++row) if (frame.rows[row].index == index) {
        Require(frame.rows[row].opacity.has_value(), "A row has no explicit opening alpha"); return *frame.rows[row].opacity;
    }
    throw std::runtime_error("A requested opening row is absent");
}
}
int main() {
    std::vector<std::pair<std::string, std::function<void()>>> checks;
    checks.emplace_back("All opening fades start together while ordinal zero is immediate", [] {
        ScrollPresentation motion; auto request = Request(2); auto initial = motion.Update(request, 100);
        Equal(Alpha(initial, 0), .4f); Equal(Alpha(initial, 1), 0); Equal(Alpha(initial, 2), 0);
        auto half = motion.Update(request, 150); Equal(Alpha(half, 1), .2f); Equal(Alpha(half, 2), .25f);
        request.opening->elapsedMilliseconds = 200; auto done = motion.Update(request, 300); Equal(Alpha(done, 2), 1);
    });
    checks.emplace_back("The final visible ordinal uses thirteen hundred milliseconds and visible rank", [] {
        auto request = Request(15, 40, 10); request.opening->elapsedMilliseconds = 650;
        const auto frame = StaticScrollFrame(request); Equal(Alpha(frame, 10), .4f); Equal(Alpha(frame, 23), .2f);
        request.layout.spotlightCursor = false; Equal(Alpha(StaticScrollFrame(request), 23), .5f);
    });
    checks.emplace_back("Scrolling takes over current alpha linearly instead of multiplying two fades", [] {
        ScrollPresentation motion; auto request = Request(1); motion.Update(request, 0);
        request.opening.reset(); request.selected = 2; auto start = motion.Update(request, 40); Equal(Alpha(start, 2), .08f);
        auto half = motion.Update(request, 100); Equal(Alpha(half, 2), .54f); Equal(Alpha(half, 1), .4f);
        auto done = motion.Update(request, 160); Equal(Alpha(done, 2), 1); Equal(Alpha(done, 1), .4f);
    });
    checks.emplace_back("A rapid reversal retargets both alpha channels from their displayed values", [] {
        ScrollPresentation motion; auto request = Request(1); motion.Update(request, 0);
        request.opening.reset(); request.selected = 2; motion.Update(request, 40);
        request.selected = 1; motion.Update(request, 70);
        const auto half = motion.Update(request, 130); Equal(Alpha(half, 2), .355f); Equal(Alpha(half, 1), .7f);
        for (std::size_t row = 0; row < half.count; ++row)
            Require(half.rows[row].decorationVisibility && *half.rows[row].decorationVisibility >= 0
                && *half.rows[row].decorationVisibility <= 1, "Retargeted decoration escaped its bound");
    });
    checks.emplace_back("A first ready moved packet can take over the last locked opening frame", [] {
        ScrollPresentation motion; auto request = Request(1); request.active = false; motion.Update(request, 0);
        request.active = true; request.selected = 2; request.opening.reset(); motion.Update(request, 40);
        Equal(Alpha(motion.Update(request, 100), 2), .54f);
    });
    checks.emplace_back("Window movement retains original ordinal alpha and fades stationary edges", [] {
        ScrollPresentation motion; auto request = Request(6, 40); request.opening->elapsedMilliseconds = 200; motion.Update(request, 0);
        request.opening.reset(); request.selected = 7; request.first = 1; motion.Update(request, 0);
        const auto half = motion.Update(request, 60); Equal(Alpha(half, 0), .2f);
        Equal(Alpha(half, 14), .2f); Equal(Alpha(half, 6), (1.0f / 3 + .4f) / 2);
    });
    checks.emplace_back("Locked design cooldown keeps owner opening clock alive", [] {
        ScrollPresentation motion; auto request = Request(2); request.active = false; motion.Update(request, 100);
        Equal(Alpha(motion.Update(request, 150), 2), .25f);
        request.opening->elapsedMilliseconds = 100; Equal(Alpha(motion.Update(request, 200), 2), .5f);
    });
    checks.emplace_back("Natural completion without movement snaps normal alpha and does not invent scrolling", [] {
        ScrollPresentation motion; auto request = Request(2); motion.Update(request, 0); request.opening.reset();
        const auto done = motion.Update(request, 40); Equal(Alpha(done, 2), 1); Require(!done.transitioning, "Natural completion invented movement");
    });
    checks.emplace_back("New page focus event and rollback reset opening rather than carrying old alpha", [] {
        ScrollPresentation motion; auto request = Request(2); motion.Update(request, 100); motion.Update(request, 150);
        request.menuRevision = 4; Equal(Alpha(motion.Update(request, 150), 2), 0);
        request.focusRevision = 1; Equal(Alpha(motion.Update(request, 180), 2), 0);
        request.opening->revision = 2; Equal(Alpha(motion.Update(request, 190), 2), 0);
        Equal(Alpha(motion.Update(request, 180), 2), 0);
    });
    checks.emplace_back("Unexpected cursor change with the same event uses static report instead of reranking", [] {
        ScrollPresentation motion; auto request = Request(1); request.opening->elapsedMilliseconds = 40; motion.Update(request, 0);
        request.selected = 2; const auto frame = motion.Update(request, 50);
        Require(!frame.transitioning, "An uncancelled opening descriptor started scrolling"); Equal(Alpha(frame, 2), .2f);
    });
    checks.emplace_back("Actual visible duration rejects completed reports and bounds a stale local lead", [] {
        ScrollPresentation motion; auto request = Request(7); request.opening->elapsedMilliseconds = 699;
        motion.Update(request, 0); const auto bounded = motion.Update(request, 5000);
        Equal(Alpha(bounded, 7), 699.0f / 700); Require(*bounded.openingElapsed == 699, "A stale phase exceeded the visible fade interval");
        request.opening->elapsedMilliseconds = 700; Equal(Alpha(motion.Update(request, 5100), 7), 1);
        request = Request(0, 1); Equal(Alpha(motion.Update(request, 5200), 0), 1);
    });
    checks.emplace_back("Legacy and invalid opening metadata cannot seed a future local phase", [] {
        ScrollPresentation motion; auto request = Request(2); request.menuRevision.reset();
        motion.Update(request, 0); Equal(Alpha(motion.Update(request, 100), 2), 0);
        request.opening->revision = 0; Equal(Alpha(motion.Update(request, 200), 2), 1);
        request.opening = MenuOpening{1, 1301}; Equal(Alpha(motion.Update(request, 300), 2), 1);
        motion.Reset(); request = Request(2); Equal(Alpha(motion.Update(request, 400), 2), 0);
    });
    unsigned passed = 0;
    for (const auto& [name, check] : checks) {
        try { check(); ++passed; std::cout << "PASS " << name << '\n'; }
        catch (const std::exception& error) { std::cerr << "FAIL " << error.what() << '\n'; return 1; }
    }
    std::cout << "PASS " << passed << " opening row and takeover checks\n";
}
