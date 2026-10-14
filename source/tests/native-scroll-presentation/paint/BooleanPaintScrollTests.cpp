#include "BooleanPaintScrollTests.h"
#include "../../../native/render/ScrollPresentation.h"
#include <stdexcept>

void AddBooleanPaintScrollChecks(std::vector<std::pair<std::string, std::function<void()>>>& checks) {
    checks.emplace_back("Cached paint identity survives heartbeat and retires motion or opening lead on owner change", [] {
        using namespace iw::overlay;
        const auto require = [](bool condition, const char* reason) { if (!condition) throw std::runtime_error(reason); };
        ScrollRequest request; request.active = true; request.menuRevision = 6; request.focusRevision = 8;
        request.rowCount = 8; request.shown = 8; request.layout = {640, 480, 260, 144, 10};
        MenuBooleanPaint paint; paint.revision = 7; paint.count = 1; paint.rows[0] = {0, MenuBooleanPaintValue::On};
        request.menuBooleanPaint = paint; ScrollPresentation owner;
        owner.Update(request, 0); request.selected = 1;
        require(owner.Update(request, 10).transitioning, "Cached paint blocked ordinary cursor interpolation");
        require(owner.Update(request, 40).menuBooleanPaint == request.menuBooleanPaint, "Heartbeat lost paint identity");
        ++request.menuBooleanPaint->revision;
        require(!owner.Update(request, 50).transitioning, "New paint owner reused old cursor history");
        request.active = false; request.opening = MenuOpening{2, 100}; owner.Reset(); owner.Update(request, 100);
        require(owner.Update(request, 150).openingElapsed == 150, "Paint heartbeat restarted normal opening lead");
        request.menuBooleanPaint->rows[0].value = MenuBooleanPaintValue::Off;
        require(owner.Update(request, 160).openingElapsed == 100, "Changed paint borrowed a prior opening sample");
        request.menuBooleanPaint->count = 129;
        require(!owner.Update(request, 170).transitioning, "Malformed paint entered animation history");
    });
}
