#include "WidthAnimation.h"
#include <algorithm>

namespace iw::overlay {
float WidthAnimation::Current(std::uint64_t nowMilliseconds) const noexcept {
    // A rollback is handled before this sample; no elapsed subtraction crosses a wrap
    if (nowMilliseconds < started_) return target_;
    const auto elapsed = nowMilliseconds - started_;
    if (elapsed >= DurationMilliseconds) return target_;
    const float progress = static_cast<float>(elapsed) / DurationMilliseconds;
    // Linear mapping matches the source duration; the engine easing itself is unverified
    return std::clamp(from_ + (target_ - from_) * progress, 200.0f, 500.0f);
}
void WidthAnimation::Snap(float width, bool active, std::uint64_t revision, std::uint64_t now) noexcept {
    active_ = active; revision_ = revision;
    from_ = width; target_ = width; started_ = now; previous_ = now;
}
float WidthAnimation::Update(int committedWidth, bool menuOpen, bool editor,
    std::uint64_t navigationRevision, std::uint64_t nowMilliseconds) noexcept {
    const float target = static_cast<float>(std::clamp(committedWidth, 200, 500));
    const bool active = menuOpen && editor;
    if (!active || !active_ || navigationRevision != revision_ || nowMilliseconds < previous_) {
        // New contexts and closed menus never inherit a previous editor's movement
        Snap(target, active, navigationRevision, nowMilliseconds);
        return target;
    }
    const float displayed = Current(nowMilliseconds);
    if (target != target_) {
        // A reversal starts at the currently displayed edge, not the old committed target
        from_ = displayed; target_ = target; started_ = nowMilliseconds;
    }
    previous_ = nowMilliseconds;
    return Current(nowMilliseconds);
}
void WidthAnimation::Reset() noexcept {
    // Device teardown and close clear the context before the next editor is shown
    *this = WidthAnimation{};
}
}
