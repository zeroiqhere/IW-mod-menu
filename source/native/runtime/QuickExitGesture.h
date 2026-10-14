#pragma once
#include <cstdint>
#include <optional>

namespace iw::overlay::runtime {
enum class Input;

// A nested knife gesture resolves to one page back or one local menu close
struct QuickExitResult {
    bool consumesInput = false;
    std::optional<Input> command;
};

class QuickExitGesture {
public:
    void Reset(bool held = false);
    // A started source knife loop keeps control until release or its hold threshold
    bool HasActiveHold() const noexcept { return active_; }
    QuickExitResult Poll(bool held, bool pressed, bool enabled, bool root,
        bool contextChanged, bool pending, std::uint64_t now);
private:
    bool active_ = false;
    bool blockedUntilRelease_ = false;
    std::uint64_t started_ = 0;
};
}
