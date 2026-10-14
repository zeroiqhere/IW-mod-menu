#pragma once
#include <cstdint>
#include <optional>

namespace iw::overlay::opening {
// This clock smooths owner reports without claiming a shared process clock
class PresentationClock final {
public:
    double Observe(int reportedMilliseconds, std::uint64_t eventRevision,
        std::optional<std::uint64_t> menuRevision, std::optional<std::uint64_t> focusRevision,
        std::uint64_t nowMilliseconds) noexcept;
    double Sample(std::uint64_t nowMilliseconds) const noexcept;
    void Reset() noexcept;
private:
    void Snap(int report, std::uint64_t event, std::uint64_t menu, std::uint64_t focus, std::uint64_t now) noexcept;
    double anchorElapsed_ = 0;
    int reported_ = 0;
    std::uint64_t event_ = 0, menu_ = 0, focus_ = 0, anchorClock_ = 0, previousClock_ = 0;
    bool initialized_ = false, interpolating_ = false;
};
}
