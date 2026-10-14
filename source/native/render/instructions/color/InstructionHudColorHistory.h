#pragma once
#include "../InstructionHudPresence.h"
#include "InstructionHudColors.h"
#include "InstructionThemeTimeline.h"
namespace iw::overlay {
class InstructionHudColorHistory final {
public:
    std::optional<InstructionHudColors> Update(const MenuSnapshot& snapshot, bool admitted,
        std::optional<layout::Color> nativeSample = std::nullopt, std::uint64_t nowMilliseconds = 0) noexcept;
    bool IsQuarantined() const noexcept { return quarantined_ || themeTimeline_.IsQuarantined(); }
private:
    std::optional<std::uint64_t> revision_;
    std::optional<InstructionRgb> lastAccent_;
    std::optional<InstructionHudColors> colors_;
    InstructionThemeTimeline themeTimeline_;
    bool themeAuthority_=false,quarantined_=false;
    std::optional<InstructionThemeTarget> observedRainbowTarget_;
};
}
