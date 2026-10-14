#include "../TestSupport.h"
#include <limits>

namespace iw::overlay::tests {
void ScopedMenuOpensRequireAllCapturedEpochsWithoutInventingValues() {
    for (const auto input : {runtime::Input::OpenMain, runtime::Input::OpenQuick}) {
        for (int missing = 0; missing < 3; ++missing) {
            Reject([&] { runtime::CommandJson(42, 7, input,
                missing == 0 ? std::nullopt : std::optional<std::uint64_t>(0),
                missing == 1 ? std::nullopt : std::optional<std::uint64_t>(13),
                missing == 2 ? std::nullopt : std::optional<std::uint64_t>(19)); });
        }
        Reject([&] { runtime::CommandJson(42, 7, input, 0, 0, 19); });
        Reject([&] { runtime::CommandJson(42, 7, input, 0, 13, 0); });
        const auto command = Json::parse(runtime::CommandJson(42, 7, input, 0, 13, 19));
        Check(command.at("navigationRevision") == 0 && command.at("openingControlsRevision") == 13
            && command.at("localAdmissionRevision") == 19
            && command.at("input") == (input == runtime::Input::OpenMain ? "openMain" : "openQuick"),
            "Opening command changed its captured independent epochs");
    }
}

void JumpBackRequiresCapturedLocalAndNavigationWhileLegacyPacketsStayExact() {
    Reject([&] { runtime::CommandJson(42, 9, runtime::Input::JumpBack); });
    Reject([&] { runtime::CommandJson(42, 9, runtime::Input::JumpBack, 0); });
    Reject([&] { runtime::CommandJson(42, 9, runtime::Input::JumpBack, 0, std::nullopt, 0); });
    const auto maximum = std::numeric_limits<std::uint64_t>::max();
    const auto command = Json::parse(runtime::CommandJson(42, 9, runtime::Input::JumpBack,
        maximum, std::nullopt, maximum));
    Check(command.at("input") == "jumpBack" && command.at("navigationRevision") == maximum
        && command.at("localAdmissionRevision") == maximum && !command.contains("openingControlsRevision"),
        "Dead-family return borrowed a policy epoch or narrowed its observation");
    // Ordinary producers retain the old exact command shape
    const auto legacy = Json::parse(runtime::CommandJson(42, 9, runtime::Input::Toggle));
    Check(legacy.size() == 4 && legacy.at("input") == "toggle", "Legacy toggle gained admission fields");
    // Supplied actorless context is preserved without borrowing local actor permission
    for (const auto navigation : {std::uint64_t{0}, maximum}) {
        const auto navigationOnly = Json::parse(runtime::CommandJson(42, 10, runtime::Input::Toggle, navigation));
        Check(navigationOnly.size() == 5 && navigationOnly.at("navigationRevision") == navigation
            && !navigationOnly.contains("openingControlsRevision"), "Toggle invented an unavailable opening policy");
        const auto scoped = Json::parse(runtime::CommandJson(42, 10, runtime::Input::Toggle, navigation, maximum));
        Check(scoped.size() == 6 && scoped.at("navigationRevision") == navigation
            && scoped.at("openingControlsRevision") == maximum && !scoped.contains("localAdmissionRevision"),
            "Actorless Toggle narrowed or omitted the displayed opening epochs");
    }
    Reject([&] { runtime::CommandJson(42, 10, runtime::Input::Toggle, std::nullopt, 1); });
}
} // namespace iw::overlay::tests
