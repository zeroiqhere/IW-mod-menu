#include "ChoiceRepeat.h"
#include "NativeInputRouter.h"

namespace iw::overlay::runtime {
void ChoiceRepeat::Reset() {
    repeating_.reset();
    blockedUntilRelease_ = false;
}
void ChoiceRepeat::Interrupt(bool held) {
    repeating_.reset();
    blockedUntilRelease_ = held;
}
void ChoiceRepeat::Observe(bool previousHeld, bool nextHeld, bool contextChanged) {
    const bool held = previousHeld || nextHeld;
    if (contextChanged) Interrupt(held);
    if (!held) blockedUntilRelease_ = false;
    const auto direction = previousHeld != nextHeld
        ? std::optional<Input>(previousHeld ? Input::PreviousChoice : Input::NextChoice)
        : std::nullopt;
    // Opposite keys and releases cancel the previous repeat without inventing a new edge
    if (repeating_ != direction) repeating_.reset();
}
void ChoiceRepeat::Pause(std::uint64_t now) {
    if (repeating_) last_ = now;
}
std::optional<Input> ChoiceRepeat::Poll(bool previousHeld, bool nextHeld,
    bool previousPressed, bool nextPressed, std::uint64_t now) {
    if (blockedUntilRelease_ || previousHeld == nextHeld) return std::nullopt;
    const auto direction = previousHeld ? Input::PreviousChoice : Input::NextChoice;
    if (repeating_ && now < last_) {
        // A clock rollback cannot turn into a delayed adjustment on another poll
        Interrupt(true);
        return std::nullopt;
    }
    if (previousHeld ? previousPressed : nextPressed) {
        repeating_ = direction;
        last_ = now;
        return direction;
    }
    // The source choice loop waits 130 ms and then its normal 50 ms poll interval
    if (repeating_ && now - last_ >= 180) {
        last_ = now;
        return repeating_;
    }
    return std::nullopt;
}
}
