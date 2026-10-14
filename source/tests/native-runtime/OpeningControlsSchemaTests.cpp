#include "ControlLabels.h"
#include "TestSupport.h"
#include <limits>
namespace iw::overlay::tests {
void OpeningControlsRejectMalformedDescriptorBeforeRouting() {
    // A supplied invalid policy must never fall back to the old opening chord
    for (const Json &descriptor :
         {Json(nullptr), Json(true), Json(7), Json("aim"), Json{{"revision", 0}, {"actions", {5, 4}}},
          Json{{"revision", 1}, {"actions", Json::array()}}, Json{{"revision", 1}, {"actions", {5, 5}}},
          Json{{"revision", 1}, {"actions", {9, 10}}}, Json{{"revision", 1}, {"actions", {13}}}}) {
        auto packet = StateJson();
        packet["mainOpeningControls"] = descriptor;
        Reject([&] { runtime::ParseState(packet.dump(), GetCurrentProcessId()); });
    }
}
void OpeningRowsRejectMalformedSpacerAndUnknownAction() {
    for (const Json &flag : {Json(nullptr), Json(1), Json("true"), Json::array()}) {
        auto packet = StateJson();
        packet["rows"][0]["isSpacer"] = flag;
        Reject([&] { runtime::ParseState(packet.dump(), GetCurrentProcessId()); });
    }
    auto packet = StateJson();
    packet["rows"][0]["openingAction"] = 13;
    Reject([&] { runtime::ParseState(packet.dump(), GetCurrentProcessId()); });
}
namespace {
Json FullOpeningBindings() {
    auto packet = BindingsJson();
    packet["previousChoice"] = {51};
    packet["nextChoice"] = {52};
    packet["attack"] = {1};
    packet["moveUp"] = {38};
    packet["moveDown"] = {40};
    packet["frag"] = {71};
    packet["secondaryOffhand"] = {69};
    packet["sprint"] = {16};
    packet["stance"] = {17};
    packet["jump"] = {32};
    return packet;
}
auto ParseWithLabels(const Json &packet, const runtime::OpeningActionLabels &labels) {
    return runtime::ParseState(packet.dump(), GetCurrentProcessId(), nullptr, nullptr, nullptr, nullptr,
                               nullptr, std::nullopt, nullptr, nullptr, &labels)
        .snapshot;
}
} // namespace
void MainOpeningDescriptorOwnsClosedRootChildAndPositiveBounds() {
    auto packet = StateJson();
    Check(!runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot->mainOpeningControls,
          "Legacy packet invented a global opening policy");
    for (bool open : {false, true}) {
        for (bool root : {false, true}) {
            packet["isOpen"] = open;
            packet["isRootMenu"] = root;
            packet["mainOpeningControls"] = {{"revision", std::numeric_limits<std::uint64_t>::max()},
                                             {"actions", {5, 4, 7}}};
            const auto state = runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot;
            packet["mainOpeningControls"]["actions"][0] = 12;
            Check(state->mainOpeningControls &&
                      state->mainOpeningControls->revision == std::numeric_limits<std::uint64_t>::max() &&
                      state->mainOpeningControls->actions ==
                          std::vector<OpeningAction>{OpeningAction::Aim, OpeningAction::Melee,
                                                     OpeningAction::Sprint} &&
                      runtime::InputModeForSnapshot(*state) == runtime::InputMode::Menu,
                  "Global descriptor was not owned independently of page visibility");
        }
    }
}
void MainOpeningDescriptorRejectsDecodedDuplicatesAndExactKeys() {
    auto packet = StateJson();
    for (const auto &descriptor :
         {Json{{"revision", 1}, {"actions", {5, 4}}, {"extra", true}},
          Json{{"revision", 1.5}, {"actions", {5}}}, Json{{"revision", -1}, {"actions", {5}}},
          Json{{"revision", 1}, {"actions", {0, 1, 2, 3}}}, Json{{"revision", 1}, {"actions", {"5"}}},
          Json{{"revision", 1}, {"actions", {nullptr}}}, Json{{"revision", 1}, {"actions", {true}}},
          Json{{"revision", 1}, {"actions", {1.0}}}}) {
        packet["mainOpeningControls"] = descriptor;
        Reject([&] { runtime::ParseState(packet.dump(), GetCurrentProcessId()); });
    }
    auto raw = StateJson().dump();
    raw.pop_back();
    raw += R"(,"mainOpeningControls":{"revision":1,"\u0072evision":2,"actions":[5,4]}})";
    Reject([&] { runtime::ParseState(raw, GetCurrentProcessId()); });
    raw = StateJson().dump();
    raw.pop_back();
    raw +=
        R"(,"mainOpeningControls":{"revision":1,"actions":[5]},"mainOpeningControls":{"revision":2,"actions":[4]}})";
    Reject([&] { runtime::ParseState(raw, GetCurrentProcessId()); });
}
void OpeningRowsOwnSpacerAndRejectSelectedBlank() {
    auto packet = StateJson();
    packet["rows"].push_back({{"label", ""}, {"value", ""}, {"enabled", false}, {"isSpacer", true}});
    const auto state = runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot;
    Check(!state->rows[0].isSpacer && state->rows[1].isSpacer, "Spacer was inferred from an ordinary row");
    for (bool open : {false, true}) {
        packet["isOpen"] = open;
        packet["selectedIndex"] = 1;
        Reject([&] { runtime::ParseState(packet.dump(), GetCurrentProcessId()); });
    }
    packet["selectedIndex"] = 0;
    for (const auto &change : {Json{{"label", "not blank"}}, Json{{"value", "OFF"}}, Json{{"enabled", true}},
                               Json{{"openingAction", 0}}}) {
        auto bad = packet;
        for (auto item = change.begin(); item != change.end(); ++item) {
            bad["rows"][1][item.key()] = item.value();
        }
        Reject([&] { runtime::ParseState(bad.dump(), GetCurrentProcessId()); });
    }
    // An explicitly false tag leaves legacy unavailable and blank rows selectable
    packet["rows"][1]["isSpacer"] = false;
    packet["selectedIndex"] = 1;
    Check(!runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot->rows[1].isSpacer,
          "False spacer tag changed legacy selection");
}
void OpeningActionLabelsUsePrivateGroupsWithoutGuessing() {
    const auto labels = runtime::DescribeOpeningActions(runtime::ParseBindings(FullOpeningBindings().dump()));
    Check(labels[4] == "V / Mouse 5" && labels[5] == "RMB" && labels[7] == "Shift" && labels[11] == "Ctrl" &&
              labels[12] == "Space",
          "Opening captions did not describe the exact saved groups");
    auto packet = StateJson();
    packet["mainOpeningControls"] = {{"revision", 1}, {"actions", {5, 4}}};
    packet["rows"][0]["openingAction"] = 7;
    const auto owned = ParseWithLabels(packet, labels);
    Check(owned->rows[0].label == "Shift", "Bound row retained a logical placeholder");
    auto missing = labels;
    missing[7].clear();
    Reject([&] { ParseWithLabels(packet, missing); });
    packet["rows"][0]["enabled"] = false;
    packet["rows"][0]["value"] = "UNAVAILABLE";
    Check(ParseWithLabels(packet, missing)->rows[0].label == "Sprint", "Unbound row invented a key caption");
    packet["mainOpeningControls"]["actions"] = {7};
    for (bool open : {false, true}) {
        packet["isOpen"] = open;
        Reject([&] { ParseWithLabels(packet, missing); });
    }
    Check(runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot->mainOpeningControls.has_value(),
          "Syntax-only caller pretended to know private action availability");
}
void OpeningActionBindingsOptionalStrictTypesAndMembership() {
    const auto legacy = runtime::ParseBindings(BindingsJson().dump());
    Check(legacy.sprint.empty() && legacy.stance.empty() && legacy.jump.empty(),
          "Legacy private packet guessed optional keys");
    const auto exact = runtime::ParseBindings(FullOpeningBindings().dump());
    Check(exact.sprint == std::vector<int>{16} && exact.stance == std::vector<int>{17} &&
              exact.jump == std::vector<int>{32},
          "Exact new groups were lost");
    for (const char *group : {"sprint", "stance", "jump"}) {
        for (const auto &keys : {Json(nullptr), Json("key"), Json(1), Json{0}, Json{255}, Json{-1}, Json{1.5},
                                 Json{true}, Json{16, 16}}) {
            auto bad = FullOpeningBindings();
            bad[group] = keys;
            Reject([&] { runtime::ParseBindings(bad.dump()); });
        }
        auto bad = FullOpeningBindings();
        bad[group] = Json(std::vector<int>(17, 16));
        Reject([&] { runtime::ParseBindings(bad.dump()); });
        for (int key : {2, 86, 38, 1, 40, 70, 51, 52, 71, 69}) {
            bad = FullOpeningBindings();
            bad[group] = {key};
            Reject([&] { runtime::ParseBindings(bad.dump()); });
        }
    }
}
void OpeningMutationCommandsRequireCapturedPolicyAndNavigation() {
    const auto maximum = std::numeric_limits<std::uint64_t>::max();
    for (auto input : {runtime::Input::Select, runtime::Input::PreviousChoice, runtime::Input::NextChoice}) {
        const auto command =
            Json::parse(runtime::CommandJson(GetCurrentProcessId(), 7, input, maximum, maximum));
        Check(command.at("navigationRevision") == maximum && command.at("openingControlsRevision") == maximum,
              "Mutation command fabricated or narrowed its captured revisions");
        Reject([&] { runtime::CommandJson(GetCurrentProcessId(), 7, input, std::nullopt, 1); });
        Reject([&] { runtime::CommandJson(GetCurrentProcessId(), 7, input, 0, 0); });
        const auto legacy = Json::parse(runtime::CommandJson(GetCurrentProcessId(), 7, input));
        Check(!legacy.contains("navigationRevision") && !legacy.contains("openingControlsRevision"),
              "Legacy ordinary command fields changed");
    }
}

} // namespace iw::overlay::tests
