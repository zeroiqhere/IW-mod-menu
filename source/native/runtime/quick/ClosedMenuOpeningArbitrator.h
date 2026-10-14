#pragma once
#include "../../shared/QuickMenu/ClosedMenuOpening.h"
#include "../NativeInputRouter.h"

namespace iw::overlay::runtime::quick {
// One input owner polls serially with immutable observed facts and a read-only key callback
// Source levels and the legacy fresh-edge adapter share current opening permissions
// Configured Quick requires verified groups; Main-only mode retains only the dead Aim+Jump fallback
class ClosedMenuOpeningArbitrator final {
public:
    explicit ClosedMenuOpeningArbitrator(Bindings bindings);
    std::optional<ClosedMenuOpeningIntent> Poll(const ClosedMenuOpeningContext& context,
                                                const ClosedMenuOpeningPolicies& policies,
                                                const std::function<bool(int)>& isDown,
                                                bool configuredQuickAvailable = true,
                                                ClosedOpeningPolling polling = ClosedOpeningPolling::FreshEdges);
    void Reset(bool consumeSourceHolds = true) noexcept;

private:
    Bindings bindings_;
    std::vector<int> keys_;
    std::optional<ClosedMenuOpeningContext> previousContext_;
    std::optional<ClosedMenuOpeningPolicies> previousPolicies_;
    bool mainHeld_ = false;
    bool quickHeld_ = false;
    bool deadHeld_ = false;
    bool previousConfiguredQuickAvailable_ = true;
    // Focus or validation loss requires a neutral chord before current levels resume
    bool sourceReleaseRequired_ = false;
};
} // namespace iw::overlay::runtime::quick
