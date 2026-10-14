#pragma once
#include <optional>

namespace iw::overlay::runtime {
enum class Input;
// Credits accept one command per fresh aggregate melee press
class MenuCreditsInput {
public:
    void Reset(bool held = false);
    std::optional<Input> Poll(bool held, bool pending, bool contextChanged);
private:
    bool previous_ = false;
    bool blockedUntilRelease_ = false;
};
}
