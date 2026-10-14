#include "MenuCreditsInput.h"
#include "NativeInputRouter.h"

namespace iw::overlay::runtime {
void MenuCreditsInput::Reset(bool held) {
    previous_ = held;
    blockedUntilRelease_ = held;
}
std::optional<Input> MenuCreditsInput::Poll(bool held, bool pending, bool contextChanged) {
    if (contextChanged) {
        // A replacement visit cannot inherit a knife hold
        Reset(held);
        return std::nullopt;
    }
    const bool pressed = held && !previous_;
    previous_ = held;
    if (!held) {
        blockedUntilRelease_ = false;
        return std::nullopt;
    }
    if (pending) {
        // An observed pending hold remains consumed until every saved melee key is released
        blockedUntilRelease_ = true;
        return std::nullopt;
    }
    if (blockedUntilRelease_ || !pressed) return std::nullopt;
    // Release rearms a deliberate retry when an acknowledged exit was rejected by the owner
    return Input::FinishMenuCredits;
}
}
