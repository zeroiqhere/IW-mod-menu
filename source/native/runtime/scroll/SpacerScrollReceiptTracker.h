#pragma once
#include "SpacerScrollPlan.h"

namespace iw::overlay::runtime::scroll {
// One command can be pending at a time and no receipt contains old row values
class SpacerScrollReceiptTracker final {
  public:
    bool Capture(const MenuSnapshot &source, SpacerScrollDirection direction, std::uint64_t sequence,
                 std::uint64_t issuedMilliseconds);
    std::optional<SpacerScrollReceipt> Observe(const MenuSnapshot &confirmed,
                                               std::uint64_t acknowledgedSequence,
                                               std::uint64_t observedMilliseconds);
    void Reset() noexcept;

  private:
    std::optional<SpacerScrollReceipt> pending_;
    std::optional<std::uint64_t> previousClock_;
};
// This optional parser input belongs to the worker and is never read from JSON
struct SpacerScrollObservation {
    SpacerScrollReceiptTracker *tracker = nullptr;
    std::uint64_t observedMilliseconds = 0;
};
} // namespace iw::overlay::runtime::scroll
