#pragma once
#include "ChromeColorFrame.h"
#include "../../../shared/MenuSnapshot.h"
namespace iw::overlay {
// Each owner retains bounded floating segments across graphics resets and focus withdrawal
class ChromeColorHistory final {
public:
    std::optional<ChromeColorFrame> Update(const MenuSnapshot& snapshot,
        std::optional<layout::Color> nativeRainbow, std::uint64_t nowMilliseconds) noexcept;
private:
    using Rgb = std::array<double, 3>;
    struct Channel {
        Rgb held{}, from{}, target{};
        std::uint8_t alpha = 255;
        bool known = true, active = false;
        std::uint64_t age = 0, displayedAge = 0;
    };
    static Rgb At(const Channel& channel, std::uint64_t age) noexcept;
    static layout::Color Draw(Channel& channel, std::uint64_t lead) noexcept;
    std::optional<std::uint64_t> creation_;
    MenuDesign design_ = MenuDesign::Apparition;
    ChromeRgb accent_{};
    bool creationRainbow_ = false, owned_ = false, retired_ = false, prefixComplete_ = true;
    unsigned roleMask_ = 0, observedRainbowMask_ = 0;
    std::array<Channel, 5> channels_{};
    std::array<ChromeThemeEvent, 8> previous_{};
    std::size_t count_ = 0;
    std::uint64_t highest_ = 0, reportClock_ = 0, lastClock_ = 0;
};
}
