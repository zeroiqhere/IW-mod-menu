#include "../../native/shared/OverlayProtocol.h"

#include <mutex>

namespace iw::overlay {
namespace {
std::mutex modelMutex;
std::shared_ptr<const MenuSnapshot> model;
std::shared_ptr<const MenuSnapshot> savedModel;
}

bool RuntimeStart(const BootstrapConfig&, std::string&) {
    auto snapshot = std::make_shared<MenuSnapshot>();
    snapshot->isOpen = true;
    snapshot->title = "Fixture menu";
    snapshot->context = "Standalone D3D11 test";
    snapshot->notice = "No game features are connected";
    snapshot->rows = {{"Godmode", "UNKNOWN", false}, {"Ammo", "UNKNOWN", false}, {"Customization", ">", true}};
    std::lock_guard lock(modelMutex);
    model = snapshot;
    return true;
}

std::shared_ptr<const MenuSnapshot> GetOverlaySnapshot() {
    std::lock_guard lock(modelMutex);
    return model;
}

void RuntimeStop() {
    std::lock_guard lock(modelMutex);
    model.reset();
    savedModel.reset();
}

}

extern "C" __declspec(dllexport) void WINAPI SetFixtureMenuOpen(BOOL open) {
    std::lock_guard lock(iw::overlay::modelMutex);
    if (iw::overlay::model) {
        // A test changes only an immutable drawing model, never a game backend
        auto replacement = std::make_shared<iw::overlay::MenuSnapshot>(*iw::overlay::model);
        replacement->isOpen = open != FALSE;
        replacement->accent = "#46DBB1";
        iw::overlay::model = replacement;
    }
}

extern "C" __declspec(dllexport) void WINAPI SetFixtureModelUnavailable(BOOL unavailable) {
    std::lock_guard lock(iw::overlay::modelMutex);
    // A missing producer model is tested without stopping hooks or connecting a game backend
    if (unavailable) {
        iw::overlay::savedModel = iw::overlay::model;
        iw::overlay::model.reset();
    } else {
        iw::overlay::model = iw::overlay::savedModel;
        iw::overlay::savedModel.reset();
    }
}
