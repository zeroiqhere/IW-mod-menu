#include "SpacerScrollPresentation.h"
#include "../../runtime/scroll/SpacerScrollPlan.h"
#include <algorithm>
#include <limits>

namespace iw::overlay::spacer {
namespace {
const ScrollRow* Find(const ScrollFrame& frame, std::size_t index) noexcept {
    for (std::size_t i = 0; i < frame.count; ++i)
        if (frame.rows[i].index == index) return &frame.rows[i];
    return nullptr;
}
float Blend(float from, float to, float progress) noexcept { return from + (to - from) * progress; }
std::uint64_t Hold(const SpacerScrollPlan& plan) noexcept {
    return static_cast<std::uint64_t>(plan.durationMilliseconds / 2) * plan.spacerCount;
}
bool TargetMatches(const ScrollRequest& request, const SpacerScrollReceipt& receipt) noexcept {
    const auto& plan = receipt.plan;
    // ACK and cursor epochs identify exactly one accepted command, never a later accumulated report
    return IsValidOpeningPolicyOwnership(receipt.sourceOpeningControls, receipt.sourceOpeningPolicies)
        && request.openingPolicyIdentity == CaptureOpeningPolicyIdentity(receipt.sourceOpeningControls, receipt.sourceOpeningPolicies)
        && request.localMenuEligibility == receipt.sourceLocalMenuEligibility
        && request.menuSurface == receipt.sourceMenuSurface
        && request.menuOptionCallback == receipt.sourceMenuOptionCallback
        && request.menuBooleanPaint == receipt.sourceMenuBooleanPaint
        && !request.menuOptionCallback
        && request.active && !request.opening && receipt.sequence != 0
        && request.navigationRevision == receipt.targetNavigationRevision
        && receipt.sourceNavigationRevision != std::numeric_limits<std::uint64_t>::max()
        && receipt.targetNavigationRevision == receipt.sourceNavigationRevision + 1
        && request.menuRevision == receipt.menuRevision && request.focusRevision == receipt.focusRevision
        && request.topology == plan.topology && request.rowCount == plan.topology.rowCount
        && request.selected == plan.targetIndex && request.first == plan.targetFirst
        && request.layout.capacity == plan.capacity && request.shown == std::min<std::size_t>(request.rowCount, plan.capacity)
        && request.durationMilliseconds == plan.durationMilliseconds
        && runtime::scroll::IsValidSpacerScrollPlan(plan);
}
void Weights(ScrollRow& row, const ScrollRow& target, float progress) noexcept {
    row.position = Blend(row.position, target.position, progress);
    row.selection = Blend(row.selection, target.selection, progress);
    row.visibility = Blend(row.visibility, target.visibility, progress);
    row.opacity = Blend(row.opacity.value_or(1), target.opacity.value_or(1), progress);
    row.decorationVisibility = Blend(row.decorationVisibility.value_or(1), target.decorationVisibility.value_or(1), progress);
}
ScrollRow Invisible(ScrollRow row) noexcept {
    row.visibility = 0;
    row.opacity = 0;
    row.decorationVisibility = 0;
    return row;
}
ScrollFrame InitialWindowFrame(const ScrollRequest& request, const SpacerScrollPlan& plan,
    const ScrollFrame& source, std::uint64_t elapsed) noexcept {
    auto frame = StaticScrollFrame(request);
    const auto half = static_cast<std::uint64_t>(plan.durationMilliseconds / 2);
    const float progress = static_cast<float>(std::min<std::uint64_t>(elapsed, plan.durationMilliseconds))
        / plan.durationMilliseconds;
    if (elapsed < half) {
        auto intermediate = request;
        intermediate.first = plan.spacerFirst;
        frame = StaticScrollFrame(intermediate);
        // The confirmed window identity remains final while its rows paint the earlier blank window
        frame.first = request.first;
        frame.scroller = source.scroller;
        for (std::size_t i = 0; i < frame.count; ++i) {
            auto& row = frame.rows[i];
            const float destination = row.position;
            if (const auto* old = Find(source, row.index)) {
                row = *old;
                row.position = Blend(old->position, destination, progress);
            } else {
                // Only the incoming blank-window edge fades in; final-only rows wait for RefreshMenu
                row.selection = 0;
                row.opacity = request.layout.spotlightCursor ? .4f : 1;
                const auto visible = row;
                row = Invisible(row);
                Weights(row, visible, progress);
            }
        }
    }
    const auto detached = plan.sourceFirst < plan.spacerFirst ? plan.sourceFirst
        : plan.sourceFirst + static_cast<std::uint32_t>(source.shown) - 1;
    if (const auto* old = Find(source, detached); old && !Find(frame, detached)) {
        if (frame.count == MaximumScrollRows) return StaticScrollFrame(request);
        auto ghost = *old;
        // Attached rows disappear at rebuild; this one detached edge finishes its original T fade
        Weights(ghost, Invisible(ghost), progress);
        ghost.outgoing = true;
        frame.rows[frame.count++] = ghost;
    }
    return frame;
}
}
bool Presentation::Admit(const ScrollRequest& source, const ScrollRequest& target,
    const ScrollFrame& displayed, std::uint64_t sourceObserved, std::uint64_t now) noexcept {
    Reset();
    const auto* receipt = target.spacerReceipt;
    if (!receipt || !TargetMatches(target, *receipt) || now < receipt->observedMilliseconds
        || receipt->observedMilliseconds < receipt->issuedMilliseconds || sourceObserved > receipt->issuedMilliseconds
        || source.navigationRevision != receipt->sourceNavigationRevision
        || source.selected != receipt->plan.fromIndex || source.first != receipt->plan.sourceFirst
        || source.topology != receipt->plan.topology || source.layout != target.layout
        || source.menuRevision != target.menuRevision || source.focusRevision != target.focusRevision
        || source.rowCount != target.rowCount || source.shown != target.shown
        || source.openingPolicyIdentity != target.openingPolicyIdentity
        || source.menuOptionCallback != target.menuOptionCallback
        || source.menuBooleanPaint != target.menuBooleanPaint
        || source.localMenuEligibility != target.localMenuEligibility || source.menuSurface != target.menuSurface
        || (!source.active && !source.opening)) return false;
    const auto lifetime = runtime::scroll::SpacerScrollLifetime(receipt->plan);
    if (!lifetime || now - receipt->issuedMilliseconds >= *lifetime) return false;
    // A second retiring edge cannot be added to the source's bounded row union
    if (displayed.count != source.shown) return false;
    plan_ = receipt->plan;
    source_ = displayed;
    sequence_ = receipt->sequence;
    issued_ = receipt->issuedMilliseconds;
    observed_ = receipt->observedMilliseconds;
    sampled_ = now;
    active_ = true;
    return true;
}
bool Presentation::Matches(const ScrollRequest& request, std::uint64_t now) const noexcept {
    const auto* receipt = request.spacerReceipt;
    const auto lifetime = runtime::scroll::SpacerScrollLifetime(plan_);
    return active_ && receipt && TargetMatches(request, *receipt) && receipt->plan == plan_
        && receipt->sequence == sequence_ && receipt->issuedMilliseconds == issued_
        && receipt->observedMilliseconds >= observed_ && now >= receipt->observedMilliseconds && now >= sampled_ && now >= issued_
        && lifetime && now - issued_ < *lifetime;
}
ScrollFrame Presentation::Sample(const ScrollRequest& request, std::uint64_t now) noexcept {
    auto frame = StaticScrollFrame(request);
    if (!Matches(request, now)) {
        // Direct helper callers receive the same rollback cancellation as the outer presentation owner
        Reset();
        return frame;
    }
    sampled_ = now;
    // New ACK reports refresh age but never move the original dispatch clock
    observed_ = request.spacerReceipt->observedMilliseconds;
    const auto elapsed = now - issued_;
    const auto hold = Hold(plan_);
    const bool initial = plan_.branch == SpacerScrollBranch::InitialWindowShiftThenRebuild;
    const float progress = static_cast<float>(std::min<std::uint64_t>(
        elapsed > hold ? elapsed - hold : 0, plan_.durationMilliseconds)) / plan_.durationMilliseconds;
    if (initial) {
        frame = InitialWindowFrame(request, plan_, source_, elapsed);
    } else {
        frame.scroller = Blend(source_.scroller, frame.scroller, progress);
        for (std::size_t i = 0; i < frame.count; ++i) {
            auto& row = frame.rows[i];
            const auto final = row;
            if (const auto* old = Find(source_, row.index)) {
                row = *old;
                row.outgoing = false;
                Weights(row, final, progress);
            } else {
                row = Invisible(final);
                Weights(row, final, progress);
            }
        }
        for (std::size_t i = 0; i < source_.count; ++i) {
            auto old = source_.rows[i];
            if (Find(frame, old.index)) continue;
            if (frame.count == MaximumScrollRows) return StaticScrollFrame(request);
            Weights(old, Invisible(old), progress);
            old.outgoing = true;
            frame.rows[frame.count++] = old;
        }
    }
    // The final selected index remains authoritative even while the old counter and bar are painted
    frame.transitioning = true;
    frame.spacerSequence = sequence_;
    frame.spacerElapsed = elapsed;
    frame.spacerPolicyIdentity = request.openingPolicyIdentity;
    frame.counterSelection = elapsed < hold ? plan_.fromIndex : plan_.targetIndex;
    return frame;
}
bool MatchesReceiptFrame(const ScrollFrame& frame, const ScrollRequest& request) noexcept {
    if (!frame.spacerSequence && !frame.spacerElapsed && !frame.counterSelection) return true;
    const auto* receipt = request.spacerReceipt;
    if (!receipt || !TargetMatches(request, *receipt) || frame.spacerSequence != receipt->sequence
        || !frame.spacerElapsed || !frame.counterSelection
        || frame.spacerPolicyIdentity != request.openingPolicyIdentity
        || frame.menuOptionCallback != request.menuOptionCallback
        || frame.menuBooleanPaint != request.menuBooleanPaint
        || frame.localMenuEligibility != request.localMenuEligibility || frame.menuSurface != request.menuSurface) return false;
    const auto lifetime = runtime::scroll::SpacerScrollLifetime(receipt->plan);
    return lifetime && receipt->observedMilliseconds >= receipt->issuedMilliseconds
        && *frame.spacerElapsed >= receipt->observedMilliseconds - receipt->issuedMilliseconds
        && *frame.spacerElapsed < *lifetime
        && *frame.counterSelection == (*frame.spacerElapsed < Hold(receipt->plan)
            ? receipt->plan.fromIndex : receipt->plan.targetIndex);
}
}
