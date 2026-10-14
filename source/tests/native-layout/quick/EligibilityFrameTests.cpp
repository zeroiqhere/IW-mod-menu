#include "EligibilityFrameTests.h"
#include "../../../native/render/ScrollRows.h"
#include <stdexcept>

namespace iw::overlay::tests {
void AddEligibilityFrameChecks(std::vector<std::pair<std::string, std::function<void()>>>& checks) {
    checks.emplace_back("Published scroll frames retain exact local admission and family identity", [] {
        const auto require = [](bool value, const char* message) {
            if (!value) throw std::runtime_error(message);
        };
        MenuSnapshot snapshot;
        snapshot.isOpen = true; snapshot.menuRevision = 6; snapshot.presentationFocusRevision = 8;
        snapshot.localMenuEligibility = LocalMenuEligibility{7, PlayerLifeState::Alive, true, false, false};
        snapshot.rows = {{"Row", "OFF", true}, {"Second", "ON", true}};
        auto request = MakeScrollRequest(snapshot, 640, 480);
        const auto hud = layout::MakeHud({640, 480, snapshot.rows.size(), 0, 260, 3, false, 0, snapshot.appearance});
        auto frame = StaticScrollFrame(request);
        require(MatchesScrollFrame(frame, hud, request), "Exact local admission frame was rejected");
        require(request.localMenuEligibility == snapshot.localMenuEligibility, "Runtime snapshot admission was lost");
        for (int change = 0; change < 3; ++change) {
            auto stale = frame;
            if (change == 0) ++stale.localMenuEligibility->revision;
            if (change == 1) stale.localMenuEligibility.reset();
            if (change == 2) stale.menuSurface = MenuSurface::Quick;
            require(!MatchesScrollFrame(stale, hud, request), "Foreign frame borrowed current actor admission");
        }
        // Even a direct caller cannot supply an unsupported family or zero revision
        request.menuSurface = static_cast<MenuSurface>(99);
        require(!MatchesScrollFrame(frame, hud, request), "Unsupported family was admitted");
        request.menuSurface = MenuSurface::Main; request.localMenuEligibility->revision = 0;
        frame = StaticScrollFrame(request);
        require(!MatchesScrollFrame(frame, hud, request), "Malformed private admission was admitted");
    });
}
}
