#include "QuickExitGesture.h"
#include "NativeInputRouter.h"

namespace iw::overlay::runtime {
void QuickExitGesture::Reset(bool held) {
    active_ = false;
    blockedUntilRelease_ = held;
}
QuickExitResult QuickExitGesture::Poll(bool held, bool pressed, bool enabled,
    bool root, bool contextChanged, bool pending, std::uint64_t now) {
    if (contextChanged || pending) {
        // Changed navigation and unacknowledged work cannot retain an old gesture
        const bool consumed = active_ || held;
        active_ = false;
        blockedUntilRelease_ = held;
        return {consumed, std::nullopt};
    }
    if (blockedUntilRelease_) {
        // A completed or cancelled hold requires release before another action
        if (!held) blockedUntilRelease_ = false;
        return {true, std::nullopt};
    }
    if (active_) {
        if (now < started_) {
            // Clock rollback discards the gesture rather than underflowing elapsed time
            active_ = false;
            blockedUntilRelease_ = held;
            return {true, std::nullopt};
        }
        if (!held) {
            // A release cannot prove the knife was still held at an unobserved threshold
            active_ = false;
            return {true, Input::Back};
        }
        // Ten immediate source counts have nine 10 ms waits between them
        if (now - started_ >= 90) {
            active_ = false;
            blockedUntilRelease_ = true;
            return {true, Input::Close};
        }
        return {true, std::nullopt};
    }
    if (!pressed) return {};
    if (!enabled || root) return {true, Input::Back};
    active_ = true;
    started_ = now;
    return {true, std::nullopt};
}
}
