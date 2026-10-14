#pragma once
#include <array>
#include <cstdint>
#include <optional>

namespace iw::overlay::runtime {
enum class Input;

// Editor actions use separate game actions rather than combined scroll bindings
struct WidthEditorKeys {
    bool attack = false, aim = false, decreaseStep = false, increaseStep = false, melee = false;
};

class WidthEditorInput {
public:
    void Reset(WidthEditorKeys held = {});
    std::optional<Input> Poll(WidthEditorKeys held, bool pending, bool contextChanged,
        std::uint64_t now);
private:
    std::array<bool, 5> previous_{};
    bool blockedUntilRelease_ = false;
    std::optional<Input> repeating_;
    std::uint64_t last_ = 0;
};
}
