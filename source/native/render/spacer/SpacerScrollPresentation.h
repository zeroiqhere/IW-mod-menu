#pragma once
#include "../ScrollTypes.h"

namespace iw::overlay::spacer {
// One bounded event owns source paint without retaining snapshot text or borrowed pointers
class Presentation final {
public:
    bool Admit(const ScrollRequest& source, const ScrollRequest& target,
        const ScrollFrame& displayed, std::uint64_t sourceObserved, std::uint64_t now) noexcept;
    bool Matches(const ScrollRequest& request, std::uint64_t now) const noexcept;
    ScrollFrame Sample(const ScrollRequest& request, std::uint64_t now) noexcept;
    bool Active() const noexcept { return active_; }
    void Reset() noexcept { active_ = false; }
private:
    SpacerScrollPlan plan_;
    ScrollFrame source_;
    std::uint64_t sequence_ = 0, issued_ = 0, observed_ = 0, sampled_ = 0;
    bool active_ = false;
};
// Supplied frames must repeat the current receipt and its bounded phase before overriding a counter
bool MatchesReceiptFrame(const ScrollFrame& frame, const ScrollRequest& request) noexcept;
}
