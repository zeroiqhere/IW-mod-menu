#include "CallbackScrollTests.h"
#include "../../../native/render/ScrollPresentation.h"
#include <stdexcept>

namespace {
using namespace iw::overlay;
void Require(bool condition, const char* message) {
    if (!condition) throw std::runtime_error(message);
}
ScrollRequest Source() {
    ScrollRequest request;
    request.menuRevision = 6; request.focusRevision = 8;
    request.navigationRevision = 12; request.rowCount = 14; request.shown = 10;
    request.selected = 1; request.layout = {640, 480, 260, 180, 10};
    request.menuOptionCallback = MenuOptionCallback{7, MenuOptionCallbackKind::Boolean, MenuOptionCallbackPhase::Pending};
    return request;
}
}
void AddCallbackScrollChecks(std::vector<std::pair<std::string, std::function<void()>>>& checks) {
    checks.emplace_back("Callback frames echo exact owner kind and phase through heartbeat and completion", [] {
        ScrollPresentation owner; auto request = Source();
        for (int change = 0; change < 5; ++change) {
            if (change == 1) ++request.menuOptionCallback->revision;
            if (change == 2) request.menuOptionCallback->phase = MenuOptionCallbackPhase::Cooldown;
            if (change == 3) request.menuOptionCallback->kind = MenuOptionCallbackKind::Plain;
            if (change == 4) request.menuOptionCallback.reset();
            const auto frame = owner.Update(request, static_cast<std::uint64_t>(change * 40));
            Require(frame.menuOptionCallback == request.menuOptionCallback && !frame.transitioning,
                "A published frame lost its exact callback scope");
        }
    });
    checks.emplace_back("Unchanged callback preserves existing opening age while changed ownership resets private lead", [] {
        for (bool plain : {false, true}) {
            ScrollPresentation owner; auto source = Source();
            if (plain) source.menuOptionCallback = MenuOptionCallback{7, MenuOptionCallbackKind::Plain, MenuOptionCallbackPhase::Cooldown};
            source.opening = MenuOpening{2, 100}; owner.Update(source, 0);
            Require(owner.Update(source, 50).openingElapsed == 150, "Callback heartbeat restarted an existing opening");
            auto changed = source; ++changed.menuOptionCallback->revision;
            Require(owner.Update(changed, 60).openingElapsed == 100, "New callback borrowed previous private opening age");
        }
    });
}
