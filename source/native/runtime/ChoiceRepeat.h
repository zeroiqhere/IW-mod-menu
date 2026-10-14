#pragma once
#include <cstdint>
#include <optional>

namespace iw::overlay::runtime {
enum class Input;

// A held choice belongs to the row where its fresh press was accepted
class ChoiceRepeat {
public:
    void Reset();
    void Observe(bool previousHeld, bool nextHeld, bool contextChanged);
    void Pause(std::uint64_t now);
    void Interrupt(bool held);
    std::optional<Input> Poll(bool previousHeld, bool nextHeld, bool previousPressed,
        bool nextPressed, std::uint64_t now);
private:
    // A moved cursor cannot inherit a choice until every choice key is released
    bool blockedUntilRelease_ = false;
    std::optional<Input> repeating_;
    std::uint64_t last_ = 0;
};
}
