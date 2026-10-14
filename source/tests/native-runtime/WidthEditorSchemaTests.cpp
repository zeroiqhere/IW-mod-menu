#include "TestSupport.h"
#include <limits>
#include <set>

namespace iw::overlay::tests {
namespace {
Json Appearance(int width = 260) {
    return {{"booleanDisplay", "Boxes"}, {"boolBoxLocation", "Right"},
        {"instructionsVisible", true}, {"optionCounter", false}, {"coloredCursor", true},
        {"spotlightCursor", true}, {"largeCursor", false}, {"quickExit", false}, {"menuWidth", width}};
}
Json EditorState() {
    auto state = StateJson();
    state["isOpen"] = true;
    state["isRootMenu"] = false;
    state["rows"] = Json::array();
    state["widthEditor"] = {{"step", 1}};
    state["appearance"] = Appearance();
    return state;
}
}
void MenuWidthDefaultsAndSupportedWidthsRemainExact() {
    auto state = StateJson();
    Check(runtime::ParseState(state.dump(), GetCurrentProcessId()).snapshot->appearance.menuWidth == 260,
        "Legacy packets must keep reference width 260");
    state["appearance"] = Appearance(); state["appearance"].erase("menuWidth");
    Check(runtime::ParseState(state.dump(), GetCurrentProcessId()).snapshot->appearance.menuWidth == 260,
        "Older explicit appearance objects must keep width 260");
    for (int width : {200, 260, 500}) {
        state["appearance"] = Appearance(width);
        const auto snapshot = runtime::ParseState(state.dump(), GetCurrentProcessId()).snapshot;
        Check(snapshot->appearance.menuWidth == width, "Confirmed menu width was ignored");
        Check(snapshot->rows[0].value == "OFF" && !snapshot->widthEditor,
            "Local width must not change feature rows or enter an editor");
    }
}
void MenuWidthRejectsOutOfRangeAndWrongTypes() {
    for (const Json& width : {Json(-1), Json(0), Json(199), Json(501), Json(1000000000), Json(260.0),
        Json(true), Json("260"), Json(nullptr), Json::array(), Json::object()}) {
        auto state = StateJson(); state["appearance"] = Appearance(); state["appearance"]["menuWidth"] = width;
        Reject([&] { runtime::ParseState(state.dump(), GetCurrentProcessId()); });
    }
}
void WidthEditorStateAndPrivateHintsAreCopiedSeparately() {
    auto state = EditorState();
    std::vector<std::string> footer{"Saved scroll", "Saved Use", "Saved Back"};
    std::vector<std::string> editor{"Saved attack", "Saved aim", "Saved next", "Saved previous", "Saved knife"};
    const auto snapshot = runtime::ParseState(state.dump(), GetCurrentProcessId(), &footer, nullptr, nullptr, &editor).snapshot;
    Check(snapshot->widthEditor && snapshot->widthEditor->step == 1, "Editor metadata must retain the confirmed offset");
    Check(snapshot->widthEditor->instructions == editor && snapshot->instructions == footer,
        "Editor body hints must not replace independent footer hints");
    Check(snapshot->widthEditor->footerInstructions[2] == "Saved knife",
        "Context help must use the same private saved exit key as the editor body");
    editor[0] = "Changed"; footer[0] = "Changed"; state["widthEditor"]["step"] = 10;
    Check(snapshot->widthEditor->step == 1 && snapshot->widthEditor->instructions[0] == "Saved attack"
        && snapshot->instructions[0] == "Saved scroll", "Snapshots must own editor values and private hints");
    const auto changed = runtime::ParseState(state.dump(), GetCurrentProcessId()).snapshot;
    Check(changed->widthEditor && changed->widthEditor->step == 10, "The maximum supported offset was lost");
    state["widthEditor"] = nullptr;
    Check(!runtime::ParseState(state.dump(), GetCurrentProcessId()).snapshot->widthEditor, "Null metadata leaves the editor");
}
void DuplicatePrivateFieldsCannotOverwriteAConfirmedValue() {
    auto state = EditorState().dump();
    const auto width = state.find("\"menuWidth\":260");
    Check(width != std::string::npos, "The owned width fixture must contain its target property");
    state.replace(width, std::string("\"menuWidth\":260").size(), "\"menuWidth\":260,\"menuWidth\":500");
    Reject([&] { runtime::ParseState(state, GetCurrentProcessId()); });
    state = EditorState().dump();
    const auto step = state.find("\"step\":1");
    state.replace(step, std::string("\"step\":1").size(), "\"step\":1,\"step\":10");
    Reject([&] { runtime::ParseState(state, GetCurrentProcessId()); });
    auto bindings = BindingsJson().dump();
    bindings.pop_back(); bindings += ",\"attack\":[1],\"\\u0061ttack\":[40]}";
    Reject([&] { runtime::ParseBindings(bindings); });
    // Reusing a name in distinct row objects is valid and must not create a false duplicate
    auto normal = StateJson(); normal["rows"].push_back(normal["rows"][0]);
    Check(runtime::ParseState(normal.dump(), GetCurrentProcessId()).snapshot->rows.size() == 2,
        "Independent objects must keep separate property-name ownership");
}
void PrivateJsonDepthRemainsBoundedWithoutDiscardingData() {
    const auto original = StateJson().dump();
    const auto nested = [&](int arrays) {
        return original.substr(0, original.size() - 1) + ",\"extension\":"
            + std::string(arrays, '[') + "0" + std::string(arrays, ']') + "}";
    };
    Check(runtime::ParseState(nested(7), GetCurrentProcessId()).snapshot->rows.size() == 1,
        "Eight total containers must remain within the private depth bound");
    Reject([&] { runtime::ParseState(nested(8), GetCurrentProcessId()); });
    Reject([&] { runtime::ParseState(nested(100), GetCurrentProcessId()); });
}
void UnknownBindingPropertiesCannotInventAControlCapability() {
    auto bindings = BindingsJson(); bindings["fire"] = {1};
    Reject([&] { runtime::ParseBindings(bindings.dump()); });
    bindings.erase("fire"); bindings["attack"] = {1};
    Check(runtime::ParseBindings(bindings.dump()).attack == std::vector<int>{1},
        "The exact supported attack property must remain valid");
}
void WidthEditorRejectsMalformedOrNonEditorContexts() {
    for (const Json& metadata : {Json(true), Json(1), Json("editor"), Json::array(), Json::object(),
        Json{{"step", 0}}, Json{{"step", 11}}, Json{{"step", 1.0}}, Json{{"step", "1"}},
        Json{{"step", true}}, Json{{"step", nullptr}}, Json{{"step", 1}, {"other", 2}}}) {
        auto state = EditorState(); state["widthEditor"] = metadata;
        Reject([&] { runtime::ParseState(state.dump(), GetCurrentProcessId()); });
    }
    auto closed = EditorState(); closed["isOpen"] = false;
    Reject([&] { runtime::ParseState(closed.dump(), GetCurrentProcessId()); });
    auto root = EditorState(); root["isRootMenu"] = true;
    Reject([&] { runtime::ParseState(root.dump(), GetCurrentProcessId()); });
    auto rows = EditorState(); rows["rows"] = StateJson()["rows"];
    Reject([&] { runtime::ParseState(rows.dump(), GetCurrentProcessId()); });
    auto selection = EditorState(); selection["selectedIndex"] = 1;
    Reject([&] { runtime::ParseState(selection.dump(), GetCurrentProcessId()); });
    auto legacy = StateJson(); legacy["widthEditor"] = nullptr;
    Check(!runtime::ParseState(legacy.dump(), GetCurrentProcessId()).snapshot->widthEditor, "Normal legacy packets keep no editor");
}
void ExactAttackBindingsPreserveKeysAndLegacyPackets() {
    auto packet = BindingsJson();
    Check(runtime::ParseBindings(packet.dump()).attack.empty(), "Legacy Down cannot imply exact attack keys");
    packet["down"] = {40, 81, 69}; packet["attack"] = {81, 69};
    packet["previousChoice"] = {51}; packet["nextChoice"] = {52};
    const auto parsed = runtime::ParseBindings(packet.dump());
    Check(parsed.attack == std::vector<int>({81, 69}) && parsed.down == std::vector<int>({40, 81, 69}),
        "Exact remapped attacks must not include action-slot 2 or assume the first Down key");
    packet["attack"] = Json::array();
    Check(runtime::ParseBindings(packet.dump()).attack.empty(), "An optional empty attack group remains unavailable");
}
void ExactAttackBindingsRejectAmbiguousMembershipAndTypes() {
    for (const Json& attack : {Json(true), Json(1), Json("1"), Json(nullptr), Json{0}, Json{255},
        Json{1, 1}, Json{2}, Json{70}, Json{86}, Json{40, 70}, Json{1.0}, Json{"1"}}) {
        auto packet = BindingsJson(); packet["attack"] = attack;
        Reject([&] { runtime::ParseBindings(packet.dump()); });
    }
    auto unsupported = BindingsJson(); unsupported["attack"] = {81};
    Reject([&] { runtime::ParseBindings(unsupported.dump()); });
    auto overlap = BindingsJson(); overlap["attack"] = {1}; overlap["nextChoice"] = {1};
    Reject([&] { runtime::ParseBindings(overlap.dump()); });
    auto oversized = BindingsJson(); oversized["attack"] = Json::array();
    for (int index = 0; index < 17; ++index) oversized["attack"].push_back(index + 1);
    Reject([&] { runtime::ParseBindings(oversized.dump()); });
}
void WidthEditorCommandsRequireTheExactNavigationEpoch() {
    for (const auto input : {runtime::Input::IncreaseWidth, runtime::Input::DecreaseWidth,
        runtime::Input::IncreaseWidthStep, runtime::Input::DecreaseWidthStep, runtime::Input::FinishWidthEditor}) {
        Reject([&] { runtime::CommandJson(GetCurrentProcessId(), 8, input); });
        for (std::uint64_t epoch : {std::uint64_t{0}, std::numeric_limits<std::uint64_t>::max()}) {
            const auto command = Json::parse(runtime::CommandJson(GetCurrentProcessId(), 8, input, epoch));
            Check(command.at("navigationRevision").get<std::uint64_t>() == epoch && command.size() == 5
                && command.at("input") == runtime::InputName(input), "Editor command lost its required exact epoch");
        }
    }
}
void EditorMetadataJoinsTheRealRouterWithoutNormalMenuCommands() {
    auto state = EditorState(); state["navigationRevision"] = 9;
    auto packet = BindingsJson(); packet["attack"] = {1}; packet["previousChoice"] = {51}; packet["nextChoice"] = {52};
    const auto parsed = runtime::ParseState(state.dump(), GetCurrentProcessId());
    runtime::NativeInputRouter router(runtime::ParseBindings(packet.dump()));
    std::set<int> held;
    auto poll = [&](std::uint64_t time) {
        return router.Poll(true, parsed.snapshot->isOpen, false, [&](int key) { return held.contains(key); },
            time, parsed.snapshot->navigationRevision, parsed.snapshot->appearance.quickExit,
            parsed.snapshot->isRootMenu, parsed.snapshot->widthEditor ? runtime::InputMode::WidthEditor : runtime::InputMode::Menu);
    };
    poll(0); held = {1};
    Check(poll(0) == runtime::Input::IncreaseWidth, "Parsed editor metadata did not select width input routing");
    Check(poll(75) == runtime::Input::IncreaseWidth, "Parsed editor route lost the width-repeat cadence");
    held.clear(); poll(80); held = {86};
    Check(poll(80) == runtime::Input::FinishWidthEditor, "Editor knife must not dispatch Back or Quick Exit");
}
}
