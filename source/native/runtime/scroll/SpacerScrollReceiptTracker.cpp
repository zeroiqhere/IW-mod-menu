#include "SpacerScrollReceiptTracker.h"
#include <limits>
#include <utility>

namespace iw::overlay::runtime::scroll {
namespace {
bool Ordinary(const MenuSnapshot &snapshot) noexcept {
    return snapshot.isOpen && !snapshot.widthEditor && !snapshot.instructionPositionEditor &&
           !snapshot.menuPositionEditor && !snapshot.menuCredits && !snapshot.menuUnlock &&
           !snapshot.menuOptionCallback && !snapshot.menuPreferenceRefresh && !snapshot.menuDesignTransition &&
           !snapshot.menuOpenTransition && !snapshot.menuPreferenceRebuild &&
           !snapshot.menuDesignFooterDesign;
}
bool ContextMatches(const MenuSnapshot &snapshot, const SpacerScrollReceipt &receipt) noexcept {
    const auto topology = CaptureTopology(snapshot);
    return Ordinary(snapshot) && snapshot.menuRevision == receipt.menuRevision &&
           snapshot.presentationFocusRevision == receipt.focusRevision &&
           snapshot.isRootMenu == receipt.sourceRootMenu && snapshot.appearance == receipt.sourceAppearance &&
           snapshot.accent == receipt.sourceAccent && snapshot.smoothRainbowTheme == receipt.sourceRainbow &&
           snapshot.mainOpeningControls == receipt.sourceOpeningControls &&
           snapshot.openingPolicies == receipt.sourceOpeningPolicies &&
           snapshot.localMenuEligibility == receipt.sourceLocalMenuEligibility &&
           snapshot.menuSurface == receipt.sourceMenuSurface &&
           snapshot.menuOptionCallback == receipt.sourceMenuOptionCallback &&
           snapshot.menuBooleanPaint == receipt.sourceMenuBooleanPaint && topology &&
           *topology == receipt.plan.topology;
}
} // namespace
bool SpacerScrollReceiptTracker::Capture(const MenuSnapshot &source, SpacerScrollDirection direction,
                                         std::uint64_t sequence, std::uint64_t issuedMilliseconds) {
    Reset();
    // Capture freezes identity after successful command IO rather than keeping snapshot pointers
    const auto plan = PlanSpacerScroll(source, direction);
    if (!IsKnownMenuSurface(source.menuSurface) || (source.localMenuEligibility
        && !IsValidLocalMenuEligibility(*source.localMenuEligibility))) return false;
    if (!plan || !sequence || source.navigationRevision == std::numeric_limits<std::uint64_t>::max() ||
        !IsValidOpeningPolicyOwnership(source.mainOpeningControls, source.openingPolicies)) {
        return false;
    }
    SpacerScrollReceipt receipt;
    receipt.plan = *plan;
    receipt.sequence = sequence;
    receipt.sourceNavigationRevision = source.navigationRevision;
    receipt.targetNavigationRevision = source.navigationRevision + 1;
    receipt.menuRevision = *source.menuRevision;
    receipt.focusRevision = *source.presentationFocusRevision;
    receipt.issuedMilliseconds = receipt.observedMilliseconds = issuedMilliseconds;
    receipt.sourceAppearance = source.appearance;
    receipt.sourceAccent = source.accent;
    receipt.sourceRainbow = source.smoothRainbowTheme;
    receipt.sourceRootMenu = source.isRootMenu;
    receipt.sourceOpeningControls = source.mainOpeningControls;
    receipt.sourceOpeningPolicies = source.openingPolicies;
    // A later family or admission observation cannot borrow this dispatch history
    receipt.sourceLocalMenuEligibility = source.localMenuEligibility;
    receipt.sourceMenuSurface = source.menuSurface;
    receipt.sourceMenuOptionCallback = source.menuOptionCallback;
    receipt.sourceMenuBooleanPaint = source.menuBooleanPaint;
    pending_ = std::move(receipt);
    previousClock_ = issuedMilliseconds;
    return true;
}
std::optional<SpacerScrollReceipt> SpacerScrollReceiptTracker::Observe(const MenuSnapshot &confirmed,
                                                                       std::uint64_t acknowledgedSequence,
                                                                       std::uint64_t observedMilliseconds) {
    if (!pending_) {
        return std::nullopt;
    }
    const auto lifetime = SpacerScrollLifetime(pending_->plan);
    // Compare elapsed differences without adding a deadline near the unsigned clock limit
    if (!lifetime || observedMilliseconds < pending_->issuedMilliseconds ||
        (previousClock_ && observedMilliseconds < *previousClock_) ||
        observedMilliseconds - pending_->issuedMilliseconds >= *lifetime ||
        !ContextMatches(confirmed, *pending_)) {
        Reset();
        return std::nullopt;
    }
    previousClock_ = observedMilliseconds;
    if (acknowledgedSequence < pending_->sequence) {
        // An older acknowledgment may retain pending state, but cannot conceal unrelated navigation
        if (confirmed.navigationRevision != pending_->sourceNavigationRevision ||
            confirmed.selectedIndex != pending_->plan.fromIndex) {
            Reset();
        }
        return std::nullopt;
    }
    // ACK advancement alone is insufficient because the owner also acknowledges rejected inputs
    if (acknowledgedSequence != pending_->sequence ||
        confirmed.navigationRevision != pending_->targetNavigationRevision ||
        confirmed.selectedIndex != pending_->plan.targetIndex) {
        Reset();
        return std::nullopt;
    }
    pending_->observedMilliseconds = observedMilliseconds;
    return pending_;
}
void SpacerScrollReceiptTracker::Reset() noexcept {
    pending_.reset();
    previousClock_.reset();
}
} // namespace iw::overlay::runtime::scroll
