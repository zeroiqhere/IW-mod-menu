#include "TestSupport.h"
#include "ControlLabels.h"
#include <array>
#include <limits>
#include <set>

namespace iw::overlay::tests {
namespace {
Json Appearance() {
    return {{"booleanDisplay", "Boxes"}, {"boolBoxLocation", "Right"}, {"instructionsVisible", true},
        {"optionCounter", false}, {"coloredCursor", true}, {"spotlightCursor", true}, {"largeCursor", false}};
}
Json EditorPacket() {
    auto source = StateJson(); source["isOpen"] = true; source["isRootMenu"] = false;
    source["title"] = "Menu Customization"; source["rows"] = Json::array();
    source["menuPositionEditor"] = {{"step", 1}};
    return source;
}
Json CompleteBindings() {
    auto source = BindingsJson(); source["attack"] = {1}; source["previousChoice"] = {51};
    source["nextChoice"] = {52}; source["moveUp"] = {38}; source["moveDown"] = {40};
    source["frag"] = {71}; source["secondaryOffhand"] = {69};
    return source;
}
}
void MenuPositionDefaultsAndSignedBoundsRemainOwned() {
    auto source = StateJson();
    Check(!runtime::ParseState(source.dump(), GetCurrentProcessId()).snapshot->appearance.menuPosition
        && DefaultMenuPosition() == MenuPosition{0, 85}, "Legacy panel placement changed");
    source["appearance"] = nullptr;
    Check(!runtime::ParseState(source.dump(), GetCurrentProcessId()).snapshot->appearance.menuPosition,
        "Null appearance invented a saved panel origin");
    source["appearance"] = Appearance(); source["appearance"]["menuPosition"] = nullptr;
    Check(!runtime::ParseState(source.dump(), GetCurrentProcessId()).snapshot->appearance.menuPosition,
        "Null panel position did not retain source default");
    for (const auto coordinate : {std::numeric_limits<std::int32_t>::min(), -1, 0, std::numeric_limits<std::int32_t>::max()}) {
        source["appearance"]["menuPosition"] = {{"x", coordinate}, {"y", coordinate}};
        const auto owned = runtime::ParseState(source.dump(), GetCurrentProcessId()).snapshot;
        source["appearance"]["menuPosition"]["x"] = 123;
        Check(owned->appearance.menuPosition == MenuPosition{coordinate, coordinate}, "A saved signed32 origin changed or retained JSON");
    }
}
void MenuPositionRejectsMalformedCoordinatesAndEditorMetadata() {
    const std::array<Json, 10> malformed{nullptr, false, "1", 1.0, Json::array(), Json::object(),
        std::int64_t(-2147483649LL), std::uint64_t(2147483648ULL),
        std::numeric_limits<std::uint64_t>::max(), std::numeric_limits<std::int64_t>::min()};
    for (const auto* axis : {"x", "y"}) {
        for (const auto& invalid : malformed) {
            auto source = StateJson(); source["appearance"] = Appearance();
            source["appearance"]["menuPosition"] = {{"x", 0}, {"y", 85}};
            source["appearance"]["menuPosition"][axis] = invalid;
            Reject([&] { runtime::ParseState(source.dump(), GetCurrentProcessId()); });
        }
    }
    for (const Json& invalid : {Json(false), Json(1), Json("position"), Json::array(), Json::object(),
        Json{{"x", 1}}, Json{{"x", 1}, {"y", 2}, {"z", 3}}}) {
        auto source = StateJson(); source["appearance"] = Appearance(); source["appearance"]["menuPosition"] = invalid;
        Reject([&] { runtime::ParseState(source.dump(), GetCurrentProcessId()); });
    }
    for (const Json& invalid : {Json(false), Json(1), Json("editor"), Json::array(), Json::object(),
        Json{{"step", nullptr}}, Json{{"step", true}}, Json{{"step", "1"}}, Json{{"step", 1.0}},
        Json{{"step", -1}}, Json{{"step", 0}}, Json{{"step", 11}},
        Json{{"step", std::numeric_limits<std::uint64_t>::max()}}, Json{{"step", 1}, {"phase", "editing"}}}) {
        auto source = EditorPacket(); source["menuPositionEditor"] = invalid;
        Reject([&] { runtime::ParseState(source.dump(), GetCurrentProcessId()); });
    }
    // Decoded aliases are duplicate properties even when one spelling uses a Unicode escape
    auto source = StateJson(); source["appearance"] = Appearance(); source["appearance"]["menuPosition"] = {{"x", 0}, {"y", 85}};
    auto bytes = source.dump(); const auto location = bytes.find("{\"x\":0,\"y\":85}");
    Check(location != std::string::npos, "Coordinate duplicate fixture lost its object");
    bytes.replace(location, 14, "{\"x\":0,\"\\u0078\":1,\"y\":85}");
    Reject([&] { runtime::ParseState(bytes, GetCurrentProcessId()); });
    bytes = EditorPacket().dump(); const auto step = bytes.find("{\"step\":1}");
    Check(step != std::string::npos, "Step duplicate fixture lost its editor object");
    bytes.replace(step, 10, "{\"step\":1,\"\\u0073tep\":2}");
    Reject([&] { runtime::ParseState(bytes, GetCurrentProcessId()); });
}
void MenuPositionEditorOwnsStepsHintsAndOrdinaryFooterSeparately() {
    auto source = EditorPacket(); const std::vector<std::string> normal{"Scroll", "Select", "Back"};
    auto hints = runtime::DescribeMenuPositionControls(runtime::ParseBindings(CompleteBindings().dump()));
    for (int step : {1, 10}) {
        source["menuPositionEditor"]["step"] = step;
        const auto snapshot = runtime::ParseState(source.dump(), GetCurrentProcessId(), &normal,
            nullptr, nullptr, nullptr, nullptr, std::uint64_t{7}, &hints).snapshot;
        Check(snapshot->menuPositionEditor && snapshot->menuPositionEditor->step == step
            && snapshot->menuPositionEditor->instructions == hints && snapshot->instructions == normal
            && snapshot->menuPositionEditor->footerInstructions == std::vector<std::string>{"Reposition Menu", "Menu navigation paused", "V / Mouse 5: Exit"}
            && snapshot->presentationFocusRevision == 7 && snapshot->title == "Menu Customization"
            && runtime::InputModeForSnapshot(*snapshot) == runtime::InputMode::MenuPositionEditor,
            "Editor metadata or private hints changed title, footer or routing");
    }
    const auto owned = runtime::ParseState(source.dump(), GetCurrentProcessId(), nullptr,
        nullptr, nullptr, nullptr, nullptr, std::nullopt, &hints).snapshot;
    hints[0] = "changed"; source["menuPositionEditor"]["step"] = 2;
    Check(owned->menuPositionEditor->step == 10 && owned->menuPositionEditor->instructions[0] == "Up: Move Up",
        "Menu position metadata retained a mutable caller buffer");
    hints.pop_back(); Reject([&] { runtime::ParseState(source.dump(), GetCurrentProcessId(), nullptr,
        nullptr, nullptr, nullptr, nullptr, std::nullopt, &hints); });
    source = StateJson(); source["menuPositionEditor"] = nullptr;
    Check(!runtime::ParseState(source.dump(), GetCurrentProcessId()).snapshot->menuPositionEditor,
        "A null editor changed legacy routing");
}
void MenuPositionEditorRejectsEveryConflictingModalContext() {
    for (int conflict = 0; conflict < 7; ++conflict) {
        auto source = EditorPacket();
        if (conflict == 0) source["isOpen"] = false;
        if (conflict == 1) source["isRootMenu"] = true;
        if (conflict == 2) source["rows"] = StateJson().at("rows");
        if (conflict == 3) source["selectedIndex"] = 1;
        if (conflict == 4) source["widthEditor"] = {{"step", 1}};
        if (conflict == 5) source["instructionPositionEditor"] = {{"phase", "editing"}};
        if (conflict == 6) source["menuUnlock"] = "pause";
        Reject([&] { runtime::ParseState(source.dump(), GetCurrentProcessId()); });
    }
    // Hidden ordinary instructions do not disable this independent panel editor
    auto source = EditorPacket(); source["appearance"] = Appearance(); source["appearance"]["instructionsVisible"] = false;
    Check(runtime::ParseState(source.dump(), GetCurrentProcessId()).snapshot->menuPositionEditor.has_value(),
        "Hidden instructions incorrectly disabled panel positioning");
}
void MenuPositionGrenadeBindingsRejectMalformedKeysAndEveryAlias() {
    auto source = CompleteBindings(); const auto saved = runtime::ParseBindings(source.dump());
    Check(saved.frag == std::vector<int>{71} && saved.secondaryOffhand == std::vector<int>{69}, "Saved grenades changed");
    const auto legacy = runtime::ParseBindings(BindingsJson().dump());
    Check(legacy.frag.empty() && legacy.secondaryOffhand.empty(), "Legacy bindings invented grenades");
    for (const auto* field : {"frag", "secondaryOffhand"}) {
        for (const Json& invalid : {Json(nullptr), Json(1), Json("71"), Json::array({true}), Json::array({71.0}),
            Json::array({0}), Json::array({255}), Json::array({71, 71}), Json(std::vector<int>(17, 71))}) {
            auto malformed = source; malformed[field] = invalid;
            Reject([&] { runtime::ParseBindings(malformed.dump()); });
        }
        // Every required, optional and grenade action group must remain disjoint
        for (int key : {2, 86, 6, 38, 1, 40, 70, 51, 52, 71, 69}) {
            if ((std::string(field) == "frag" && key == 71) || (std::string(field) == "secondaryOffhand" && key == 69)) continue;
            auto overlap = source; overlap[field] = {key}; Reject([&] { runtime::ParseBindings(overlap.dump()); });
        }
        auto empty = source; empty[field] = Json::array(); runtime::ParseBindings(empty.dump());
    }
}
void MenuPositionCommandsRequireExactCapturedUnsignedEpochs() {
    for (auto input : {runtime::Input::MoveMenuUp, runtime::Input::MoveMenuDown, runtime::Input::MoveMenuLeft,
        runtime::Input::MoveMenuRight, runtime::Input::IncreaseMenuPositionStep, runtime::Input::DecreaseMenuPositionStep,
        runtime::Input::FinishMenuPositionEditor}) {
        Reject([&] { runtime::CommandJson(42, 1, input); });
        for (auto revision : {std::uint64_t{0}, std::numeric_limits<std::uint64_t>::max()}) {
            const auto command = Json::parse(runtime::CommandJson(42, 1, input, revision));
            Check(command.at("navigationRevision") == revision && command.at("input") == runtime::InputName(input)
                && command.at("processId") == 42 && command.at("sequence") == 1, "An editor command lost its owned context");
        }
    }
}
void MenuPositionSchemaRoutesImmediateEntryAndConsumesUnlockHolds() {
    runtime::NativeInputRouter router(runtime::ParseBindings(CompleteBindings().dump()));
    auto source = EditorPacket(); source["navigationRevision"] = 11;
    std::set<int> held{38}; const auto poll = [&](std::uint64_t now) {
        const auto snapshot = runtime::ParseState(source.dump(), GetCurrentProcessId()).snapshot;
        return router.Poll(true, snapshot->isOpen, false, [&](int key) { return held.contains(key); }, now,
            snapshot->navigationRevision, false, snapshot->isRootMenu, runtime::InputModeForSnapshot(*snapshot));
    };
    Check(!poll(0) && !poll(1000), "Immediate entry inherited a movement hold");
    held.clear(); poll(1001); held = {38}; Check(poll(1001) == runtime::Input::MoveMenuUp, "Fresh immediate entry failed to route movement");
    held.clear(); poll(1026); held = {86}; Check(poll(1026) == runtime::Input::FinishMenuPositionEditor, "Melee failed to finish after source movement wait");
    source.erase("menuPositionEditor"); source["menuUnlock"] = "rebuild"; poll(1026);
    source["menuUnlock"] = "pause"; Check(!poll(1076), "Unlock pause replayed editor exit");
    source.erase("menuUnlock"); source["rows"] = StateJson().at("rows"); source["navigationRevision"] = 12;
    Check(!poll(1176) && !poll(2000), "Unlocked parent inherited held editor exit");
    held.clear(); poll(2001); held = {70}; Check(poll(2001) == runtime::Input::Select, "Parent retained modal editor routing");
}
}
