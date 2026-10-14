#include "LocalMenuEligibilityScrollTests.h"
#include "../../../native/render/ScrollPresentation.h"
#include <stdexcept>

namespace {
using namespace iw::overlay;
void Require(bool value, const char* message) {
    if (!value) throw std::runtime_error(message);
}
ScrollRequest Source() {
    ScrollRequest request;
    request.active = true; request.menuRevision = 6; request.focusRevision = 8;
    request.navigationRevision = 10; request.rowCount = 14; request.shown = 10;
    request.selected = 1; request.layout = {640, 480, 260, 180, 10};
    request.localMenuEligibility = LocalMenuEligibility{7, PlayerLifeState::Alive, true, false, false};
    return request;
}
}
void AddLocalMenuEligibilityScrollChecks(std::vector<std::pair<std::string, std::function<void()>>>& checks) {
    checks.emplace_back("Family and actor admission changes cannot carry old cursor weights", [] {
        for (int change = 0; change < 3; ++change) {
            ScrollPresentation owner; auto source = Source(); owner.Update(source, 0);
            auto target = source; target.selected = 2;
            if (change == 0) target.menuSurface = MenuSurface::Quick;
            if (change == 1) ++target.localMenuEligibility->revision;
            if (change == 2) target.localMenuEligibility->hasMenu = std::nullopt;
            const auto frame = owner.Update(target, 40);
            Require(!frame.transitioning && frame.localMenuEligibility == target.localMenuEligibility
                && frame.menuSurface == target.menuSurface, "Foreign admission inherited the source cursor");
        }
    });
    checks.emplace_back("Unchanged local admission preserves opening age and its frame echo", [] {
        ScrollPresentation owner; auto source = Source(); source.opening = MenuOpening{1, 100};
        owner.Update(source, 0); const auto frame = owner.Update(source, 50);
        Require(frame.openingElapsed == 150 && frame.localMenuEligibility == source.localMenuEligibility,
            "Unchanged admission restarted an opening or lost its frame identity");
        auto changed = source; ++changed.localMenuEligibility->revision;
        Require(owner.Update(changed, 60).openingElapsed == 100,
            "Changed admission borrowed an older actor's opening interpolation");
    });
}
