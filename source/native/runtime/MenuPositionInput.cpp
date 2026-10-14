#include "MenuPositionInput.h"
#include "NativeInputRouter.h"
#include <algorithm>

namespace iw::overlay::runtime {
namespace {
std::array<bool, 7> Groups(MenuPositionKeys keys) {
    return {keys.up, keys.down, keys.left, keys.right, keys.increaseStep, keys.decreaseStep, keys.melee};
}
bool Any(const std::array<bool, 7>& groups) {
    return std::any_of(groups.begin(), groups.end(), [](bool held) { return held; });
}
}
void MenuPositionInput::Reset(MenuPositionKeys held) {
    previous_ = Groups(held);
    eligible_.fill(false);
    blockedUntilRelease_ = Any(previous_);
    cooldown_ = false;
    observedTime_.reset();
}
std::optional<Input> MenuPositionInput::Poll(MenuPositionKeys held, bool pending,
    bool contextChanged, std::uint64_t now) {
    const auto current = Groups(held);
    std::array<bool, 7> pressed{};
    for (std::size_t index = 0; index < current.size(); ++index) {
        pressed[index] = current[index] && !previous_[index];
    }
    previous_ = current;
    if (contextChanged || (observedTime_ && now < *observedTime_)) {
        // Context replacement and rollback discard every inherited hold before elapsed subtraction
        Reset(held);
        observedTime_ = now;
        return std::nullopt;
    }
    observedTime_ = now;
    if (!Any(current)) {
        blockedUntilRelease_ = false;
        eligible_.fill(false);
    }
    if (blockedUntilRelease_) return std::nullopt;
    const bool waiting = cooldown_ && now - last_ < interval_;
    for (std::size_t index = 0; index < current.size(); ++index) {
        if (!current[index]) eligible_[index] = false;
        else if (pressed[index]) {
            // Current held movement and offset groups follow the source loop at its deadline
            // Melee stays edge-driven so a wait-time press cannot become a delayed exit
            eligible_[index] = !pending && (!waiting || index < 6);
        }
    }
    if (pending) {
        // Acknowledgement delays an established hold while consuming every new edge
        if (cooldown_) last_ = now;
        return std::nullopt;
    }
    if (waiting) return std::nullopt;
    cooldown_ = false;
    if (!Any(current)) return std::nullopt;
    // Source order is Down, Up, Right, Left, smoke, frag, then melee
    const std::size_t group = current[1] ? 1 : current[0] ? 0 : current[3] ? 3 : current[2] ? 2
        : current[5] ? 5 : current[4] ? 4 : 6;
    if (!eligible_[group]) return std::nullopt;
    constexpr std::array actions{Input::MoveMenuUp, Input::MoveMenuDown, Input::MoveMenuLeft,
        Input::MoveMenuRight, Input::IncreaseMenuPositionStep, Input::DecreaseMenuPositionStep,
        Input::FinishMenuPositionEditor};
    if (group == 6) {
        // Held melee finishes once and cannot become an ordinary Back action
        eligible_[group] = false;
        return actions[group];
    }
    last_ = now;
    // Offset branches include their 100ms wait plus the shared 25ms loop, even at bounds
    interval_ = group < 4 ? 25 : 125;
    cooldown_ = true;
    return actions[group];
}
}
