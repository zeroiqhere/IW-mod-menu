#include "OpeningPresentationClock.h"
#include "../../shared/MenuRowCapacity.h"
#include <algorithm>

namespace iw::overlay::opening {
double PresentationClock::Sample(std::uint64_t now) const noexcept {
    if (!initialized_ || !interpolating_ || now < anchorClock_ || now < previousClock_) return reported_;
    // Bound the unsigned difference before conversion, even after a very long idle period
    const auto delta = std::min<std::uint64_t>(now - anchorClock_, 100);
    return std::min({anchorElapsed_ + static_cast<double>(delta), reported_ + 100.0,
        static_cast<double>(MaximumMenuOpeningMilliseconds)});
}
void PresentationClock::Snap(int report, std::uint64_t event, std::uint64_t menu, std::uint64_t focus,
    std::uint64_t now) noexcept {
    reported_ = report; anchorElapsed_ = report; event_ = event; menu_ = menu; focus_ = focus;
    anchorClock_ = previousClock_ = now; initialized_ = true; interpolating_ = true;
}
double PresentationClock::Observe(int report, std::uint64_t event, std::optional<std::uint64_t> menu,
    std::optional<std::uint64_t> focus, std::uint64_t now) noexcept {
    if (!event || report < 0 || report > MaximumMenuOpeningMilliseconds) { Reset(); return 0; }
    if (!menu || !focus) {
        // Missing private identity keeps legacy rendering exactly at the owner report
        Reset(); reported_ = report; initialized_ = true; return report;
    }
    if (!initialized_ || !interpolating_ || event != event_ || *menu != menu_ || *focus != focus_
        || report < reported_ || now < previousClock_) {
        Snap(report, event, *menu, *focus, now); return report;
    }
    const double displayed = Sample(now);
    if (report != reported_) {
        // New reports cannot rewind a phase already shown within its permitted local lead
        anchorElapsed_ = std::max(static_cast<double>(report), displayed);
        reported_ = report; anchorClock_ = now;
    }
    previousClock_ = now; return Sample(now);
}
void PresentationClock::Reset() noexcept {
    // Closing or changing the owner context removes all previous timing
    *this = PresentationClock{};
}
}
