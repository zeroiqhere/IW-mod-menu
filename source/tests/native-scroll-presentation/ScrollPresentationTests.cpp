#include "paint/BooleanPaintScrollTests.h"
#include "callbacks/CallbackScrollTests.h"
#include "policy/LocalMenuEligibilityScrollTests.h"
#include "../../native/render/ScrollPresentation.h"
#include "spacer/SpacerScrollTests.h"
#include "policy/OpeningPolicyScrollTests.h"
#include <cmath>
#include <functional>
#include <iostream>
#include <limits>
#include <stdexcept>
#include <string>
#include <vector>

namespace {
using namespace iw::overlay;
void Require(bool value, const char* message) { if (!value) { throw std::runtime_error(message); } }
void Equal(float value, float expected) { Require(std::abs(value - expected) < 0.001f, "A sampled source row or style weight differs"); }
ScrollRequest Request(std::size_t count = 8, std::size_t selected = 0, std::size_t first = 0) {
    ScrollRequest result; result.active = true; result.menuRevision = 5; result.focusRevision = 0;
    result.rowCount = count; result.selected = selected; result.first = first; result.shown = std::min<std::size_t>(count, 10);
    result.layout = {640, 480, 260, static_cast<float>(result.shown * 18), 10}; return result;
}
const ScrollRow& Row(const ScrollFrame& frame, std::size_t index) {
    for (std::size_t row = 0; row < frame.count; ++row) if (frame.rows[row].index == index) return frame.rows[row];
    throw std::runtime_error("A required logical row is missing");
}
void Bounded(const ScrollFrame& frame) {
    Require(frame.count <= MaximumScrollRows, "Presentation exceeded its fixed row bound");
    for (std::size_t row = 0; row < frame.count; ++row) {
        const auto& item = frame.rows[row];
        Require(item.index < frame.rowCount && std::isfinite(item.position) && item.selection >= 0 && item.selection <= 1
            && item.visibility >= 0 && item.visibility <= 1, "A row identity or weight escaped its bound");
        for (std::size_t other = 0; other < row; ++other) Require(frame.rows[other].index != item.index, "A row identity was drawn twice");
    }
    Require(std::isfinite(frame.scroller), "A cursor position is nonfinite");
}
}
int main() {
    std::vector<std::pair<std::string, std::function<void()>>> checks;
    checks.emplace_back("First observation snaps to the confirmed cursor without an invented opening motion", [] {
        ScrollPresentation animation; auto request = Request(8, 4); const auto frame = animation.Update(request, 100);
        Equal(frame.scroller, 4); Equal(Row(frame, 4).selection, 1); Require(!frame.transitioning, "Initial cursor animated");
    });
    checks.emplace_back("Adjacent cursor movement animates the bar and both selection weights for the captured duration", [] {
        ScrollPresentation animation; auto request = Request(); animation.Update(request, 0); request.selected = 1;
        Equal(animation.Update(request, 100).scroller, 0);
        auto half = animation.Update(request, 160); Equal(half.scroller, 0.5f); Equal(Row(half, 0).selection, 0.5f); Equal(Row(half, 1).selection, 0.5f);
        auto done = animation.Update(request, 220); Equal(done.scroller, 1); Require(!done.transitioning, "Completed animation stayed active");
    });
    checks.emplace_back("One-row downward window shifts move common rows and fade stationary edge rows", [] {
        ScrollPresentation animation; auto request = Request(37, 4); animation.Update(request, 0); request.selected = 5; request.first = 1;
        animation.Update(request, 100); const auto half = animation.Update(request, 160);
        Equal(half.scroller, 4); Equal(Row(half, 1).position, 0.5f); Equal(Row(half, 9).position, 8.5f);
        Equal(Row(half, 0).position, 0); Equal(Row(half, 0).visibility, 0.5f); Require(Row(half, 0).outgoing, "Outgoing edge was retained as a normal row");
        Equal(Row(half, 10).position, 9); Equal(Row(half, 10).visibility, 0.5f);
        const auto done = animation.Update(request, 220); Require(done.count == 10, "Expired outgoing edge was retained"); Equal(Row(done, 1).position, 0);
    });
    checks.emplace_back("One-row upward window shifts mirror movement while keeping incoming and outgoing edges stationary", [] {
        ScrollPresentation animation; auto request = Request(37, 5, 1); animation.Update(request, 0); request.selected = 4; request.first = 0;
        animation.Update(request, 100); auto half = animation.Update(request, 160);
        Equal(Row(half, 1).position, 0.5f); Equal(Row(half, 0).position, 0); Equal(Row(half, 0).visibility, 0.5f);
        Equal(Row(half, 10).position, 9); Equal(Row(half, 10).visibility, 0.5f);
    });
    checks.emplace_back("Short-list wrap moves across the visible list while long-list wrap refreshes immediately", [] {
        ScrollPresentation shortList; auto request = Request(8); shortList.Update(request, 0); request.selected = 7;
        shortList.Update(request, 100); Equal(shortList.Update(request, 160).scroller, 3.5f);
        ScrollPresentation longList; request = Request(37); longList.Update(request, 0); request.selected = 36; request.first = 27;
        const auto wrapped = longList.Update(request, 100); Equal(wrapped.scroller, 9); Require(!wrapped.transitioning, "A long wrap crossed unrelated windows");
    });
    checks.emplace_back("Large cursor jumps and discontinuous windows snap instead of inventing unseen intermediate input", [] {
        ScrollPresentation animation; auto request = Request(37); animation.Update(request, 0); request.selected = 10; request.first = 6;
        const auto frame = animation.Update(request, 100); Equal(frame.scroller, 4); Require(!frame.transitioning, "A large jump animated");
    });
    checks.emplace_back("Rapid reversals start from the sampled displayed bar and row styles", [] {
        ScrollPresentation animation; auto request = Request(); animation.Update(request, 0); request.selected = 1; animation.Update(request, 100);
        request.selected = 0; auto start = animation.Update(request, 130); Equal(start.scroller, 0.25f); Equal(Row(start, 1).selection, 0.25f);
        auto half = animation.Update(request, 190); Equal(half.scroller, 0.125f); Equal(Row(half, 1).selection, 0.125f);
        animation.Reset(); request = Request(37, 4); animation.Update(request, 0);
        request.selected = 5; request.first = 1; animation.Update(request, 100);
        request.selected = 4; request.first = 0; const auto reverse = animation.Update(request, 130);
        Equal(Row(reverse, 0).visibility, .75f); Equal(Row(reverse, 1).position, .75f);
        Equal(Row(reverse, 10).visibility, .25f); Require(Row(reverse, 10).outgoing, "Reversal did not retire the previously incoming edge");
        const auto rowHalf = animation.Update(request, 190);
        Equal(Row(rowHalf, 0).visibility, .875f); Equal(Row(rowHalf, 1).position, .875f);
        Equal(Row(rowHalf, 10).visibility, .125f);

    });
    checks.emplace_back("A committed duration change cannot stretch an already active transition", [] {
        ScrollPresentation animation; auto request = Request(); animation.Update(request, 0); request.selected = 1; animation.Update(request, 100);
        request.durationMilliseconds = 250; auto half = animation.Update(request, 160); Equal(half.scroller, 0.5f);
        Equal(animation.Update(request, 220).scroller, 1); request.selected = 2; animation.Update(request, 300);
        Equal(animation.Update(request, 425).scroller, 1.5f);
    });
    checks.emplace_back("Page structure focus layout and inactive contexts snap independently of cursor changes", [] {
        for (unsigned change = 0; change < 8; ++change) {
            ScrollPresentation animation; auto request = Request(); animation.Update(request, 0); request.selected = 1;
            if (change == 0) request.menuRevision = 6;
            if (change == 1) request.focusRevision = 1;
            if (change == 2) request.layout.viewportWidth = 960;
            if (change == 3) request.layout.largeCursor = true;
            if (change == 4) request.rowCount = 7;
            if (change == 5) request.active = false;
            if (change == 6) request.menuRevision.reset();
            if (change == 7) request.focusRevision.reset();
            const auto frame = animation.Update(request, 100); Equal(frame.scroller, 1); Require(!frame.transitioning, "A changed context inherited cursor motion");
        }
    });
    checks.emplace_back("Clock rollback and unsigned wrap snap before any elapsed subtraction", [] {
        ScrollPresentation animation; auto request = Request(); animation.Update(request, 100); request.selected = 1; animation.Update(request, 200);
        auto rollback = animation.Update(request, 150); Equal(rollback.scroller, 1); Require(!rollback.transitioning, "Clock rollback stayed active");
        animation.Reset(); animation.Update(request, std::numeric_limits<std::uint64_t>::max() - 10); request.selected = 2;
        animation.Update(request, std::numeric_limits<std::uint64_t>::max() - 5); auto wrapped = animation.Update(request, 0);
        Equal(wrapped.scroller, 2); Require(!wrapped.transitioning, "Unsigned wrap stayed active");
    });
    checks.emplace_back("Repeated retargets never exceed fifteen logical rows and overflow safely snaps", [] {
        ScrollPresentation animation; auto request = Request(37, 6); request.shown = 14; request.layout.capacity = 14;
        animation.Update(request, 0); request.selected = 7; request.first = 1; animation.Update(request, 100);
        request.selected = 8; request.first = 2; const auto overflow = animation.Update(request, 110);
        Bounded(overflow); Require(!overflow.transitioning && overflow.count == 14, "An overflowing row union did not snap");
    });
    checks.emplace_back("Hostile row counts and empty requests produce bounded finite fallback frames", [] {
        ScrollPresentation animation; ScrollRequest request; request.rowCount = request.shown = request.first = request.selected = std::numeric_limits<std::size_t>::max();
        Bounded(animation.Update(request, 0)); request = {}; auto empty = animation.Update(request, 1); Require(empty.count == 0, "Empty rows fabricated a cursor option");
    });
    checks.emplace_back("Invalid direct requests snap without seeding a later transition", [] {
        for (unsigned failure = 0; failure < 8; ++failure) {
            ScrollPresentation animation; auto valid = Request(); animation.Update(valid, 0);
            auto bad = valid; bad.selected = 1;
            if (failure == 0) bad.durationMilliseconds = 121;
            if (failure == 1) bad.durationMilliseconds = 0;
            if (failure == 2) bad.first = 200;
            if (failure == 3) bad.selected = 200;
            if (failure == 4) bad.layout.viewportWidth = 0;
            if (failure == 5) bad.layout.bodyHeight = -1;
            if (failure == 6) bad.shown = 0;
            if (failure == 7) bad.layout.capacity = 15;
            const auto rejected = animation.Update(bad, 100);
            Bounded(rejected); Require(!rejected.transitioning, "Invalid request entered a transition");
            valid.selected = 2;
            const auto recovered = animation.Update(valid, 110);
            Require(!recovered.transitioning, "Rejected request seeded later motion"); Equal(recovered.scroller, 2);
        }
    });
    checks.emplace_back("Every supported capacity stays finite and bounded across repeated window changes", [] {
        for (std::size_t capacity = 8; capacity <= 14; ++capacity) {
            ScrollPresentation animation; auto request = Request(37, capacity / 2 - 1);
            request.shown = request.layout.capacity = capacity; animation.Update(request, 0);
            for (std::size_t first = 1; first <= 20; ++first) {
                request.first = first; ++request.selected;
                animation.Update(request, first * 300);
                for (unsigned delta : {0u, 1u, 60u, 119u, 120u}) {
                    const auto frame = animation.Update(request, first * 300 + delta); Bounded(frame);
                    for (std::size_t row = 0; row < frame.count; ++row)
                        Require(frame.rows[row].position >= 0 && frame.rows[row].position <= capacity - 1,
                            "Window movement left its visible row range");
                }
            }
        }
    });
    checks.emplace_back("Exact committed origins reset motion even when adjacent signed positions share a float", [] {
        for (bool horizontal : {false, true}) {
            ScrollPresentation animation; auto request = Request();
            request.layout.menuX = request.layout.menuY = std::numeric_limits<std::int32_t>::max() - 1;
            request.layout.bodyX = static_cast<float>(request.layout.menuX);
            request.layout.bodyY = static_cast<float>(request.layout.menuY);
            animation.Update(request, 0); request.selected = 1;
            if (horizontal) ++request.layout.menuX; else ++request.layout.menuY;
            Require(request.layout.bodyX == static_cast<float>(request.layout.menuX)
                && request.layout.bodyY == static_cast<float>(request.layout.menuY), "This case did not exercise float-rounded origins");
            const auto reset = animation.Update(request, 100);
            Require(!reset.transitioning && reset.scroller == 1, "Adjacent exact signed origins inherited old motion");
        }
    });
    checks.emplace_back("Nonfinite body origins reject motion and cannot seed the next valid context", [] {
        for (bool horizontal : {false, true}) {
            ScrollPresentation animation; auto valid = Request(); animation.Update(valid, 0);
            auto bad = valid; bad.selected = 1;
            if (horizontal) bad.layout.bodyX = std::numeric_limits<float>::quiet_NaN();
            else bad.layout.bodyY = std::numeric_limits<float>::infinity();
            Require(!animation.Update(bad, 100).transitioning, "A nonfinite origin entered animation");
            valid.selected = 2; Require(!animation.Update(valid, 110).transitioning,
                "A rejected origin seeded the next motion");
        }
    });
    AddSpacerScrollChecks(checks);
    AddOpeningPolicyScrollChecks(checks);
    AddLocalMenuEligibilityScrollChecks(checks);
    AddBooleanPaintScrollChecks(checks);
    AddCallbackScrollChecks(checks);
    unsigned passed = 0;
    for (const auto& [name, check] : checks) {
        try { check(); ++passed; std::cout << "PASS " << name << '\n'; }
        catch (const std::exception& error) { std::cerr << "FAIL " << name << ": " << error.what() << '\n'; return 1; }
    }
    std::cout << "PASS " << passed << " pure scroll presentation checks\n";
}
