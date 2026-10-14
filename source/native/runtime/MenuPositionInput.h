#pragma once
#include <array>
#include <cstdint>
#include <optional>

namespace iw::overlay::runtime {
enum class Input;
// These seven groups represent exact saved actions rather than normal menu aliases
struct MenuPositionKeys {
    bool up = false, down = false, left = false, right = false;
    bool increaseStep = false, decreaseStep = false, melee = false;
};
class MenuPositionInput {
public:
    void Reset(MenuPositionKeys held = {});
    std::optional<Input> Poll(MenuPositionKeys held, bool pending,
        bool contextChanged, std::uint64_t now);
private:
    std::array<bool, 7> previous_{};
    std::array<bool, 7> eligible_{};
    bool blockedUntilRelease_ = false;
    // A dispatched branch owns its wait even after every action is released
    bool cooldown_ = false;
    // Every observed poll participates in rollback detection, including idle polls
    std::optional<std::uint64_t> observedTime_ = std::nullopt;
    std::uint64_t last_ = 0;
    std::uint64_t interval_ = 25;
};
}
