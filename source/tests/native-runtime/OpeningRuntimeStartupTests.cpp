#include "RuntimeStartup.h"
#include "TestSupport.h"
namespace iw::overlay::tests {
void RuntimeStartupRejectsUnavailableActionsBeforeReturningPublishableData() {
    auto packet = StateJson();
    packet["mainOpeningControls"] = {{"revision", 1}, {"actions", {12}}};
    const auto bindings = BindingsJson();
    // The same preparation function used by RuntimeStart cannot return an invalid policy
    for (bool open : {false, true}) {
        packet["isOpen"] = open;
        Reject(
            [&] { runtime::PrepareRuntimeStartup(packet.dump(), bindings.dump(), GetCurrentProcessId()); });
    }
    packet["mainOpeningControls"]["actions"] = {4};
    packet["rows"][0]["openingAction"] = 4;
    const auto prepared =
        runtime::PrepareRuntimeStartup(packet.dump(), bindings.dump(), GetCurrentProcessId());
    Check(prepared.state.snapshot->mainOpeningControls == MainOpeningControls{1, {OpeningAction::Melee}} &&
              prepared.state.snapshot->rows[0].label == "V / Mouse 5" &&
              prepared.bindings.melee == std::vector<int>{86, 6},
          "Startup did not retain verified policy, private caption and immutable keys together");
    auto malformed = bindings;
    malformed["jump"] = nullptr;
    Reject([&] { runtime::PrepareRuntimeStartup(packet.dump(), malformed.dump(), GetCurrentProcessId()); });
}
} // namespace iw::overlay::tests
