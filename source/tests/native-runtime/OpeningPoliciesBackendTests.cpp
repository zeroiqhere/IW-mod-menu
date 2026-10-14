#include "TestSupport.h"
#include "ControlLabels.h"
#include "OpeningPolicyScope.h"
#include <fstream>
#include <map>

namespace iw::overlay::tests {
void BackendOpeningPoliciesPreserveIndependentOwnersAndGlobalCommandScopes(const std::filesystem::path &directory) {
    if (!std::filesystem::exists(directory / "quick-controls-default.json")) return;
    const runtime::Bindings bindings{{2}, {86, 6}, {2, 38}, {1, 40}, {70}, {51}, {52}, {1},
        {38}, {40}, {71}, {69}, {16}, {17}, {32}};
    const auto labels = runtime::DescribeOpeningActions(bindings);
    using Phase = MenuPreferenceRefreshPhase;
    struct Expected {
        const char *name;
        std::uint64_t global, quick;
        std::vector<OpeningAction> actions;
        bool disabled;
        std::optional<Phase> phase;
    };
    const std::vector<OpeningAction> initial{OpeningAction::Aim, OpeningAction::SecondaryOffhand};
    const std::vector<OpeningAction> use{OpeningAction::Aim, OpeningAction::Use};
    const std::vector<OpeningAction> trimmed{OpeningAction::Aim};
    const Expected profiles[] = {
        {"default", 1, 1, initial, false, {}}, {"menu-draft", 1, 1, initial, false, {}},
        {"menu-applied", 1, 1, initial, false, Phase::Cooldown}, {"quick-slot2", 1, 1, initial, false, {}},
        {"use-pending", 2, 2, use, false, Phase::Pending}, {"use-pre-refresh", 2, 2, use, false, Phase::Pending},
        {"use-redraw", 2, 2, use, false, Phase::Cooldown}, {"use-pre-unlock", 2, 2, use, false, Phase::Cooldown},
        {"use-unlocked", 2, 2, use, false, {}}, {"duplicate-pending", 2, 2, use, false, Phase::Pending},
        {"none-pending", 3, 3, trimmed, false, Phase::Pending}, {"none-unlocked", 3, 3, trimmed, false, {}},
        {"disable-pending", 4, 3, trimmed, true, Phase::Pending}, {"disable-redraw", 4, 3, trimmed, true, Phase::Cooldown},
        {"disable-unlocked", 4, 3, trimmed, true, {}}, {"reenable-pending", 5, 3, trimmed, false, Phase::Pending},
        {"reenable-unlocked", 5, 3, trimmed, false, {}}, {"hidden", 5, 3, trimmed, false, {}},
        {"closed", 5, 3, trimmed, false, {}}, {"selector-opening-wait", 1, 1, initial, false, Phase::Selector},
        {"selector-opening-unlocked", 1, 1, initial, false, {}}};
    struct Identity { std::uint64_t navigation, menu, sequence; std::size_t selected, count; };
    // These fixed producer intents stay independent from the bytes being parsed
    const std::map<std::string, Identity> identities{
        {"default", {8, 6, 6, 0, 16}}, {"menu-draft", {8, 6, 7, 0, 16}},
        {"menu-applied", {9, 7, 8, 0, 16}}, {"quick-slot2", {11, 8, 11, 1, 17}},
        {"use-pending", {21, 8, 22, 12, 17}}, {"use-pre-refresh", {21, 8, 22, 12, 17}},
        {"use-redraw", {21, 9, 22, 12, 17}}, {"use-pre-unlock", {21, 9, 22, 12, 17}},
        {"use-unlocked", {21, 9, 22, 12, 17}}, {"duplicate-pending", {34, 9, 36, 9, 17}},
        {"none-pending", {44, 10, 47, 3, 17}}, {"none-unlocked", {44, 11, 47, 3, 17}},
        {"disable-pending", {58, 15, 60, 5, 12}}, {"disable-redraw", {58, 16, 60, 5, 12}},
        {"disable-unlocked", {58, 16, 60, 5, 12}}, {"reenable-pending", {58, 16, 61, 5, 12}},
        {"reenable-unlocked", {58, 17, 61, 5, 12}}, {"hidden", {58, 17, 61, 5, 12}},
        {"closed", {59, 18, 62, 5, 12}}, {"selector-opening-wait", {10, 8, 9, 0, 16}},
        {"selector-opening-unlocked", {10, 8, 9, 0, 16}}};
    std::map<std::string, runtime::ParsedState> states;
    for (const auto &expected : profiles) {
        const auto path = directory / (std::string("quick-controls-") + expected.name + ".json");
        Check(std::filesystem::is_regular_file(path) && std::filesystem::file_size(path) <= runtime::MaximumFileBytes,
              "An actual dual-policy packet is missing or oversized");
        std::ifstream file(path, std::ios::binary);
        const std::string bytes{std::istreambuf_iterator<char>(file), std::istreambuf_iterator<char>()};
        auto parsed = runtime::ParseState(bytes, 42, nullptr, nullptr, nullptr, nullptr, nullptr,
            8, nullptr, nullptr, &labels);
        const auto &state = *parsed.snapshot;
        const auto &identity = identities.at(expected.name);
        Check(state.navigationRevision == identity.navigation && state.menuRevision == identity.menu
              && parsed.lastSequence == identity.sequence && state.selectedIndex == identity.selected
              && state.rows.size() == identity.count,
              "Actual Quick editor action context, acknowledgement or retained cursor changed");
        const auto pair = state.openingPolicies;
        Check(pair && pair->revision == expected.global && pair->main == MainOpeningControls{1,
                  {OpeningAction::Aim, OpeningAction::Melee}} && state.mainOpeningControls == pair->main
              && pair->quick == MainOpeningControls{expected.quick, expected.actions}
              && pair->quickMenuDisabled == expected.disabled && state.menuPreferenceRefresh == expected.phase,
              "Actual dual tuple, preference, mirror or independent epochs changed");
        const bool open = std::string_view(expected.name) != "hidden" && std::string_view(expected.name) != "closed";
        Check(state.isOpen == open && runtime::InputModeForSnapshot(state) ==
              (expected.phase ? runtime::InputMode::MenuLocked : runtime::InputMode::Menu),
              "Configuration activated a Quick mode or lost its ordinary lock");
        // Commands capture the actual global owner even while rows still show its prior checkboxes
        const auto command = Json::parse(runtime::CommandJson(42, parsed.lastSequence + 1,
            runtime::Input::Select, state.navigationRevision, runtime::ObservedOpeningControlsRevision(state)));
        Check(command.at("openingControlsRevision") == expected.global
              && command.at("navigationRevision") == state.navigationRevision,
              "Actual command captured a tuple or stale editor scope");
        states.emplace(expected.name, std::move(parsed));
    }
    const auto row = [&](const char *name, OpeningAction action) -> const MenuRow & {
        for (const auto &item : states.at(name).snapshot->rows) if (item.openingAction == action) return item;
        throw std::runtime_error("A real Quick editor action row is missing");
    };
    Check(row("use-pending", OpeningAction::Use).value == "OFF"
          && row("use-pre-refresh", OpeningAction::SecondaryOffhand).value == "ON"
          && row("use-redraw", OpeningAction::Use).value == "ON"
          && row("use-redraw", OpeningAction::Use).label == "F",
          "Actual cached radio rows or exact saved-key readback changed");
    const auto preference = [&](const char *name) -> std::string {
        for (const auto &item : states.at(name).snapshot->rows)
            if (item.label == "Disable Quick Menu") return item.value;
        throw std::runtime_error("A real Quick preference row is missing");
    };
    Check(preference("disable-pending") == "OFF" && preference("disable-redraw") == "ON"
          && preference("reenable-pending") == "ON" && preference("reenable-unlocked") == "OFF",
          "Actual cached Quick preference rows changed before their redraw");
    for (const auto *prefix : {"use", "disable"}) {
        const auto &pending = *states.at(std::string(prefix) + "-pending").snapshot;
        const auto &redraw = *states.at(std::string(prefix) + "-redraw").snapshot;
        const auto &unlocked = *states.at(std::string(prefix) + "-unlocked").snapshot;
        Check(pending.menuRevision && redraw.menuRevision == *pending.menuRevision + 1
              && unlocked.menuRevision == redraw.menuRevision
              && pending.navigationRevision == redraw.navigationRevision
              && unlocked.navigationRevision == redraw.navigationRevision,
              "Actual policy refresh changed its action epoch or rebuilt more than once");
    }
    const auto &wait = *states.at("selector-opening-wait").snapshot;
    const auto &ready = *states.at("selector-opening-unlocked").snapshot;
    Check(wait.menuOpening && wait.menuOpening->elapsedMilliseconds == 550 && ready.menuOpening
          && ready.menuOpening->elapsedMilliseconds == 800 && wait.menuRevision == ready.menuRevision,
          "The no-redraw selector wait changed opening age or structural identity");
    const auto fallbackPath = directory / "quick-controls-main-only.json";
    std::ifstream fallbackFile(fallbackPath, std::ios::binary);
    const std::string fallbackBytes{std::istreambuf_iterator<char>(fallbackFile), std::istreambuf_iterator<char>()};
    auto partial = labels;
    for (unsigned id : {0u, 1u, 7u, 9u, 10u, 11u, 12u}) partial[id].clear();
    const auto fallback = runtime::ParseState(fallbackBytes, 42, nullptr, nullptr, nullptr, nullptr,
        nullptr, 8, nullptr, nullptr, &partial);
    Check(fallback.snapshot->navigationRevision == 8 && fallback.snapshot->menuRevision == 6
          && fallback.lastSequence == 6 && fallback.snapshot->selectedIndex == 0 && fallback.snapshot->rows.size() == 16
          && !fallback.snapshot->openingPolicies && fallback.snapshot->mainOpeningControls
          && runtime::ObservedOpeningControlsRevision(*fallback.snapshot) == 1,
          "Missing Quick proof removed Main or fabricated a dual owner");
}
} // namespace iw::overlay::tests
