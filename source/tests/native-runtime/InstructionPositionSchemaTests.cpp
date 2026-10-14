#include "TestSupport.h"
#include <array>
#include <limits>
#include <set>

namespace iw::overlay::tests {
namespace {
// Packets use the real producer shape and retain all legacy appearance fields
Json AppearanceWithPosition() {
    return {{"booleanDisplay", "Boxes"}, {"boolBoxLocation", "Right"},
        {"instructionsVisible", true}, {"optionCounter", false}, {"coloredCursor", true},
        {"spotlightCursor", true}, {"largeCursor", false}, {"instructionPosition", {{"x", 263}, {"y", 480}}}};
}
Json PositionEditorPacket() {
    auto value = StateJson();
    value["isOpen"] = true;
    value["isRootMenu"] = false;
    value["rows"] = Json::array();
    value["appearance"] = AppearanceWithPosition();
    value["instructionPositionEditor"] = {{"phase", "editing"}};
    return value;
}
}

void PositionCoordinatesPreserveNullStyleDefaultsAndSignedBounds() {
    auto source = StateJson();
    Check(!runtime::ParseState(source.dump(), GetCurrentProcessId()).snapshot->appearance.instructionPosition,
        "A legacy packet invented a saved position");
    Check(DefaultInstructionPosition(false) == InstructionPosition{255, 472}
        && DefaultInstructionPosition(true) == InstructionPosition{320, 472}, "Source style defaults changed");
    source["appearance"] = AppearanceWithPosition();
    const auto original = runtime::ParseState(source.dump(), GetCurrentProcessId()).snapshot;
    Check(original->appearance.instructionPosition == InstructionPosition{263, 480}, "A saved position was ignored");
    // Parsed coordinates must own their values after the packet changes
    source["appearance"]["instructionPosition"]["x"] = 1;
    Check(original->appearance.instructionPosition == InstructionPosition{263, 480}, "Coordinates retained mutable JSON");
    for (int value : {std::numeric_limits<std::int32_t>::min(), -1, 0, std::numeric_limits<std::int32_t>::max()}) {
        source["appearance"]["instructionPosition"] = {{"x", value}, {"y", value}};
        Check(runtime::ParseState(source.dump(), GetCurrentProcessId()).snapshot->appearance.instructionPosition
            == InstructionPosition{value, value}, "A signed32 position boundary changed");
    }
    source["appearance"]["instructionPosition"] = nullptr;
    Check(!runtime::ParseState(source.dump(), GetCurrentProcessId()).snapshot->appearance.instructionPosition,
        "A null position must preserve style-derived placement");
    source["appearance"].erase("instructionPosition");
    source["appearance"]["alternateInstructions"] = true;
    Check(!runtime::ParseState(source.dump(), GetCurrentProcessId()).snapshot->appearance.instructionPosition,
        "An omitted alternate position must stay derived");
}

void PositionCoordinatesRejectWrongShapesTypesOverflowAndDuplicates() {
    const std::array<Json, 10> invalid{nullptr, true, "1", 1.0, Json::array(), Json::object(),
        std::int64_t(-2147483649LL), std::uint64_t(2147483648ULL),
        std::numeric_limits<std::uint64_t>::max(), std::numeric_limits<std::int64_t>::min()};
    // Each axis has the same strict signed32 contract
    for (const auto* field : {"x", "y"}) {
        for (const auto& value : invalid) {
            auto source = StateJson(); source["appearance"] = AppearanceWithPosition();
            source["appearance"]["instructionPosition"][field] = value;
            Reject([&] { runtime::ParseState(source.dump(), GetCurrentProcessId()); });
        }
    }
    for (const Json& position : {Json(true), Json(1), Json("position"), Json::array(), Json::object(),
        Json{{"x", 1}}, Json{{"x", 1}, {"y", 2}, {"z", 3}}}) {
        auto source = StateJson(); source["appearance"] = AppearanceWithPosition();
        source["appearance"]["instructionPosition"] = position;
        Reject([&] { runtime::ParseState(source.dump(), GetCurrentProcessId()); });
    }
    auto source = StateJson(); source["appearance"] = AppearanceWithPosition();
    std::string bytes = source.dump();
    const auto start = bytes.find("{\"x\":263,\"y\":480}");
    Check(start != std::string::npos, "Duplicate-field fixture lost its coordinate object");
    // Escaped aliases count as duplicate decoded JSON property names
    bytes.replace(start, 17, "{\"x\":263,\"\\u0078\":1,\"y\":480}");
    Reject([&] { runtime::ParseState(bytes, GetCurrentProcessId()); });
}

void PositionEditorPhasesAndPrivateHintsAreOwnedWithoutChangingOrdinaryHelp() {
    auto source = PositionEditorPacket();
    std::vector<std::string> hints{"W: Move Up", "S: Move Down", "A: Move Left", "D: Move Right", "V: Exit"};
    const std::vector<std::string> normal{"Scroll", "Use", "Back"};
    for (const auto& [name, phase] : {std::pair{"entering", InstructionPositionEditorPhase::Entering},
        {"editing", InstructionPositionEditorPhase::Editing}, {"exiting", InstructionPositionEditorPhase::Exiting}}) {
        source["instructionPositionEditor"] = {{"phase", name}};
        const auto snapshot = runtime::ParseState(source.dump(), GetCurrentProcessId(), &normal,
            nullptr, nullptr, nullptr, &hints).snapshot;
        Check(snapshot->instructionPositionEditor && snapshot->instructionPositionEditor->phase == phase
            && snapshot->instructionPositionEditor->instructions == hints && snapshot->instructions == normal,
            "Position phases or private help changed ordinary instructions");
        Check(runtime::InputModeForSnapshot(*snapshot) == (phase == InstructionPositionEditorPhase::Editing
            ? runtime::InputMode::InstructionPositionEditor : runtime::InputMode::MenuLocked),
            "A position transition phase enabled movement routing");
        source["instructionPositionEditor"]["phase"] = "changed";
        Check(snapshot->instructionPositionEditor->phase == phase, "An editor phase retained mutable JSON");
    }
    const auto snapshot = runtime::ParseState(PositionEditorPacket().dump(), GetCurrentProcessId(),
        nullptr, nullptr, nullptr, nullptr, &hints).snapshot;
    // Private hints cannot keep a reference to the caller
    hints[0] = "changed";
    Check(snapshot->instructionPositionEditor->instructions[0] == "W: Move Up", "Position help retained a mutable vector");
    source = StateJson(); source["instructionPositionEditor"] = nullptr;
    Check(!runtime::ParseState(source.dump(), GetCurrentProcessId()).snapshot->instructionPositionEditor,
        "A legacy null editor invented an active position session");
}

void PositionEditorSchemaJoinsRoutingAndConsumesTransitionHolds() {
    runtime::Bindings bindings{{2}, {86}, {2, 38}, {1, 40}, {70}, {51}, {52}, {1}, {38}, {40}};
    runtime::NativeInputRouter router(bindings);
    std::set<int> keys{38};
    auto source = PositionEditorPacket(); source["navigationRevision"] = 77;
    const auto state = [&](const char* phase) {
        source["instructionPositionEditor"] = {{"phase", phase}};
        return runtime::ParseState(source.dump(), GetCurrentProcessId()).snapshot;
    };
    const auto poll = [&](const MenuSnapshot& snapshot, std::uint64_t now) {
        return router.Poll(true, snapshot.isOpen, false, [&](int key) { return keys.contains(key); },
            now, snapshot.navigationRevision, snapshot.appearance.quickExit, snapshot.isRootMenu,
            runtime::InputModeForSnapshot(snapshot));
    };
    // Phase changes run through the production schema and router together
    const auto entering = state("entering");
    Check(!poll(*entering, 0) && !poll(*entering, 99), "Entering must consume held position inputs");
    const auto editing = state("editing");
    Check(!poll(*editing, 100) && !poll(*editing, 125), "Editing replayed a movement held during entry");
    keys.clear(); poll(*editing, 150); keys = {38};
    const auto action = poll(*editing, 150);
    Check(action == runtime::Input::MoveInstructionsUp, "A fresh exact movement failed after phase entry");
    const auto command = Json::parse(runtime::CommandJson(42, 1, *action, editing->navigationRevision));
    Check(command.at("navigationRevision") == 77, "Joined position input lost the controller epoch");
    keys = {86};
    const auto exiting = state("exiting");
    Check(!poll(*exiting, 200), "Exiting allowed a second position finish or movement");
    auto parent = StateJson(); parent["isOpen"] = true; parent["isRootMenu"] = false; parent["navigationRevision"] = 78;
    const auto resumed = runtime::ParseState(parent.dump(), GetCurrentProcessId()).snapshot;
    Check(!poll(*resumed, 300) && !poll(*resumed, 400), "An editor exit replayed as ordinary Back");
    keys.clear(); poll(*resumed, 401); keys = {70};
    Check(poll(*resumed, 401) == runtime::Input::Select, "The resumed parent retained position routing");
}

void PositionEditorRejectsUnsupportedPhasesAndConflictingContexts() {
    for (const Json& editor : {Json(true), Json(1), Json("editing"), Json::array(), Json::object(),
        Json{{"phase", nullptr}}, Json{{"phase", false}}, Json{{"phase", "unknown"}},
        Json{{"phase", "editing"}, {"step", 1}}}) {
        auto source = PositionEditorPacket(); source["instructionPositionEditor"] = editor;
        Reject([&] { runtime::ParseState(source.dump(), GetCurrentProcessId()); });
    }
    // An editor cannot share a normal page or another modal transition
    for (int conflict = 0; conflict < 6; ++conflict) {
        auto source = PositionEditorPacket();
        if (conflict == 0) source["isOpen"] = false;
        if (conflict == 1) source["isRootMenu"] = true;
        if (conflict == 2) source["rows"] = StateJson().at("rows");
        if (conflict == 3) source["appearance"]["instructionsVisible"] = false;
        if (conflict == 4) source["widthEditor"] = {{"step", 1}};
        if (conflict == 5) source["menuUnlock"] = "pause";
        Reject([&] { runtime::ParseState(source.dump(), GetCurrentProcessId()); });
    }
}

void PositionBindingsRemainExactAndRejectAliasesOrIncompleteSubsets() {
    auto source = BindingsJson();
    const auto legacy = runtime::ParseBindings(source.dump());
    Check(legacy.moveUp.empty() && legacy.moveDown.empty(), "Legacy bindings invented movement controls");
    source["up"] = {2, 38}; source["down"] = {1, 40};
    source["moveUp"] = {38}; source["moveDown"] = {40};
    source["previousChoice"] = {51}; source["nextChoice"] = {52}; source["attack"] = {1};
    auto bindings = runtime::ParseBindings(source.dump());
    Check(bindings.moveUp == std::vector<int>{38} && bindings.moveDown == std::vector<int>{40}, "Exact slot bindings changed");
    for (const auto* field : {"moveUp", "moveDown"}) {
        for (const Json& invalid : {Json(nullptr), Json(1), Json("38"), Json::array({38, 38}),
            Json::array({0}), Json::array({255}), Json::array({true}), Json::array({38.0})}) {
            auto malformed = source; malformed[field] = invalid;
            Reject([&] { runtime::ParseBindings(malformed.dump()); });
        }
    }
    // Aim and attack remain excluded from exact movement capability
    for (int conflict = 0; conflict < 6; ++conflict) {
        auto malformed = source;
        if (conflict == 0) malformed["moveUp"] = {2};
        if (conflict == 1) malformed["moveDown"] = {1};
        if (conflict == 2) malformed["moveUp"] = {39};
        if (conflict == 3) malformed["moveDown"] = {39};
        if (conflict == 4) malformed["moveUp"] = {40};
        if (conflict == 5) malformed["moveDown"] = {38};
        Reject([&] { runtime::ParseBindings(malformed.dump()); });
    }
}

void PositionCommandsRequireTheirCapturedNavigationEpoch() {
    for (auto action : {runtime::Input::MoveInstructionsUp, runtime::Input::MoveInstructionsDown,
        runtime::Input::MoveInstructionsLeft, runtime::Input::MoveInstructionsRight,
        runtime::Input::FinishInstructionPositionEditor}) {
        Reject([&] { runtime::CommandJson(42, 1, action); });
        // Both unsigned epoch boundaries must survive command encoding
        for (auto revision : {std::uint64_t{0}, std::numeric_limits<std::uint64_t>::max()}) {
            const auto value = Json::parse(runtime::CommandJson(42, 1, action, revision));
            Check(value.at("navigationRevision") == revision && value.at("processId") == 42
                && value.at("input") == runtime::InputName(action), "Position command lost its captured epoch or identity");
        }
    }
}
}
