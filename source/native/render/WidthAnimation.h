#pragma once
#include <cstdint>

namespace iw::overlay {
// Presentation state stays on the renderer thread and never changes committed menu values
class WidthAnimation final {
public:
    static constexpr std::uint64_t DurationMilliseconds = 50;
    float Update(int committedWidth, bool menuOpen, bool editor,
        std::uint64_t navigationRevision, std::uint64_t nowMilliseconds) noexcept;
    void Reset() noexcept;
private:
    float Current(std::uint64_t nowMilliseconds) const noexcept;
    void Snap(float width, bool active, std::uint64_t revision, std::uint64_t now) noexcept;
    bool active_ = false;
    float from_ = 260, target_ = 260;
    std::uint64_t revision_ = 0, started_ = 0, previous_ = 0;
};
}
