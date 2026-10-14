#include "CreditsPresentationClock.h"
#include <algorithm>

namespace iw::overlay::credits {
double PresentationClock::Current(std::uint64_t now) const noexcept {
    if (now < anchorClock_) return reported_;
    // Cap the integer delta before conversion, including the largest unsigned clock value
    const auto delta = std::min<std::uint64_t>(now - anchorClock_, 100);
    return std::min({anchorElapsed_ + static_cast<double>(delta), reported_ + 100.0, 39999.0});
}
void PresentationClock::Snap(int reported, std::uint64_t menu, std::uint64_t focus, std::uint64_t now) noexcept {
    reported_ = reported; anchorElapsed_ = reported; menu_ = menu; focus_ = focus;
    anchorClock_ = previousClock_ = now; initialized_ = true;
}
std::optional<double> PresentationClock::Update(int reported, std::optional<std::uint64_t> menu,
    std::optional<std::uint64_t> focus, std::uint64_t now) noexcept {
    if (reported < 0 || reported >= 40000) { Reset(); return std::nullopt; }
    if (!menu || !focus) {
        // Legacy producers retain exact reported timing rather than guessing a sequence identity
        Reset(); return reported;
    }
    if (!initialized_ || menu_ != *menu || focus_ != *focus || reported < reported_ || now < previousClock_) {
        Snap(reported, *menu, *focus, now); return reported;
    }
    const double displayed = Current(now);
    if (reported != reported_) {
        // Delayed increasing reports cannot rewind a phase already displayed within its allowed lead
        anchorElapsed_ = std::max(static_cast<double>(reported), displayed);
        reported_ = reported; anchorClock_ = now;
    }
    previousClock_ = now; return Current(now);
}
void PresentationClock::Reset() noexcept {
    // Close and renderer teardown remove the old local clock without changing backend state
    *this = PresentationClock{};
}
}
