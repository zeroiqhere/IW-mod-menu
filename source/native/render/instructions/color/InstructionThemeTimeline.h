#pragma once
#include "InstructionHudColors.h"
#include "../../../shared/Instructions/InstructionThemeState.h"
#include <array>
namespace iw::overlay {
class InstructionThemeTimeline final {
public:
    std::optional<InstructionHudColors> Update(const InstructionThemeState& state,
        InstructionHudColors seed,std::optional<InstructionThemeTarget> observedRainbowTarget,std::uint64_t nowMilliseconds) noexcept;
    bool IsQuarantined() const noexcept { return quarantined_; }
    void Override(InstructionThemeTarget target,layout::Color sample) noexcept;
private:
    using Rgb=std::array<double,3>;
    struct Channel {
        Rgb held{},from{},target{};
        std::uint8_t alpha=255;
        bool known=true,active=false;
        std::uint64_t age=0,displayedAge=0;
    };
    static Rgb At(const Channel& channel,std::uint64_t elapsed) noexcept;
    static layout::Color Draw(Channel& channel,std::uint64_t lead) noexcept;
    std::optional<std::uint64_t> creation_;
    std::array<Channel,2> channels_{};
    std::array<InstructionThemeEvent,8> previous_{};
    std::size_t count_=0;
    std::uint64_t highest_=0,reportClock_=0,lastClock_=0;
    bool prefixComplete_=true,quarantined_=false;
};
}
