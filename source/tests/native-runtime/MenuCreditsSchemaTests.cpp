#include "TestSupport.h"
#include "ControlLabels.h"
#include <limits>
#include <set>

namespace iw::overlay::tests {
namespace {
Json CreditsPacket() {
    auto source = StateJson(); source["isOpen"] = true; source["isRootMenu"] = false;
    source["title"] = "Menu Customization"; source["rows"] = Json::array();
    source["menuCredits"] = {{"elapsedMilliseconds", 0}};
    return source;
}
}
void MenuCreditsPreserveLegacyDefaultsAndOwnedElapsedBoundaries() {
    auto source = StateJson();
    Check(!runtime::ParseState(source.dump(), GetCurrentProcessId()).snapshot->menuCredits,
        "Legacy state invented a credits visit");
    source["menuCredits"] = nullptr;
    Check(!runtime::ParseState(source.dump(), GetCurrentProcessId()).snapshot->menuCredits,
        "Null credits changed ordinary menu state");
    for (int elapsed : {0, 500, 1000, 39999}) {
        source = CreditsPacket(); source["menuCredits"]["elapsedMilliseconds"] = elapsed;
        const auto owned = runtime::ParseState(source.dump(), GetCurrentProcessId()).snapshot;
        source["menuCredits"]["elapsedMilliseconds"] = 1;
        Check(owned->menuCredits && owned->menuCredits->elapsedMilliseconds == elapsed
            && owned->title == "Menu Customization" && owned->rows.empty() && owned->selectedIndex == 0
            && runtime::InputModeForSnapshot(*owned) == runtime::InputMode::MenuCredits,
            "Credits progress retained mutable JSON or changed modal identity");
    }
}
void MenuCreditsRejectsInvalidElapsedTimeAndClosedContexts() {
    for (const Json& invalid : {Json(nullptr), Json(true), Json(1), Json("credits"), Json::array(), Json::object(),
        Json{{"elapsedMilliseconds", nullptr}}, Json{{"elapsedMilliseconds", false}}, Json{{"elapsedMilliseconds", "0"}},
        Json{{"elapsedMilliseconds", 0.0}}, Json{{"elapsedMilliseconds", -1}}, Json{{"elapsedMilliseconds", 40000}},
        Json{{"elapsedMilliseconds", std::numeric_limits<std::uint64_t>::max()}},
        Json{{"elapsedMilliseconds", std::numeric_limits<std::int64_t>::min()}},
        Json{{"elapsedMilliseconds", 0}, {"instructions", Json::array({"injected"})}}}) {
        // Null metadata is the one supported inactive shape, not a malformed active visit
        if (invalid.is_null()) continue;
        auto source = CreditsPacket(); source["menuCredits"] = invalid;
        Reject([&] { runtime::ParseState(source.dump(), GetCurrentProcessId()); });
    }
    auto source = CreditsPacket(); source["isOpen"] = false;
    Reject([&] { runtime::ParseState(source.dump(), GetCurrentProcessId()); });
    // Duplicate decoded field names cannot replace validated progress with another value
    for (const auto* replacement : {"{\"elapsedMilliseconds\":0,\"elapsedMilliseconds\":1}",
        "{\"elapsedMilliseconds\":0,\"\\u0065lapsedMilliseconds\":1}"}) {
        auto bytes = CreditsPacket().dump(); const auto object = std::string("{\"elapsedMilliseconds\":0}");
        const auto start = bytes.find(object); Check(start != std::string::npos, "Credits duplicate fixture lost its object");
        bytes.replace(start, object.size(), replacement);
        Reject([&] { runtime::ParseState(bytes, GetCurrentProcessId()); });
    }
}
void MenuCreditsRejectEveryConflictingEditorUnlockAndSelection() {
    for (int conflict = 0; conflict < 8; ++conflict) {
        auto source = CreditsPacket();
        if (conflict == 0) source["isOpen"] = false;
        if (conflict == 1) source["isRootMenu"] = true;
        if (conflict == 2) source["rows"] = StateJson().at("rows");
        if (conflict == 3) source["selectedIndex"] = 1;
        if (conflict == 4) source["widthEditor"] = {{"step", 1}};
        if (conflict == 5) source["instructionPositionEditor"] = {{"phase", "editing"}};
        if (conflict == 6) source["menuPositionEditor"] = {{"step", 1}};
        if (conflict == 7) source["menuUnlock"] = "pause";
        Reject([&] { runtime::ParseState(source.dump(), GetCurrentProcessId()); });
    }
    // A hidden ordinary footer does not disable the credits roll or its saved exit key
    auto source = CreditsPacket(); source["appearance"] = {{"booleanDisplay", "Boxes"}, {"boolBoxLocation", "Right"},
        {"instructionsVisible", false}, {"optionCounter", false}, {"coloredCursor", true}, {"spotlightCursor", true}, {"largeCursor", false}};
    Check(runtime::ParseState(source.dump(), GetCurrentProcessId()).snapshot->menuCredits.has_value(),
        "Hidden instructions incorrectly disabled credits");
}
void MenuCreditsOwnPrivateMeleeHelpWithoutReplacingOrdinaryInstructions() {
    const auto source = CreditsPacket(); const std::vector<std::string> normal{"Scroll", "Select", "Back"};
    auto hints = runtime::DescribeMenuCreditsControls(runtime::ParseBindings(BindingsJson().dump()));
    const auto owned = runtime::ParseState(source.dump(), GetCurrentProcessId(), &normal,
        nullptr, nullptr, nullptr, nullptr, std::uint64_t{7}, nullptr, &hints).snapshot;
    Check(owned->menuCredits->instructions == std::vector<std::string>{"V / Mouse 5: Exit Menu Credits"}
        && owned->instructions == normal && owned->presentationFocusRevision == 7,
        "Credits help borrowed normal gestures or discarded private focus identity");
    hints[0] = "changed";
    Check(owned->menuCredits->instructions[0] == "V / Mouse 5: Exit Menu Credits", "Private credits help retained caller storage");
    for (const auto& invalid : {std::vector<std::string>{}, std::vector<std::string>{"one", "two"}}) {
        Reject([&] { runtime::ParseState(source.dump(), GetCurrentProcessId(), nullptr,
            nullptr, nullptr, nullptr, nullptr, std::nullopt, nullptr, &invalid); });
    }
}
void MenuCreditsFinishCommandsRequireTheirExactCapturedUnsignedEpoch() {
    Reject([&] { runtime::CommandJson(42, 1, runtime::Input::FinishMenuCredits); });
    for (auto revision : {std::uint64_t{0}, std::numeric_limits<std::uint64_t>::max()}) {
        const auto command = Json::parse(runtime::CommandJson(42, 1, runtime::Input::FinishMenuCredits, revision));
        Check(command.at("input") == "finishMenuCredits" && command.at("navigationRevision") == revision
            && command.at("sequence") == 1 && command.at("processId") == 42,
            "Credits exit lost its exact command identity or captured navigation epoch");
    }
}
void MenuCreditsSchemaJoinsFreshExitAndConsumesReturnHolds() {
    runtime::NativeInputRouter router(runtime::ParseBindings(BindingsJson().dump()));
    auto source = CreditsPacket(); source["navigationRevision"] = 12; source["menuRevision"] = 6;
    std::set<int> held{86}; const auto poll = [&](std::uint64_t now) {
        const auto snapshot = runtime::ParseState(source.dump(), GetCurrentProcessId()).snapshot;
        return router.Poll(true, snapshot->isOpen, false, [&](int key) { return held.contains(key); }, now,
            snapshot->navigationRevision, true, snapshot->isRootMenu, runtime::InputModeForSnapshot(*snapshot),
            snapshot->appearance.scrollAnimationMilliseconds, snapshot->menuRevision);
    };
    Check(!poll(0) && !poll(1000), "Credits entry inherited held melee");
    held.clear(); poll(1001); held = {86};
    const auto exit = poll(1001); Check(exit == runtime::Input::FinishMenuCredits, "Fresh credits exit entered normal Back");
    Check(Json::parse(runtime::CommandJson(42, 1, *exit, 12)).at("navigationRevision") == 12,
        "Joined credits input lost its captured epoch");
    source.erase("menuCredits"); source["rows"] = StateJson().at("rows"); source["navigationRevision"] = 14;
    source["menuRevision"] = 8; source["menuUnlock"] = "rebuild"; Check(!poll(1001), "Credits rebuild replayed exit");
    source["menuUnlock"] = "pause"; Check(!poll(1051), "Credits pause allowed old exit to act");
    source.erase("menuUnlock"); Check(!poll(1151) && !poll(2000), "Credits return leaked old melee into Back or Close");
    held.clear(); poll(2001); held = {70}; Check(poll(2001) == runtime::Input::Select, "Parent retained credits routing");
}
}
