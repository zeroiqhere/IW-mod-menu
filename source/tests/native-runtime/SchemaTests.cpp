#include "TestSupport.h"
#include "ControlLabels.h"
#include <limits>

namespace iw::overlay::tests {

void StateValidSnapshotAndBoundaries() {
    auto source = StateJson();
    auto parsed = runtime::ParseState(source.dump(), GetCurrentProcessId());
    Check(!parsed.snapshot->isOpen && parsed.snapshot->title == "Main Menu"
        && parsed.snapshot->rows.size() == 1 && parsed.snapshot->rows[0].enabled
        && parsed.snapshot->accent == "#3998FE", "Normal state was not retained");
    source["title"] = "Changed";
    Check(parsed.snapshot->title == "Main Menu", "Snapshot retained mutable JSON data");
    source["rows"] = Json::array();
    runtime::ParseState(source.dump(), GetCurrentProcessId());
    source["rows"] = Json::array();
    for (int index = 0; index < 128; ++index) source["rows"].push_back({ {"label", "R"}, {"value", ""}, {"enabled", false} });
    source["selectedIndex"] = 127;
    source["lastSequence"] = std::numeric_limits<unsigned long long>::max();
    parsed = runtime::ParseState(source.dump(), GetCurrentProcessId());
    Check(parsed.snapshot->rows.size() == 128 && parsed.lastSequence == std::numeric_limits<unsigned long long>::max(), "Boundary state was rejected");
}

void StateRejectsWrongIdentityVersionsAndTypes() {
    for (int variation = 0; variation < 9; ++variation) {
        auto source = StateJson();
        if (variation == 0) source["version"] = 2;
        if (variation == 1) source["processId"] = GetCurrentProcessId() + 1;
        if (variation == 2) source["isOpen"] = 1;
        if (variation == 3) source["lastSequence"] = -1;
        if (variation == 4) source["selectedIndex"] = 1;
        if (variation == 5) source["lastSequence"] = 0.5;
        if (variation == 6) source["rows"][0]["enabled"] = "true";
        if (variation == 7) source.erase("title");
        if (variation == 8) source["accent"] = "#zzzzzz";
        Reject([&] { runtime::ParseState(source.dump(), GetCurrentProcessId()); });
    }
    Reject([&] { runtime::ParseState("{", GetCurrentProcessId()); });
    Reject([&] { runtime::ParseState("[]", GetCurrentProcessId()); });
    Reject([&] { runtime::ParseState(StateJson().dump(), 0); });
}

void StateRejectsTextUtf8AndRowBounds() {
    auto source = StateJson();
    source["title"] = std::string(1500, 'A');
    runtime::ParseState(source.dump(), GetCurrentProcessId());
    source["title"] = std::string(1501, 'A');
    Reject([&] { runtime::ParseState(source.dump(), GetCurrentProcessId()); });
    source = StateJson();
    source["notice"] = std::string("bad\0text", 8);
    Reject([&] { runtime::ParseState(source.dump(), GetCurrentProcessId()); });
    source = StateJson();
    source["rows"] = Json::array();
    for (int index = 0; index < 129; ++index) source["rows"].push_back({ {"label", "R"}, {"value", ""}, {"enabled", false} });
    Reject([&] { runtime::ParseState(source.dump(), GetCurrentProcessId()); });
    std::string malformed = StateJson().dump();
    malformed[malformed.find("Main Menu")] = static_cast<char>(0xff);
    Reject([&] { runtime::ParseState(malformed, GetCurrentProcessId()); });
    Reject([&] { runtime::ParseState(std::string(runtime::MaximumFileBytes + 1, ' '), GetCurrentProcessId()); });
}

void StateAcceptsProducerUnicodeNoticeAndRejectsOversizedNotice() {
    auto source = StateJson();
    std::string unicode;
    // StateDocument permits 1500 characters and up to 6000 encoded bytes for notices
    for (int index = 0; index < 1500; ++index) unicode += "\xe2\x98\x83";
    source["notice"] = unicode;
    const auto parsed = runtime::ParseState(source.dump(), GetCurrentProcessId());
    Check(parsed.snapshot->notice == unicode, "A producer-valid Unicode notice was rejected or changed");
    source["notice"] = std::string(6000, 'A');
    runtime::ParseState(source.dump(), GetCurrentProcessId());
    source["notice"] = std::string(6001, 'A');
    Reject([&] { runtime::ParseState(source.dump(), GetCurrentProcessId()); });
    source["notice"] = std::string("bad\0text", 8);
    Reject([&] { runtime::ParseState(source.dump(), GetCurrentProcessId()); });
}

void StateControlHintsAreCopiedWithoutChangingFeatureRows() {
    auto source = StateJson();
    auto hints = runtime::DescribeControls(runtime::ParseBindings(BindingsJson().dump()));
    const auto parsed = runtime::ParseState(source.dump(), GetCurrentProcessId(), &hints);
    Check(parsed.snapshot->instructions[1] == "F: Select", "Snapshot lost the saved select key");
    hints[1] = "Changed";
    Check(parsed.snapshot->instructions[1] == "F: Select", "Snapshot retained mutable control hints");
    Check(parsed.snapshot->rows[0].value == "OFF" && parsed.snapshot->rows[0].enabled,
        "Control help modified feature availability or state");
    const auto legacy = runtime::ParseState(source.dump(), GetCurrentProcessId());
    Check(legacy.snapshot->instructions[1] == "Use: Select", "A missing hint must retain the default action description");
}

void BindingsSchemaRejectsMalformedArrays() {
    runtime::ParseBindings(BindingsJson().dump());
    for (int variation = 0; variation < 5; ++variation) {
        auto source = BindingsJson();
        if (variation == 0) source["version"] = 2;
        if (variation == 1) source["aim"] = "Mouse2";
        if (variation == 2) source["aim"] = {-1};
        if (variation == 3) source["aim"] = {2.5};
        if (variation == 4) source["up"] = Json::array();
        Reject([&] { runtime::ParseBindings(source.dump()); });
    }
}

void BindingsOptionalChoiceGroupsRemainExactAndRejectMalformedKeys() {
    auto source = BindingsJson();
    auto parsed = runtime::ParseBindings(source.dump());
    Check(parsed.previousChoice.empty() && parsed.nextChoice.empty(), "Legacy bindings guessed choice keys");
    source["previousChoice"] = {51};
    source["nextChoice"] = {52};
    parsed = runtime::ParseBindings(source.dump());
    Check(parsed.previousChoice == std::vector<int>{51} && parsed.nextChoice == std::vector<int>{52},
        "Saved choice keys were changed");
    source["nextChoice"] = Json::array();
    parsed = runtime::ParseBindings(source.dump());
    Check(parsed.nextChoice.empty(), "Missing next choice guessed a fallback");
    for (const auto* name : {"previousChoice", "nextChoice"}) {
        for (int variation = 0; variation < 8; ++variation) {
            source = BindingsJson();
            if (variation == 0) source[name] = nullptr;
            if (variation == 1) source[name] = "3";
            if (variation == 2) source[name] = {255};
            if (variation == 3) source[name] = {-1};
            if (variation == 4) source[name] = {51.5};
            if (variation == 5) source[name] = {51, 51};
            if (variation == 6) source[name] = {70};
            if (variation == 7) source[name] = std::vector<int>(17, 51);
            Reject([&] { runtime::ParseBindings(source.dump()); });
        }
    }
}

void NavigationRevisionIsUnsignedAndPreservesTheControllerEpoch() {
    auto source = StateJson();
    Check(runtime::ParseState(source.dump(), GetCurrentProcessId()).snapshot->navigationRevision == 0,
        "A legacy state guessed a navigation epoch");
    source["navigationRevision"] = std::numeric_limits<std::uint64_t>::max();
    Check(runtime::ParseState(source.dump(), GetCurrentProcessId()).snapshot->navigationRevision
        == std::numeric_limits<std::uint64_t>::max(), "Controller navigation epoch was narrowed");
    for (const Json& invalid : {Json(-1), Json(1.5), Json(true), Json("1"), Json(nullptr)}) {
        source["navigationRevision"] = invalid;
        Reject([&] { runtime::ParseState(source.dump(), GetCurrentProcessId()); });
    }
}

void StateAppearanceDefaultsPreserveLegacyPackets() {
    auto source = StateJson();
    const MenuAppearance defaults;
    Check(runtime::ParseState(source.dump(), GetCurrentProcessId()).snapshot->appearance == defaults,
        "An absent appearance changed the source defaults");
    source["appearance"] = nullptr;
    Check(runtime::ParseState(source.dump(), GetCurrentProcessId()).snapshot->appearance == defaults,
        "A null appearance changed the legacy defaults");
}

void StateAppearanceChoicesAreCopiedAndKeepFeatureState() {
    for (const auto* display : {"Boxes", "Text", "Text Color"}) {
        for (const auto* location : {"Right", "Left"}) {
            auto source = StateJson();
            source["appearance"] = {{"booleanDisplay", display}, {"boolBoxLocation", location},
                {"instructionsVisible", false}, {"optionCounter", true}, {"coloredCursor", false},
                {"spotlightCursor", false}, {"largeCursor", true}};
            const auto parsed = runtime::ParseState(source.dump(), GetCurrentProcessId());
            const auto& appearance = parsed.snapshot->appearance;
            Check(appearance.booleanDisplay == display && appearance.boolBoxLocation == location
                && !appearance.instructionsVisible && appearance.optionCounter && !appearance.coloredCursor
                && !appearance.spotlightCursor && appearance.largeCursor,
                "A supported presentation choice was lost");
            source["appearance"]["booleanDisplay"] = "Changed";
            Check(appearance.booleanDisplay == display, "Appearance retained mutable JSON data");
            Check(parsed.snapshot->rows[0].value == "OFF" && parsed.snapshot->rows[0].enabled,
                "Appearance changed confirmed game state");
        }
    }
}

void StateAppearanceRejectsUnsupportedValuesAndTypes() {
    const Json supported = {{"booleanDisplay", "Boxes"}, {"boolBoxLocation", "Right"},
        {"instructionsVisible", true}, {"optionCounter", false}, {"coloredCursor", true},
        {"spotlightCursor", true}, {"largeCursor", false}};
    // Partial packets fail rather than mixing a stale choice with guessed values
    for (auto iterator = supported.begin(); iterator != supported.end(); ++iterator) {
        auto source = StateJson();
        source["appearance"] = supported;
        source["appearance"].erase(iterator.key());
        Reject([&] { runtime::ParseState(source.dump(), GetCurrentProcessId()); });
        source["appearance"] = supported;
        source["appearance"][iterator.key()] = iterator.value().is_boolean() ? Json(1) : Json(false);
        Reject([&] { runtime::ParseState(source.dump(), GetCurrentProcessId()); });
    }
    for (const Json& invalid : {Json(false), Json("Boxes"), Json::array(), Json(1)}) {
        auto source = StateJson();
        source["appearance"] = invalid;
        Reject([&] { runtime::ParseState(source.dump(), GetCurrentProcessId()); });
    }
    for (const auto* field : {"booleanDisplay", "boolBoxLocation"}) {
        for (const auto* invalid : {"", "Unknown", "boxes", "Left ", "Text Color\n"}) {
            auto source = StateJson();
            source["appearance"] = supported;
            source["appearance"][field] = invalid;
            Reject([&] { runtime::ParseState(source.dump(), GetCurrentProcessId()); });
        }
    }
}

void StateQuickExitDefaultsAndBooleanValuesAreCopied() {
    auto source = StateJson();
    Check(!runtime::ParseState(source.dump(), GetCurrentProcessId()).snapshot->appearance.quickExit,
        "An absent appearance enabled quick exit");
    source["appearance"] = nullptr;
    Check(!runtime::ParseState(source.dump(), GetCurrentProcessId()).snapshot->appearance.quickExit,
        "A null legacy appearance enabled quick exit");
    source["appearance"] = {{"booleanDisplay", "Text"}, {"boolBoxLocation", "Left"},
        {"instructionsVisible", false}, {"optionCounter", true}, {"coloredCursor", false},
        {"spotlightCursor", false}, {"largeCursor", true}};
    auto legacy = runtime::ParseState(source.dump(), GetCurrentProcessId()).snapshot;
    Check(!legacy->appearance.quickExit, "An explicit seven-field appearance enabled quick exit");
    for (bool enabled : {false, true}) {
        source["appearance"]["quickExit"] = enabled;
        const auto parsed = runtime::ParseState(source.dump(), GetCurrentProcessId()).snapshot;
        Check(parsed->appearance.quickExit == enabled, "A boolean quick-exit preference was lost");
        auto unchanged = parsed->appearance;
        unchanged.quickExit = false;
        Check(unchanged == legacy->appearance && parsed->isRootMenu == legacy->isRootMenu
            && parsed->rows[0].value == legacy->rows[0].value && parsed->rows[0].enabled == legacy->rows[0].enabled
            && parsed->selectedIndex == legacy->selectedIndex && parsed->navigationRevision == legacy->navigationRevision,
            "Quick exit changed unrelated presentation, navigation, or feature state");
        // Later producer mutations cannot alter a published immutable snapshot
        source["appearance"]["quickExit"] = !enabled;
        Check(parsed->appearance.quickExit == enabled, "Quick exit retained mutable JSON data");
    }
}

void StateQuickExitRejectsNonBooleanValues() {
    for (const Json& invalid : {Json(nullptr), Json(0), Json(1), Json(0.5), Json("false"), Json::array(), Json::object()}) {
        auto source = StateJson();
        source["appearance"] = {{"booleanDisplay", "Boxes"}, {"boolBoxLocation", "Right"},
            {"instructionsVisible", true}, {"optionCounter", false}, {"coloredCursor", true},
            {"spotlightCursor", true}, {"largeCursor", false}, {"quickExit", invalid}};
        // Presence requires an actual JSON boolean rather than truth-like text or numbers
        Reject([&] { runtime::ParseState(source.dump(), GetCurrentProcessId()); });
    }
}

void StateRootMenuIdentityDefaultsAndValuesAreCopied() {
    auto source = StateJson();
    Check(runtime::ParseState(source.dump(), GetCurrentProcessId()).snapshot->isRootMenu,
        "A legacy producer lost its conservative root default");
    for (bool root : {false, true}) {
        source["isRootMenu"] = root;
        // An explicit page flag remains authoritative even when its title resembles the root
        source["title"] = "Apparition";
        const auto parsed = runtime::ParseState(source.dump(), GetCurrentProcessId()).snapshot;
        Check(parsed->isRootMenu == root && !parsed->appearance.quickExit
            && parsed->rows[0].value == "OFF" && parsed->rows[0].enabled,
            "Root identity was inferred from a title or changed other state");
        source["isRootMenu"] = !root;
        Check(parsed->isRootMenu == root, "Root identity retained mutable JSON data");
    }
}

void StateRootMenuIdentityRejectsNonBooleanValues() {
    for (const Json& invalid : {Json(nullptr), Json(0), Json(1), Json(0.5), Json("true"), Json::array(), Json::object()}) {
        auto source = StateJson(); source["isRootMenu"] = invalid;
        Reject([&] { runtime::ParseState(source.dump(), GetCurrentProcessId()); });
    }
}

void StateQuickExitControlHintsUseConfirmedRootIdentity() {
    auto source = StateJson();
    std::vector<std::string> normal{"Scroll", "Use", "Back / close"};
    std::vector<std::string> nested{"Scroll", "Use", "Tap back / hold close"};
    std::vector<std::string> root{"Scroll", "Use", "Close"};
    auto parse = [&] { return runtime::ParseState(source.dump(), GetCurrentProcessId(), &normal, &nested, &root).snapshot; };
    Check(parse()->instructions == normal, "Legacy appearance did not retain normal control hints");
    source["appearance"] = {{"booleanDisplay", "Boxes"}, {"boolBoxLocation", "Right"},
        {"instructionsVisible", true}, {"optionCounter", false}, {"coloredCursor", true},
        {"spotlightCursor", true}, {"largeCursor", false}, {"quickExit", true}};
    source["isRootMenu"] = false;
    Check(parse()->instructions == nested, "Nested quick exit lost its tap-back and hold-close hints");
    source["isRootMenu"] = true;
    auto copied = parse();
    Check(copied->instructions == root, "Root quick exit lost its immediate close hint");
    root[2] = "Changed";
    Check(copied->instructions[2] == "Close", "Quick-exit help retained a mutable vector");
    Check(runtime::ParseState(source.dump(), GetCurrentProcessId(), &normal, &nested).snapshot->instructions == normal,
        "A missing root hint variant selected unrelated nested help");
    source["isRootMenu"] = false;
    Check(runtime::ParseState(source.dump(), GetCurrentProcessId(), &normal, nullptr, &root).snapshot->instructions == normal,
        "A missing nested hint variant selected unrelated root help");
    source["appearance"]["quickExit"] = false;
    Check(parse()->instructions == normal, "Disabled quick exit kept the quick-exit hint variant");
    const auto defaults = runtime::ParseState(source.dump(), GetCurrentProcessId());
    Check(defaults.snapshot->instructions[1] == "Use: Select", "Absent hints changed the portable defaults");
}

void CloseCommandsRequireTheExactNavigationEpoch() {
    for (std::uint64_t epoch : {std::uint64_t{0}, std::numeric_limits<std::uint64_t>::max()}) {
        auto packet = Json::parse(runtime::CommandJson(GetCurrentProcessId(), 9, runtime::Input::Close, epoch));
        Check(packet.at("input") == "close" && packet.at("navigationRevision").get<std::uint64_t>() == epoch
            && packet.at("sequence") == 9 && packet.at("processId") == GetCurrentProcessId(),
            "Close lost its exact navigation epoch or command identity");
    }
    // Every Close must describe the snapshot that gave rise to its held gesture
    Reject([&] { runtime::CommandJson(GetCurrentProcessId(), 9, runtime::Input::Close); });
    for (auto input : {runtime::Input::Back, runtime::Input::Select,
        runtime::Input::Up, runtime::Input::Down, runtime::Input::PreviousChoice, runtime::Input::NextChoice}) {
        const auto legacy = Json::parse(runtime::CommandJson(GetCurrentProcessId(), 9, input));
        const auto withEpoch = Json::parse(runtime::CommandJson(GetCurrentProcessId(), 9, input, 12));
        Check(legacy == withEpoch && legacy.size() == 4 && !legacy.contains("navigationRevision"),
            "A navigation epoch changed a legacy non-close command");
    }
}

void CommandSchemaPreservesUnsignedSequence() {
    auto maximum = std::numeric_limits<unsigned long long>::max();
    const auto command = Json::parse(runtime::CommandJson(GetCurrentProcessId(), maximum, runtime::Input::Back));
    Check(command.at("sequence").get<unsigned long long>() == maximum
        && command.at("input") == "back" && command.at("processId") == GetCurrentProcessId(), "Command schema lost its identity or sequence");
    Check(Json::parse(runtime::ReadyJson(GetCurrentProcessId())).at("ready") == true, "Ready schema is incorrect");
}

void RendererStatusSchemaPreservesBoundsAndIdentity() {
    RendererStatus status;
    status.initialized = true;
    status.drawnFrames = std::numeric_limits<std::uint64_t>::max();
    status.faults = std::numeric_limits<std::uint64_t>::max();
    status.featureLevel = std::numeric_limits<std::uint32_t>::max();
    status.width = std::numeric_limits<std::uint32_t>::max();
    status.height = std::numeric_limits<std::uint32_t>::max();
    std::string bytes = runtime::RendererStatusJson(GetCurrentProcessId(), status);
    auto packet = Json::parse(bytes);
    Check(bytes.size() < 512 && packet.size() == 8, "Renderer status packet exceeded its fixed field bounds");
    Check(packet.at("version") == ProtocolVersion && packet.at("processId") == GetCurrentProcessId()
        && packet.at("initialized") == true, "Renderer status lost protocol identity");
    Check(packet.at("drawnFrames").get<std::uint64_t>() == status.drawnFrames
        && packet.at("faults").get<std::uint64_t>() == status.faults
        && packet.at("featureLevel").get<std::uint32_t>() == status.featureLevel
        && packet.at("width").get<std::uint32_t>() == status.width
        && packet.at("height").get<std::uint32_t>() == status.height, "Renderer status lost numeric values");
}

void RuntimeErrorSchemaIsBoundedAndHasOnlyPublicFields() {
    auto processId = std::numeric_limits<DWORD>::max();
    std::string bytes = runtime::RuntimeErrorJson(processId);
    const auto packet = Json::parse(bytes);
    Check(packet.size() == 3 && bytes.size() < 512, "Runtime error packet exceeded its fixed field bounds");
    Check(packet.at("version") == ProtocolVersion && packet.at("processId") == processId,
        "Runtime error packet lost protocol identity");
    const auto message = packet.at("error").get<std::string>();
    Check(!message.empty() && message.size() <= 1500
        && message == "Overlay runtime worker stopped after a protocol or IO failure",
        "Runtime error message was not the bounded public diagnostic");
}
}
