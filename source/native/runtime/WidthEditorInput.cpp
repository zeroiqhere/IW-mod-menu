#include "WidthEditorInput.h"
#include "NativeInputRouter.h"
#include <algorithm>

namespace iw::overlay::runtime {
namespace {
std::array<bool, 5> Groups(WidthEditorKeys keys) {
    return {keys.attack, keys.aim, keys.decreaseStep, keys.increaseStep, keys.melee};
}
bool Any(const std::array<bool, 5>& groups) {
    return std::any_of(groups.begin(), groups.end(), [](bool held) { return held; });
}
}
void WidthEditorInput::Reset(WidthEditorKeys held) {
    previous_ = Groups(held);
    blockedUntilRelease_ = Any(previous_);
    repeating_.reset();
}
std::optional<Input> WidthEditorInput::Poll(WidthEditorKeys held, bool pending,
    bool contextChanged, std::uint64_t now) {
    const auto current = Groups(held);
    std::array<bool, 5> pressed{};
    for (std::size_t index = 0; index < current.size(); ++index) {
        pressed[index] = current[index] && !previous_[index];
    }
    previous_ = current;
    if (contextChanged) {
        // A held action cannot follow a replacement editor or navigation context
        Reset(held);
        return std::nullopt;
    }
    if (!Any(current)) {
        blockedUntilRelease_ = false;
        repeating_.reset();
        return std::nullopt;
    }
    if (blockedUntilRelease_) return std::nullopt;
    // The reference checks attack, aim, slot 3, slot 4, then melee in this order
    const auto found = std::find(current.begin(), current.end(), true);
    const auto group = static_cast<std::size_t>(found - current.begin());
    constexpr std::array actions{Input::IncreaseWidth, Input::DecreaseWidth,
        Input::DecreaseWidthStep, Input::IncreaseWidthStep, Input::FinishWidthEditor};
    const auto action = actions[group];
    if (repeating_ != action) repeating_.reset();
    if (pending) {
        // Fresh edges are consumed, while an existing hold resumes after a full interval
        if (repeating_) last_ = now;
        return std::nullopt;
    }
    if (action == Input::FinishWidthEditor) {
        return pressed[group] ? std::optional<Input>(action) : std::nullopt;
    }
    if (repeating_ && now < last_) {
        // Rollback disarms the action before unsigned elapsed subtraction
        Reset(held);
        return std::nullopt;
    }
    if (pressed[group]) {
        repeating_ = action;
        last_ = now;
        return action;
    }
    // Resize waits 50+25 ms; offset changes wait 100+25 ms in the source editor
    const std::uint64_t interval = group < 2 ? 75 : 125;
    if (repeating_ && now - last_ >= interval) {
        last_ = now;
        return repeating_;
    }
    return std::nullopt;
}
}
