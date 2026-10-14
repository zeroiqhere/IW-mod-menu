#include "SpacerScrollTestModels.h"
#include <limits>
namespace iw::overlay::tests {
using namespace runtime::scroll;
void SpacerReceiptRequiresExactRawAckAndOneConfirmedNavigation() {
    const auto source = spacer::Source();
    const auto target = spacer::Target(source, 3);
    // An older ACK preserves only the unchanged source, never an unconfirmed target
    SpacerScrollReceiptTracker tracker;
    Check(tracker.Capture(source, SpacerScrollDirection::Down, 11, 100), "Valid capture failed");
    Check(!tracker.Observe(source, 10, 116), "Old ACK promoted a receipt");
    const auto receipt = tracker.Observe(target, 11, 120);
    Check(receipt && receipt->sequence == 11 && receipt->issuedMilliseconds == 100 &&
              receipt->observedMilliseconds == 120 && receipt->targetNavigationRevision == 11,
          "Exact ACK did not promote confirmed navigation");
    for (int mismatch = 0; mismatch < 4; ++mismatch) {
        auto changed = target;
        const auto ack = mismatch == 0 ? 12u : 11u;
        if (mismatch == 1) changed.navigationRevision += 1;
        if (mismatch == 2) changed.selectedIndex = source.selectedIndex;
        if (mismatch == 3) changed = source;
        Check(tracker.Capture(source, SpacerScrollDirection::Down, 11, 100), "Capture failed");
        Check(!tracker.Observe(changed, ack, 120) && !tracker.Observe(target, 11, 121),
              "Dropped, jumped or same-index ACK retained a promotable receipt");
    }
}
void SpacerReceiptRetainsOldRenderedSourceAndAdvancesReportsWithoutRestart() {
    const auto source = spacer::Source();
    const auto target = spacer::Target(source, 3);
    SpacerScrollReceiptTracker tracker;
    Check(tracker.Capture(source, SpacerScrollDirection::Down, 11, 100), "Capture failed");
    Check(!tracker.Observe(source, 10, 116), "Old source became an animation");
    const auto first = tracker.Observe(target, 11, 120);
    const auto later = tracker.Observe(target, 11, 145);
    Check(first && later && first->issuedMilliseconds == later->issuedMilliseconds &&
              first->plan == later->plan && later->observedMilliseconds == 145,
          "Advancing reports restarted or rejected the same receipt");
    Check(!tracker.Observe(target, 11, 144) && !tracker.Observe(target, 11, 146),
          "A regressing report recovered without a fresh capture");
}
void SpacerReceiptRejectsChangedPrivateContextPolicyAndTopology() {
    const auto source = spacer::Source();
    for (int change = 0; change < 13; ++change) {
        auto target = spacer::Target(source, 3);
        switch (change) {
        case 0: target.menuRevision = 7; break;
        case 1: target.presentationFocusRevision = 9; break;
        case 2: target.isRootMenu = true; break;
        case 3: target.appearance.menuWidth = 270; break;
        case 4: target.accent = "#FFFFFF"; break;
        case 5: target.smoothRainbowTheme = true; break;
        case 6: target.mainOpeningControls = MainOpeningControls{1, {OpeningAction::Aim, OpeningAction::Melee}}; break;
        case 7: target.rows[2] = {"Ordinary", "OFF", true}; break;
        case 8: target.rows[4].openingAction = OpeningAction::Aim; break;
        case 9: target.rows.pop_back(); break;
        case 10: target.isOpen = false; break;
        case 11: target.menuPreferenceRefresh = MenuPreferenceRefreshPhase::Pending; break;
        case 12: target.menuRevision.reset(); break;
        }
        SpacerScrollReceiptTracker tracker;
        Check(tracker.Capture(source, SpacerScrollDirection::Down, 11, 100), "Capture failed");
        Check(!tracker.Observe(target, 11, 120), "Changed private context promoted old history");
    }
}
void SpacerReceiptExpiresFromDispatchWithoutHeartbeatReplay() {
    for (bool upwardShift : {false, true}) {
        const auto source = spacer::Source(upwardShift, false, upwardShift ? 3 : 1);
        const auto target = spacer::Target(source, upwardShift ? 1 : 3);
        const auto direction = upwardShift ? SpacerScrollDirection::Up : SpacerScrollDirection::Down;
        const auto lifetime = upwardShift ? 120u : 180u;
        SpacerScrollReceiptTracker tracker;
        Check(tracker.Capture(source, direction, 11, 100), "Capture failed");
        const auto late = tracker.Observe(target, 11, 100 + lifetime - 1);
        Check(late && late->issuedMilliseconds == 100, "Late ACK restarted the dispatch clock");
        Check(!tracker.Observe(target, 11, 100 + lifetime) &&
                  !tracker.Observe(target, 11, 100 + lifetime + 1),
              "Expired heartbeat replayed an animation");
        Check(tracker.Capture(source, direction, 12, 400) && tracker.Observe(target, 12, 410),
              "A fresh command could not recover after expiry");
    }
}
void SpacerReceiptChecksUnsignedBoundsAndRejectsRollback() {
    auto source = spacer::Source();
    SpacerScrollReceiptTracker tracker;
    Check(!tracker.Capture(source, SpacerScrollDirection::Down, 0, 100), "Zero sequence was accepted");
    source.navigationRevision = std::numeric_limits<std::uint64_t>::max();
    Check(!tracker.Capture(source, SpacerScrollDirection::Down, 1, 100), "Navigation wrapped");
    source = spacer::Source();
    source.mainOpeningControls = MainOpeningControls{0, {OpeningAction::Aim, OpeningAction::Melee}};
    Check(!tracker.Capture(source, SpacerScrollDirection::Down, 1, 100), "Invalid policy was accepted");
    source = spacer::Source();
    const auto target = spacer::Target(source, 3);
    const auto maximum = std::numeric_limits<std::uint64_t>::max();
    Check(tracker.Capture(source, SpacerScrollDirection::Down, maximum, maximum - 30) &&
              tracker.Observe(target, maximum, maximum - 1), "Safe unsigned clock bounds were rejected");
    Check(!tracker.Observe(target, maximum, 0), "Clock wrap fabricated elapsed time");
    Check(tracker.Capture(source, SpacerScrollDirection::Down, 1, 100) &&
              !tracker.Observe(target, 1, 99), "Pre-dispatch time was accepted");
}
void SpacerReceiptCopiesIdentityWithoutCachingRowValuesAndResetDropsPending() {
    auto source = spacer::Source();
    source.mainOpeningControls = MainOpeningControls{1, {OpeningAction::Aim, OpeningAction::Melee}};
    auto target = spacer::Target(source, 3);
    SpacerScrollReceiptTracker tracker;
    Check(tracker.Capture(source, SpacerScrollDirection::Down, 11, 100), "Capture failed");
    source.mainOpeningControls->revision = 2;
    source.appearance.menuWidth = 300;
    // Ordinary text and values remain authoritative in the newly parsed snapshot
    target.rows[4].label = "Current caption";
    target.rows[4].value = "ON";
    const auto receipt = tracker.Observe(target, 11, 120);
    Check(receipt && receipt->sourceOpeningControls->revision == 1 &&
              receipt->sourceAppearance.menuWidth == 260, "Capture retained mutable identity references");
    tracker.Reset();
    Check(!tracker.Observe(target, 11, 121), "Reset leaked a published receipt");
}
void SpacerReceiptCancelsUnexpectedNavigationWhileAckIsPending() {
    const auto source = spacer::Source();
    const auto target = spacer::Target(source, 3);
    SpacerScrollReceiptTracker tracker;
    Check(tracker.Capture(source, SpacerScrollDirection::Down, 11, 100), "Capture failed");
    Check(!tracker.Observe(target, 10, 110) && !tracker.Observe(target, 11, 120),
          "Unacknowledged movement was later promoted as owned history");
    Check(tracker.Capture(source, SpacerScrollDirection::Down, 12, 200) &&
              tracker.Observe(target, 12, 220), "Fresh capture failed after pending cancellation");
}
} // namespace iw::overlay::tests
