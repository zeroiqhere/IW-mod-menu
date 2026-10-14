#include "InstructionPositionInput.h"
#include "NativeInputRouter.h"
#include <algorithm>

namespace iw::overlay::runtime {
namespace {
std::array<bool, 5> Groups(InstructionPositionKeys keys) {
    return {keys.up, keys.down, keys.left, keys.right, keys.melee};
}
bool Any(const std::array<bool, 5>& groups) {
    return std::any_of(groups.begin(), groups.end(), [](bool held) { return held; });
}
}
void InstructionPositionInput::Reset(InstructionPositionKeys held) {
    previous_ = Groups(held);
    eligible_.fill(false);
    blockedUntilRelease_ = Any(previous_);
    repeating_ = false;
}
std::optional<Input> InstructionPositionInput::Poll(InstructionPositionKeys held, bool pending,
    bool contextChanged, std::uint64_t now) {
    const auto current = Groups(held);
    std::array<bool, 5> pressed{};
    for (std::size_t index = 0; index < current.size(); ++index) {
        pressed[index] = current[index] && !previous_[index];
    }
    previous_ = current;
    if (contextChanged || (repeating_ && now < last_)) {
        // Replacement contexts and clock rollback discard every inherited action hold
        Reset(held);
        return std::nullopt;
    }
    if (!Any(current)) {
        blockedUntilRelease_ = false;
        eligible_.fill(false);
        repeating_ = false;
        return std::nullopt;
    }
    if (blockedUntilRelease_) return std::nullopt;
    for (std::size_t index = 0; index < current.size(); ++index) {
        if (!current[index]) eligible_[index] = false;
        else if (pressed[index]) eligible_[index] = !pending;
    }
    if (pending) {
        // Fresh pending edges remain consumed; established holds wait a complete interval
        if (repeating_) last_ = now;
        return std::nullopt;
    }
    // The source checks vertical movement first, with Down and Right winning conflicts
    const std::size_t group = current[1] ? 1 : current[0] ? 0
        : current[3] ? 3 : current[2] ? 2 : 4;
    if (!eligible_[group]) return std::nullopt;
    constexpr std::array actions{Input::MoveInstructionsUp, Input::MoveInstructionsDown,
        Input::MoveInstructionsLeft, Input::MoveInstructionsRight, Input::FinishInstructionPositionEditor};
    if (group == 4) {
        // Melee exits once only when no movement group is held
        eligible_[group] = false;
        repeating_ = false;
        return actions[group];
    }
    // One shared 25ms movement clock preserves priority changes without catchup bursts
    if (!repeating_ || now - last_ >= 25) {
        repeating_ = true;
        last_ = now;
        return actions[group];
    }
    return std::nullopt;
}
}
