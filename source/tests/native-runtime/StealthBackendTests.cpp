#include "../../native/shared/MenuRowCapacity.h"
#include "TestSupport.h"
#include <fstream>
#include <map>
#include <set>

namespace iw::overlay::tests {
namespace {
std::shared_ptr<const MenuSnapshot> Read(const std::filesystem::path &directory, const std::string &name,
                                         std::uint64_t sequence) {
    const auto path = directory / (name + ".json");
    Check(std::filesystem::is_regular_file(path) &&
              std::filesystem::file_size(path) <= runtime::MaximumFileBytes,
          "An actual Stealth profile is missing or oversized");
    std::ifstream file(path, std::ios::binary);
    const std::string bytes{std::istreambuf_iterator<char>(file), std::istreambuf_iterator<char>()};
    const auto parsed = runtime::ParseState(bytes, 42);
    Check(parsed.lastSequence == sequence, "Actual Stealth acknowledgement sequence changed");
    return parsed.snapshot;
}
void Rows(const MenuSnapshot &state, bool counter, bool stealth, bool textColor = false) {
    const char *labels[] = {
        "Design",       "Bool Display",       "Bool Box Location", "Scroll Animation Time (ms)",
        "Quick Exit",   "Disable Quick Menu", "Spotlight Cursor",  "Colored Cursor",
        "Large Cursor", "Option Counter",     "Stealth UI",        "No Target While In Menu"};
    const std::string values[] = {"Apparition",
                                  textColor ? "Text Color" : "Boxes",
                                  "Right",
                                  "120 ms",
                                  "OFF",
                                  "UNAVAILABLE",
                                  "ON",
                                  "ON",
                                  "OFF",
                                  counter ? "ON" : "OFF",
                                  stealth ? "ON" : "OFF",
                                  "UNAVAILABLE"};
    Check(state.rows.size() == std::size(labels), "Actual compact preferences lost a source row");
    for (std::size_t index = 0; index < std::size(labels); ++index) {
        // These fixed packets retain their old row payload, while current admission is tested from fresh exports
        Check(state.rows[index].label == labels[index] && state.rows[index].value == values[index] &&
                  (index == 9 || state.rows[index].enabled == (index != 5 && index != 11)),
              "Actual compact preference label, value or capability changed");
    }
}
void Context(const MenuSnapshot &state, const MenuAppearance &appearance, std::size_t selected,
             std::uint64_t navigation, std::uint64_t menu) {
    Check(state.isOpen && !state.isRootMenu && state.title == "Menu Preferences" &&
              state.context == "PREVIEW" && state.accent == "#3998FE" && state.appearance == appearance &&
              state.selectedIndex == selected && state.navigationRevision == navigation &&
              state.menuRevision == menu && !state.smoothRainbowTheme && !state.widthEditor &&
              !state.instructionPositionEditor && !state.menuPositionEditor && !state.menuCredits &&
              !state.menuUnlock && !state.menuDesignTransition && !state.menuDesignFooterDesign &&
              !state.menuOpenTransition && !state.presentationFocusRevision,
          "Actual compact preferences changed context, flags or exact epochs");
}
} // namespace
void BackendStealthStatesPreserveActualTransitionsCapacityAndFreshInput(
    const std::filesystem::path &directory) {
    struct Group {
        const char *prefix;
        bool rebuild;
        std::size_t selected;
        std::uint64_t navigation, sequence, menu, opening;
        bool oldCounter, newCounter, newStealth;
    };
    const Group groups[] = {{"stealth-refresh", false, 10, 23, 22, 6, 0, false, false, true},
                            {"stealth-rebuild", true, 10, 23, 23, 7, 3, true, false, true},
                            {"counter-on", true, 9, 22, 21, 6, 2, false, true, false},
                            {"counter-off", true, 9, 22, 22, 7, 3, true, false, false}};
    // Each group retains one cursor identity through its real admission, redraw and unlock packets
    for (const auto &group : groups) {
        std::vector<std::shared_ptr<const MenuSnapshot>> states;
        const char *refresh[] = {"pending", "pre-refresh", "cooldown", "pre-unlock", "unlocked"};
        const char *rebuild[] = {"rebuild", "pre-rebuild", "cooldown", "pre-unlock", "unlocked"};
        for (std::size_t phase = 0; phase < 5; ++phase) {
            const auto state =
                Read(directory,
                     std::string(group.prefix) + "-" + (group.rebuild ? rebuild[phase] : refresh[phase]),
                     group.sequence);
            MenuAppearance expected;
            expected.optionCounter = phase < 2 ? group.oldCounter : group.newCounter;
            if (phase >= 2 && group.newStealth) {
                expected.stealthUI = true;
                expected.booleanDisplay = "Text";
            }
            Context(*state, expected, group.selected, group.navigation, group.menu + (phase >= 2 ? 1 : 0));
            Rows(*state, expected.optionCounter, expected.stealthUI);
            const auto rebuildPhase = !group.rebuild || phase == 4
                                          ? std::nullopt
                                          : std::optional{phase < 2 ? MenuPreferenceRebuildPhase::Rebuild
                                                                    : MenuPreferenceRebuildPhase::Cooldown};
            const auto refreshPhase = group.rebuild || phase == 4
                                          ? std::nullopt
                                          : std::optional{phase < 2 ? MenuPreferenceRefreshPhase::Pending
                                                                    : MenuPreferenceRefreshPhase::Cooldown};
            Check(state->menuPreferenceRebuild == rebuildPhase &&
                      state->menuPreferenceRefresh == refreshPhase &&
                      state->menuOpening ==
                          (!group.rebuild || phase < 2
                               ? std::nullopt
                               : std::optional{MenuOpening{group.opening, phase == 2   ? 0
                                                                          : phase == 3 ? 199
                                                                                       : 200}}) &&
                      runtime::InputModeForSnapshot(*state) ==
                          (phase < 4 ? runtime::InputMode::MenuLocked : runtime::InputMode::Menu),
                  "Actual compact transition crossed the wrong redraw or input boundary");
            Check(MenuRowsShown(state->appearance, state->rows.size()) == (expected.stealthUI       ? 5
                                                                           : expected.optionCounter ? 8
                                                                                                    : 10),
                  "Actual compact transition used a guessed row capacity");
            states.push_back(state);
        }
        runtime::NativeInputRouter router(runtime::ParseBindings(BindingsJson().dump()));
        std::set<int> keys;
        const auto poll = [&](std::uint64_t now, const MenuSnapshot &state) {
            return router.Poll(
                true, true, false, [&](int key) { return keys.contains(key); }, now, state.navigationRevision,
                false, false, runtime::InputModeForSnapshot(state), 120, state.menuRevision);
        };
        poll(0, *states[0]);
        keys = {70};
        const std::uint64_t refreshTimes[] = {0, 179, 180, 429, 430}, rebuildTimes[] = {0, 49, 50, 249, 250};
        for (std::size_t phase = 0; phase < 5; ++phase)
            Check(!poll(group.rebuild ? rebuildTimes[phase] : refreshTimes[phase], *states[phase]),
                  "Actual compact pause replayed held Select");
        keys.clear();
        poll(431, *states.back());
        keys = {70};
        Check(poll(431, *states.back()) == runtime::Input::Select,
              "Fresh Select failed after actual compact unlock");
    }
    // Retained opening remains owned during pending refresh, then ends at the actual redraw
    for (int phase = 0; phase < 3; ++phase) {
        const char *names[] = {"pending", "pre-refresh", "cooldown"};
        const auto state = Read(directory, std::string("stealth-retained-opening-") + names[phase], 24);
        MenuAppearance expected;
        if (phase == 2) {
            expected.stealthUI = true;
            expected.booleanDisplay = "Text";
        }
        Context(*state, expected, 10, 25, phase == 2 ? 9 : 8);
        Rows(*state, false, phase == 2);
        Check(state->menuPreferenceRefresh == (phase < 2 ? MenuPreferenceRefreshPhase::Pending
                                                         : MenuPreferenceRefreshPhase::Cooldown) &&
                  !state->menuPreferenceRebuild &&
                  state->menuOpening ==
                      (phase == 2 ? std::nullopt : std::optional{MenuOpening{2, phase == 0 ? 550 : 729}}),
              "Retained opening was canceled before refresh redraw");
    }
    struct Extra {
        const char *name;
        std::size_t selected;
        std::uint64_t navigation, sequence, menu;
        bool stealth, color;
        int elapsed;
    };
    const Extra extras[] = {{"stealth-disabled-unlocked", 10, 23, 23, 8, false, false, -1},
                            {"stealth-boxes-rejected", 1, 26, 27, 8, true, false, -1},
                            {"stealth-text-color", 1, 26, 29, 9, true, true, -1},
                            {"stealth-opening-last", 10, 23, 23, 8, true, false, 399},
                            {"stealth-opening-complete", 10, 23, 23, 8, true, false, -1}};
    for (const auto &extra : extras) {
        const auto state = Read(directory, extra.name, extra.sequence);
        MenuAppearance expected;
        expected.stealthUI = extra.stealth;
        expected.booleanDisplay = extra.color ? "Text Color" : "Text";
        Context(*state, expected, extra.selected, extra.navigation, extra.menu);
        Rows(*state, false, extra.stealth, extra.color);
        Check(!state->menuPreferenceRebuild && !state->menuPreferenceRefresh &&
                  state->menuOpening ==
                      (extra.elapsed < 0 ? std::nullopt : std::optional{MenuOpening{3, extra.elapsed}}),
              "Actual compact reset, rejected Boxes or opening expiry changed modal lifetime");
    }
}
} // namespace iw::overlay::tests
