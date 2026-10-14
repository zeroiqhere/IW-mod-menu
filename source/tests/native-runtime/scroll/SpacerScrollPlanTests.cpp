#include "../../../native/shared/scroll/MenuScrollWindow.h"
#include "SpacerScrollTestModels.h"
#include <limits>
namespace iw::overlay::tests {
using namespace runtime::scroll;
void SpacerPlannerRecognizesAllThreeProductionDirectionalPaths() {
    for (int style = 0; style < 3; ++style) {
        for (bool up : {false, true}) {
            const auto source = spacer::Source(style == 2, style == 1, up ? 3 : 1);
            const auto plan =
                PlanSpacerScroll(source, up ? SpacerScrollDirection::Up : SpacerScrollDirection::Down);
            const auto branch = style < 2 ? SpacerScrollBranch::StableWindow
                                : up      ? SpacerScrollBranch::InitialWindowShiftThenRebuild
                                          : SpacerScrollBranch::FinalWindowShift;
            Check(plan && plan->branch == branch && plan->fromIndex == (up ? 3u : 1u) &&
                      plan->spacerIndex == 2 && plan->targetIndex == (up ? 1u : 3u) &&
                      plan->spacerCount == 1 &&
                      plan->capacity == (style == 2   ? 5u
                                         : style == 1 ? 8u
                                                      : 10u) &&
                      IsValidSpacerScrollPlan(*plan),
                  "Planner omitted a source-proven ordinary or Stealth spacer branch");
            Check(SpacerScrollLifetime(*plan) == (style == 2 && up ? 120u : 180u),
                  "A source spacer branch used the wrong hold or ghost lifetime");
        }
    }
}
void SpacerPlannerAllowsStableMultipleBlanksButRejectsShiftedAmbiguity() {
    // A single shifted blank may advance again before the source refresh rebuilds
    auto shifted = spacer::Source(true, false, 2);
    shifted.rows[2] = {"Ordinary", "OFF", true};
    shifted.rows[3] = {"", "", false, true};
    const auto threeWindows = PlanSpacerScroll(shifted, SpacerScrollDirection::Down);
    Check(threeWindows && threeWindows->branch == SpacerScrollBranch::InitialWindowShiftThenRebuild &&
              threeWindows->sourceFirst == 0 && threeWindows->spacerFirst == 1 &&
              threeWindows->targetFirst == 2 && SpacerScrollLifetime(*threeWindows) == 120,
          "Single shifted blank lost its intermediate window and detached ghost lifetime");
    auto source = spacer::Source();
    source.rows[3] = {"", "", false, true};
    auto plan = PlanSpacerScroll(source, SpacerScrollDirection::Down);
    Check(plan && plan->targetIndex == 4 && plan->spacerCount == 2 &&
              plan->branch == SpacerScrollBranch::StableWindow && SpacerScrollLifetime(*plan) == 240,
          "Stable recursive blanks lost their accumulated half-waits");
    source = spacer::Source(true);
    source.selectedIndex = 0;
    source.rows[1] = {"", "", false, true};
    plan = PlanSpacerScroll(source, SpacerScrollDirection::Down);
    Check(plan && plan->targetIndex == 3 && plan->spacerCount == 2 &&
              plan->branch == SpacerScrollBranch::FinalWindowShift,
          "Valid multi-blank final shift was rejected");
    source.selectedIndex = 1;
    source.rows[1] = {"Ordinary", "OFF", true};
    source.rows[3] = {"", "", false, true};
    Check(!PlanSpacerScroll(source, SpacerScrollDirection::Down),
          "A shifted invalid step invented recursive history after its source rebuild");
}
void SpacerPlannerNeverSkipsDisabledRowsOrInventsWrapMovement() {
    auto source = spacer::Source();
    source.rows[2] = {"Unavailable", "UNAVAILABLE", false};
    Check(!PlanSpacerScroll(source, SpacerScrollDirection::Down), "Disabled ordinary row became a spacer");
    source.rows[2] = {"", "", false, true};
    source.rows[3].enabled = false;
    Check(PlanSpacerScroll(source, SpacerScrollDirection::Down)->targetIndex == 3,
          "Disabled ordinary endpoint was skipped");
    source.selectedIndex = 0;
    source.rows[15] = {"", "", false, true};
    Check(!PlanSpacerScroll(source, SpacerScrollDirection::Up), "Wrapped source rebuild became a hold event");
    for (auto &row : source.rows) {
        row = {"", "", false, true};
    }
    source.rows[0] = {"Only row", "OFF", true};
    Check(!PlanSpacerScroll(source, SpacerScrollDirection::Down),
          "Same-index wrap fabricated an accepted movement");
}
void SpacerPlannerRejectsForgedBranchWindowsAndMalformedTopology() {
    auto source = spacer::Source();
    const auto valid = PlanSpacerScroll(source, SpacerScrollDirection::Down);
    Check(valid.has_value(), "Valid plan was not available for mutation checks");
    for (int invalid = 0; invalid < 9; ++invalid) {
        auto bad = *valid;
        if (invalid == 0) {
            bad.branch = SpacerScrollBranch::FinalWindowShift;
        }
        if (invalid == 1) {
            bad.targetFirst = 1;
        }
        if (invalid == 2) {
            bad.spacerCount = 2;
        }
        if (invalid == 3) {
            bad.durationMilliseconds = 125;
        }
        if (invalid == 4) {
            bad.targetIndex = 2;
        }
        if (invalid == 5) {
            bad.topology.spacerMask[1] = 1;
        }
        if (invalid == 6) {
            bad.topology.openingActions[2] = 4;
        }
        if (invalid == 7) {
            bad.topology.openingActions[0] = 13;
        }
        if (invalid == 8) {
            bad.direction = static_cast<SpacerScrollDirection>(999);
        }
        Check(!IsValidSpacerScrollPlan(bad) && !SpacerScrollLifetime(bad),
              "Forged plan reached presentation timing");
    }
    source.rows[2].label = "Not blank";
    Check(!CaptureTopology(source), "Malformed spacer topology was captured");
    source = spacer::Source();
    source.rows.resize(129);
    Check(!CaptureTopology(source), "Oversized topology was captured");
}
void SpacerPlannerRejectsLegacyHiddenAndEveryModalRoute() {
    for (int invalid = 0; invalid < 12; ++invalid) {
        auto source = spacer::Source();
        if (invalid == 0) {
            source.isOpen = false;
        }
        if (invalid == 1) {
            source.menuRevision.reset();
        }
        if (invalid == 2) {
            source.presentationFocusRevision.reset();
        }
        if (invalid == 3) {
            source.widthEditor.emplace();
        }
        if (invalid == 4) {
            source.instructionPositionEditor.emplace();
        }
        if (invalid == 5) {
            source.menuPositionEditor.emplace();
        }
        if (invalid == 6) {
            source.menuCredits.emplace();
        }
        if (invalid == 7) {
            source.menuUnlock = MenuUnlockPhase::Rebuild;
        }
        if (invalid == 8) {
            source.menuPreferenceRefresh = MenuPreferenceRefreshPhase::Pending;
        }
        if (invalid == 9) {
            source.menuDesignTransition = MenuDesignTransitionPhase::Rebuild;
        }
        if (invalid == 10) {
            source.menuOpenTransition = MenuOpenTransitionPhase::Cooldown;
        }
        if (invalid == 11) {
            source.menuPreferenceRebuild = MenuPreferenceRebuildPhase::Cooldown;
        }
        Check(!PlanSpacerScroll(source, SpacerScrollDirection::Down),
              "A locked or unconfirmed route created a receipt");
    }
}
void SharedScrollWindowKeepsExactSourceBoundariesAndRejectsOverflowInputs() {
    for (std::size_t capacity : {5u, 8u, 10u, 14u}) {
        for (std::size_t selected = 0; selected < 16; ++selected) {
            const auto expected =
                std::min(16 - capacity, selected > (capacity - 1) / 2 ? selected - (capacity - 1) / 2 : 0);
            Check(MenuScrollWindowFirst(16, capacity, selected) == expected,
                  "Shared source window boundary changed");
        }
    }
    Check(MenuScrollWindowFirst(0, 5, 0) == 0 && MenuScrollWindowFirst(3, 5, 2) == 0,
          "Empty or short layout changed its fixed first row");
    Check(!MenuScrollWindowFirst(16, 0, 1) && !MenuScrollWindowFirst(16, 15, 1) &&
              !MenuScrollWindowFirst(129, 5, 1) && !MenuScrollWindowFirst(16, 5, 16) &&
              !MenuScrollWindowFirst(std::numeric_limits<std::size_t>::max(), 5, 1),
          "Invalid window geometry was admitted");
}
} // namespace iw::overlay::tests
