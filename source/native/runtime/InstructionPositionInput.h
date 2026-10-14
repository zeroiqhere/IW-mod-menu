#pragma once
#include <array>
#include <cstdint>
#include <optional>

namespace iw::overlay::runtime {
enum class Input;
struct InstructionPositionKeys {
    bool up = false, down = false, left = false, right = false, melee = false;
};

// Exact saved action groups control one source position editor at a time
class InstructionPositionInput {
public:
    void Reset(InstructionPositionKeys held = {});
    std::optional<Input> Poll(InstructionPositionKeys held, bool pending,
        bool contextChanged, std::uint64_t now);
private:
    std::array<bool, 5> previous_{};
    std::array<bool, 5> eligible_{};
    bool blockedUntilRelease_ = false;
    bool repeating_ = false;
    std::uint64_t last_ = 0;
};
}
