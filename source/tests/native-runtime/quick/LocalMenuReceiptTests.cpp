#include "../scroll/SpacerScrollTestModels.h"

namespace iw::overlay::tests {
void LocalAdmissionReceiptsRetireEveryChangedFactAndFamilyBeforePromotion() {
    auto source = spacer::Source();
    source.localMenuEligibility = LocalMenuEligibility{7, PlayerLifeState::Alive, true, false, false};
    for (int change = 0; change < 7; ++change) {
        runtime::scroll::SpacerScrollReceiptTracker tracker;
        Check(tracker.Capture(source, SpacerScrollDirection::Down, 11, 100), "Valid admission capture failed");
        auto target = spacer::Target(source, 3);
        if (change == 0) ++target.localMenuEligibility->revision;
        if (change == 1) target.localMenuEligibility->life = PlayerLifeState::Dead;
        if (change == 2) target.localMenuEligibility->hasMenu = std::nullopt;
        if (change == 3) target.localMenuEligibility->controlsDisabled = true;
        if (change == 4) target.localMenuEligibility->blocked = true;
        if (change == 5) target.localMenuEligibility.reset();
        if (change == 6) target.menuSurface = MenuSurface::Quick;
        Check(!tracker.Observe(target, 11, 120), "Receipt retained foreign family or local admission history");
    }
    runtime::scroll::SpacerScrollReceiptTracker tracker;
    Check(tracker.Capture(source, SpacerScrollDirection::Down, 11, 100), "Valid source capture failed");
    const auto receipt = tracker.Observe(spacer::Target(source, 3), 11, 120);
    Check(receipt && receipt->sourceLocalMenuEligibility == source.localMenuEligibility
        && receipt->sourceMenuSurface == source.menuSurface, "Receipt lost its private admission source");
    source.localMenuEligibility->revision = 0;
    Check(!tracker.Capture(source, SpacerScrollDirection::Down, 12, 200), "Malformed admission entered private history");
}
}
