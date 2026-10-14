#include "MechanicalSchemaSupport.h"
#include <limits>

namespace iw::overlay::tests::mechanical {
void LegacyCommandsRemainByteExactWhenMenuRevisionIsOmitted() {
    // These literal legacy packets cover every changed branch without borrowing new fields
    for (const auto& [input, expected] : {
        std::pair{runtime::Input::PreviousChoice, "{\"input\":\"previousChoice\",\"processId\":42,\"sequence\":7,\"version\":1}"},
        std::pair{runtime::Input::NextChoice, "{\"input\":\"nextChoice\",\"processId\":42,\"sequence\":7,\"version\":1}"},
        std::pair{runtime::Input::Back, "{\"input\":\"back\",\"processId\":42,\"sequence\":7,\"version\":1}"},
        std::pair{runtime::Input::Close, "{\"input\":\"close\",\"navigationRevision\":0,\"processId\":42,\"sequence\":7,\"version\":1}"}}) {
        Check(runtime::CommandJson(42, 7, input, 0) == expected, "Legacy mechanical command bytes changed");
    }
    Check(runtime::CommandJson(42, 7, runtime::Input::JumpBack, 0, std::nullopt, 4)
        == "{\"input\":\"jumpBack\",\"localAdmissionRevision\":4,\"navigationRevision\":0,\"processId\":42,\"sequence\":7,\"version\":1}",
        "Legacy scoped JumpBack gained a menu epoch");
    auto selector = Json::parse(runtime::CommandJson(42, 7, runtime::Input::NextChoice, 0, 9));
    Check(selector.at("openingControlsRevision") == 9 && !selector.contains("menuRevision"),
        "Legacy opening selector gained an unrelated epoch");
}
void MechanicalCommandsSerializeAllThreeCapturedEpochs() {
    for (auto input : {runtime::Input::PreviousChoice, runtime::Input::NextChoice, runtime::Input::Back,
        runtime::Input::Close, runtime::Input::JumpBack}) {
        for (auto revision : {std::uint64_t{0}, std::numeric_limits<std::uint64_t>::max()}) {
            auto command = Json::parse(runtime::CommandJson(42, 7, input, revision, std::nullopt, 4, revision));
            Check(command.size() == 7 && command.at("navigationRevision") == revision
                && command.at("menuRevision") == revision && command.at("localAdmissionRevision") == 4,
                "Mechanical command lost or narrowed its full captured scope");
        }
    }
    auto selector = Json::parse(runtime::CommandJson(42, 7, runtime::Input::NextChoice, 0, 9, 4, 0));
    Check(selector.size() == 8 && selector.at("openingControlsRevision") == 9 && selector.at("menuRevision") == 0,
        "New mechanical scope replaced the existing opening selector epoch");
}
void SuppliedMenuRevisionRequiresNavigationAndPositiveAdmission() {
    for (auto input : {runtime::Input::PreviousChoice, runtime::Input::NextChoice, runtime::Input::Back,
        runtime::Input::Close, runtime::Input::JumpBack}) {
        Reject([&] { runtime::CommandJson(42, 7, input, std::nullopt, std::nullopt, 4, 0); });
        Reject([&] { runtime::CommandJson(42, 7, input, 0, std::nullopt, std::nullopt, 0); });
        Reject([&] { runtime::CommandJson(42, 7, input, 0, std::nullopt, 0, 0); });
    }
}
void MenuRevisionCannotBeAttachedToUnrelatedCommands() {
    for (auto input : {runtime::Input::Select, runtime::Input::Up, runtime::Input::Toggle,
        runtime::Input::OpenMain, runtime::Input::OpenQuick, runtime::Input::FinishWidthEditor}) {
        Reject([&] { runtime::CommandJson(42, 7, input, 0, 9, 4, 0); });
    }
}
}
