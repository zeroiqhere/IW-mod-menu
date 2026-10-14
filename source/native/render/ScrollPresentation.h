#pragma once
#include "ScrollTypes.h"
#include "opening/OpeningPresentationClock.h"
#include "spacer/SpacerScrollPresentation.h"

namespace iw::overlay {
class ScrollPresentation final {
public:
    ScrollFrame Update(const ScrollRequest& request, std::uint64_t nowMilliseconds) noexcept;
    void Reset() noexcept;
private:
    ScrollFrame Current(std::uint64_t nowMilliseconds) const noexcept;
    void Snap(const ScrollRequest& request, std::uint64_t nowMilliseconds) noexcept;
    spacer::Presentation spacer_;
    opening::PresentationClock openingClock_;
    ScrollRequest previous_;
    ScrollFrame from_, target_;
    std::uint64_t started_ = 0, previousClock_ = 0, sourceFirstObserved_ = 0;
    unsigned duration_ = 120;
    bool initialized_ = false, moving_ = false;
};
}
