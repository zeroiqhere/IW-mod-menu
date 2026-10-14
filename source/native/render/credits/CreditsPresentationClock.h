#pragma once
#include <cstdint>
#include <optional>

namespace iw::overlay::credits {
class PresentationClock final {
public:
    // The report owns timing; local interpolation is limited to one hundred milliseconds ahead
    std::optional<double> Update(int reportedMilliseconds, std::optional<std::uint64_t> menuRevision,
        std::optional<std::uint64_t> focusRevision, std::uint64_t nowMilliseconds) noexcept;
    void Reset() noexcept;
private:
    double Current(std::uint64_t nowMilliseconds) const noexcept;
    void Snap(int reported, std::uint64_t menu, std::uint64_t focus, std::uint64_t now) noexcept;
    double anchorElapsed_ = 0;
    int reported_ = 0;
    std::uint64_t menu_ = 0, focus_ = 0, anchorClock_ = 0, previousClock_ = 0;
    bool initialized_ = false;
};
}
