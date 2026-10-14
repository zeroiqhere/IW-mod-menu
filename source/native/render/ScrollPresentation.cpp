#include "ScrollPresentation.h"
#include "../shared/MenuRowCapacity.h"
#include <algorithm>
#include <cmath>

namespace iw::overlay {
namespace {
float Blend(float from, float target, float progress) noexcept { return from + (target - from) * progress; }
const ScrollRow* Find(const ScrollFrame& frame, std::size_t index) noexcept {
    for (std::size_t row = 0; row < frame.count; ++row) if (frame.rows[row].index == index) return &frame.rows[row];
    return nullptr;
}
bool Valid(const ScrollRequest& request) noexcept {
    // Direct callers receive the same duration and visible-window limits as the wire model
    const auto& layout = request.layout;
    return (!request.menuBooleanPaint || (request.menuRevision && IsValidMenuBooleanPaint(*request.menuBooleanPaint, request.rowCount)))
        && (!request.menuOptionCallback || (IsValidMenuOptionCallback(*request.menuOptionCallback) && !request.active))
        && IsKnownMenuSurface(request.menuSurface)
        && (!request.localMenuEligibility || IsValidLocalMenuEligibility(*request.localMenuEligibility))
        && std::isfinite(layout.viewportWidth) && std::isfinite(layout.viewportHeight)
        && std::isfinite(layout.bodyWidth) && std::isfinite(layout.bodyHeight)
        && std::isfinite(layout.bodyX) && std::isfinite(layout.bodyY)
        && layout.viewportWidth > 0 && layout.viewportHeight > 0 && layout.bodyWidth > 0 && layout.bodyHeight > 0
        && layout.capacity >= 1 && layout.capacity <= 14 && request.rowCount <= 128
        && request.shown >= 1 && request.shown <= layout.capacity && request.shown <= request.rowCount
        && request.first <= request.rowCount - request.shown
        && request.selected >= request.first && request.selected - request.first < request.shown
        && request.durationMilliseconds >= 100 && request.durationMilliseconds <= 250
        && request.durationMilliseconds % 10 == 0;
}
bool ValidOpening(const ScrollRequest& request) noexcept {
    return !request.opening || ((!request.menuOptionCallback || CallbackKeepsOpening(*request.menuOptionCallback))
        && request.shown >= 2 && request.shown <= MaximumMenuRowsShown
        && request.opening->revision != 0 && request.opening->elapsedMilliseconds >= 0
        && request.opening->elapsedMilliseconds < static_cast<int>((request.shown - 1) * 100));
}
float TargetOpacity(const ScrollRequest& request, const ScrollRow& row) noexcept {
    return request.layout.spotlightCursor ? .4f + .6f * row.selection : 1;
}
void ApplyOpening(ScrollFrame& frame, const ScrollRequest& request, double elapsed) noexcept {
    if (!request.opening || !ValidOpening(request)) return;
    elapsed = std::clamp(elapsed, static_cast<double>(request.opening->elapsedMilliseconds),
        std::min(request.opening->elapsedMilliseconds + 100.0, static_cast<double>((request.shown - 1) * 100 - 1)));
    frame.opening = request.opening; frame.openingElapsed = elapsed;
    for (std::size_t row = 0; row < frame.count; ++row) {
        const auto ordinal = frame.rows[row].index - frame.first;
        // All rows begin together; ordinal zero has no fade duration
        const float progress = ordinal ? static_cast<float>(std::clamp(elapsed / (ordinal * 100.0), 0.0, 1.0)) : 1;
        frame.rows[row].opacity = TargetOpacity(request, frame.rows[row]) * progress;
        frame.rows[row].decorationVisibility = progress;
    }
}
std::size_t Distance(std::size_t a, std::size_t b) noexcept { return a > b ? a - b : b - a; }
}
ScrollFrame StaticScrollFrame(const ScrollRequest& request) noexcept {
    // Even rejected metadata produces a finite, fixed-size fallback with no outside row access
    ScrollFrame result; result.design = request.layout.design; result.layout = request.layout;
    result.rowCount = std::min<std::size_t>(request.rowCount, 128);
    result.shown = std::min({request.shown, result.rowCount, std::size_t{14}});
    result.first = std::min(request.first, result.rowCount - result.shown);
    result.selected = result.rowCount ? std::min(request.selected, result.rowCount - 1) : 0;
    result.localMenuEligibility = request.localMenuEligibility; result.menuSurface = request.menuSurface;
    result.menuOptionCallback = request.menuOptionCallback;
    result.menuBooleanPaint = request.menuBooleanPaint;
    result.count = result.shown; result.menuRevision = request.menuRevision; result.focusRevision = request.focusRevision;
    result.scroller = result.shown ? static_cast<float>(std::clamp(result.selected, result.first,
        result.first + result.shown - 1) - result.first) : 0;
    for (std::size_t row = 0; row < result.shown; ++row) {
        const auto index = result.first + row;
        result.rows[row] = {index, static_cast<float>(row), index == result.selected ? 1.0f : 0.0f, 1, false};
        result.rows[row].opacity = TargetOpacity(request, result.rows[row]);
        result.rows[row].decorationVisibility = 1;
    }
    if (request.opening) ApplyOpening(result, request, request.opening->elapsedMilliseconds);
    return result;
}
void ScrollPresentation::Snap(const ScrollRequest& request, std::uint64_t nowMilliseconds) noexcept {
    previous_ = request; previous_.spacerReceipt = nullptr; from_ = target_ = StaticScrollFrame(request);
    started_ = previousClock_ = sourceFirstObserved_ = nowMilliseconds; initialized_ = true; moving_ = false;
}
ScrollFrame ScrollPresentation::Current(std::uint64_t nowMilliseconds) const noexcept {
    // Context and rollback checks run before sampling, so elapsed never crosses a wrap
    if (!moving_ || nowMilliseconds < started_ || nowMilliseconds - started_ >= duration_) {
        auto frame = StaticScrollFrame(previous_);
        ApplyOpening(frame, previous_, openingClock_.Sample(nowMilliseconds)); return frame;
    }
    const float progress = static_cast<float>(nowMilliseconds - started_) / duration_;
    // Source HUD timings are known; the native linear easing is an explicit mapping
    auto frame = target_; frame.transitioning = true;
    frame.scroller = Blend(from_.scroller, target_.scroller, progress);
    for (std::size_t row = 0; row < frame.count; ++row) {
        frame.rows[row].position = Blend(from_.rows[row].position, target_.rows[row].position, progress);
        frame.rows[row].selection = std::clamp(Blend(from_.rows[row].selection, target_.rows[row].selection, progress), 0.0f, 1.0f);
        frame.rows[row].visibility = std::clamp(Blend(from_.rows[row].visibility, target_.rows[row].visibility, progress), 0.0f, 1.0f);
        // Alpha is its own engine-style channel, never the product of two concurrent fades
        frame.rows[row].opacity = std::clamp(Blend(*from_.rows[row].opacity, *target_.rows[row].opacity, progress), 0.0f, 1.0f);
        frame.rows[row].decorationVisibility = std::clamp(Blend(*from_.rows[row].decorationVisibility,
            *target_.rows[row].decorationVisibility, progress), 0.0f, 1.0f);
    }
    return frame;
}
ScrollFrame ScrollPresentation::Update(const ScrollRequest& request, std::uint64_t nowMilliseconds) noexcept {
    const auto next = StaticScrollFrame(request);
    if (!Valid(request) || !ValidOpening(request)) {
        // Invalid metadata cannot seed a later clock or opacity transition
        Reset(); return next;
    }
    const bool sameContext = initialized_ && request.menuRevision && request.focusRevision
        && request.menuRevision == previous_.menuRevision && request.focusRevision == previous_.focusRevision
        && request.menuOptionCallback == previous_.menuOptionCallback
        && request.menuBooleanPaint == previous_.menuBooleanPaint
        && request.localMenuEligibility == previous_.localMenuEligibility && request.menuSurface == previous_.menuSurface
        && request.layout == previous_.layout && request.rowCount == previous_.rowCount
        && request.shown == previous_.shown && nowMilliseconds >= previousClock_;
    if (spacer_.Active()) {
        if (sameContext && spacer_.Matches(request, nowMilliseconds)) {
            previousClock_ = nowMilliseconds;
            return spacer_.Sample(request, nowMilliseconds);
        }
        // Expired or foreign reports never revive a receipt from an already observed target
        spacer_.Reset();
        Snap(request, nowMilliseconds);
        return Current(nowMilliseconds);
    }
    if (sameContext && request.spacerReceipt) {
        const auto issued = request.spacerReceipt->issuedMilliseconds;
        // Later unchanged source reports retain first-observed history and its most recent bounded paint
        const auto sourcePaint = issued <= nowMilliseconds ? Current(std::max(issued, previousClock_)) : next;
        if (spacer_.Admit(previous_, request, sourcePaint, sourceFirstObserved_, nowMilliseconds)) {
            previous_ = request;
            previous_.spacerReceipt = nullptr;
            previousClock_ = nowMilliseconds;
            moving_ = false;
            openingClock_.Reset();
            return spacer_.Sample(request, nowMilliseconds);
        }
    }
    if (!sameContext) openingClock_.Reset();
    // Capture the old opening before consuming the owner's cancellation on a real cursor move
    const auto displayed = sameContext ? Current(nowMilliseconds) : next;
    const bool moved = request.selected != previous_.selected || request.first != previous_.first;
    const bool openingChanged = request.opening && (!previous_.opening
        || request.opening->revision != previous_.opening->revision
        || request.opening->elapsedMilliseconds < previous_.opening->elapsedMilliseconds);
    if (openingChanged || (request.opening && moved)) openingClock_.Reset();
    if (request.opening) openingClock_.Observe(request.opening->elapsedMilliseconds, request.opening->revision,
        request.menuRevision, request.focusRevision, nowMilliseconds);
    else openingClock_.Reset();
    const bool openingTakeover = sameContext && previous_.opening && !request.opening && moved;
    if (!sameContext || !request.active || (!previous_.active && !openingTakeover)
        || openingChanged || (request.opening && moved)) {
        // Locked design rows may still fade; a new descriptor never inherits a cursor path
        Snap(request, nowMilliseconds); return Current(nowMilliseconds);
    }
    if (!moved) {
        // New reports and duration choices update metadata without stretching an existing scroll
        previous_.opening = request.opening; previous_.durationMilliseconds = request.durationMilliseconds;
        // A new source identity begins a new observation interval without retaining borrowed receipt data
        if (previous_.navigationRevision != request.navigationRevision || previous_.topology != request.topology
            || previous_.openingPolicyIdentity != request.openingPolicyIdentity)
            sourceFirstObserved_ = nowMilliseconds;
        previous_.navigationRevision = request.navigationRevision; previous_.topology = request.topology;
        previous_.openingPolicyIdentity = request.openingPolicyIdentity;
        auto frame = Current(nowMilliseconds); moving_ = frame.transitioning;
        previousClock_ = nowMilliseconds; return frame;
    }
    const bool shortWrap = next.rowCount <= next.shown && next.rowCount > 1
        && ((previous_.selected == 0 && next.selected == next.rowCount - 1)
            || (next.selected == 0 && previous_.selected == next.rowCount - 1));
    if ((!shortWrap && Distance(next.selected, previous_.selected) != 1)
        || Distance(next.first, previous_.first) > 1) {
        // Source long wraps and missing intermediate windows rebuild their HUDs immediately
        Snap(request, nowMilliseconds); return next;
    }
    auto from = next, target = next;
    from.scroller = displayed.scroller;
    for (std::size_t row = 0; row < next.count; ++row) {
        if (const auto* old = Find(displayed, next.rows[row].index)) {
            from.rows[row] = *old; from.rows[row].outgoing = false;
        } else {
            // New edge rows fade in at their destination rather than sliding in from outside
            from.rows[row].visibility = 0; from.rows[row].opacity = 0; from.rows[row].decorationVisibility = 0;
        }
    }
    for (std::size_t row = 0; row < displayed.count; ++row) {
        const auto& old = displayed.rows[row];
        if (Find(next, old.index)) continue;
        if (target.count == MaximumScrollRows) {
            // Rapid retargets cannot grow an unbounded list of retiring row identities
            Snap(request, nowMilliseconds); return next;
        }
        from.rows[target.count] = target.rows[target.count] = old;
        from.rows[target.count].outgoing = target.rows[target.count].outgoing = true;
        // Source outgoing rows keep their position and selection style while fading away
        target.rows[target.count].visibility = 0; target.rows[target.count].opacity = 0; target.rows[target.count].decorationVisibility = 0; ++target.count; from.count = target.count;
    }
    from_ = from; target_ = target; previous_ = request; previous_.spacerReceipt = nullptr;
    // Validation admits only the source duration range and ten-millisecond steps
    duration_ = static_cast<unsigned>(request.durationMilliseconds);
    started_ = previousClock_ = sourceFirstObserved_ = nowMilliseconds; moving_ = true;
    return Current(nowMilliseconds);
}
void ScrollPresentation::Reset() noexcept {
    // Renderer teardown clears every old context and transition without touching menu data
    *this = ScrollPresentation{};
}
}
