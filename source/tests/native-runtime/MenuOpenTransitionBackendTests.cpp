#include "TestSupport.h"
#include "ControlLabels.h"
#include <fstream>
#include <map>
#include <set>
#include <tuple>

namespace iw::overlay::tests {
namespace {
std::shared_ptr<const MenuSnapshot> ReadOpenProfile(const std::filesystem::path& directory, const std::string& name,
    const std::vector<std::string>& hints) {
    const auto path = directory / ("menu-initial-open-" + name + ".json");
    Check(std::filesystem::is_regular_file(path), "An actual initial open profile is missing");
    const auto size = std::filesystem::file_size(path);
    Check(size > 0 && size <= runtime::MaximumFileBytes, "An actual initial open profile exceeds protocol bounds");
    std::ifstream input(path, std::ios::binary);
    const std::string bytes{std::istreambuf_iterator<char>(input), std::istreambuf_iterator<char>()};
    // PID 42 belongs to the offline producer; parsing performs no session work
    return runtime::ParseState(bytes, 42, &hints).snapshot;
}
}

void BackendMenuOpenTransitionStatesPreserveActualRedrawsAndIndependentInputReadiness(const std::filesystem::path& directory) {
    struct Profile { const char* name; std::optional<MenuOpenTransitionPhase> phase; int elapsed; };
    const auto rebuild = std::optional{MenuOpenTransitionPhase::Rebuild};
    const auto cooldown = std::optional{MenuOpenTransitionPhase::Cooldown};
    // Healthy and late profiles share one source sequence: first draw R, then a 550 ms input wait
    const Profile roots[] = {{"rebuild", rebuild, -1}, {"pre-draw", rebuild, -1},
        {"drawn", cooldown, 0}, {"pre-unlock", cooldown, 549}, {"input-ready", std::nullopt, 550},
        {"last", std::nullopt, 699}, {"complete", std::nullopt, -1}, {"late", cooldown, 0},
        {"late-pre-unlock", cooldown, 549}, {"late-ready", std::nullopt, 550}};
    auto packet = BindingsJson(); packet["previousChoice"] = {51}; packet["nextChoice"] = {52};
    const auto bindings = runtime::ParseBindings(packet.dump()); const auto hints = runtime::DescribeControls(bindings);
    std::map<std::string, std::shared_ptr<const MenuSnapshot>> states;
    for (const auto& profile : roots) {
        const auto state = ReadOpenProfile(directory, profile.name, hints);
        Check(state->isOpen && state->isRootMenu && state->rows.size() == 8 && state->selectedIndex == 0
            && state->appearance == MenuAppearance{} && state->accent == "#3998FE" && !state->smoothRainbowTheme
            && state->title == "Apparition" && state->context == "PREVIEW" && state->instructions == hints
            && !state->widthEditor && !state->instructionPositionEditor && !state->menuPositionEditor
            && !state->menuCredits && !state->menuUnlock && !state->menuPreferenceRefresh && !state->menuDesignTransition
            && !state->presentationFocusRevision && state->navigationRevision == 1 && state->menuRevision == 2,
            "Actual initial opening changed its ordinary offline root context or exact public epochs");
        Check(state->menuOpenTransition == profile.phase
            && state->menuOpening == (profile.elapsed < 0 ? std::nullopt : std::optional{MenuOpening{1, profile.elapsed}})
            && runtime::InputModeForSnapshot(*state) == (profile.phase ? runtime::InputMode::MenuLocked : runtime::InputMode::Menu),
            "Actual initial opening lost source progress or its separate input readiness");
        const char* labels[] = {"Connect after spawning", "Basic Scripts", "Menu Customization", "Players Menu",
            "All Players Menu", "Messages", "Feature Support", "Exit"};
        for (std::size_t row = 0; row < std::size(labels); ++row) {
            Check(state->rows[row].label == labels[row] && state->rows[row].enabled == (row != 0)
                && state->rows[row].value == (row == 0 ? "UNAVAILABLE" : row == 7 ? "" : ">"),
                "Actual initial root changed an exact row value or offline capability");
        }
        states.emplace(profile.name, state);
    }
    const auto& start = *states.at("rebuild");
    // Clock-only redraw, fade progress and readiness cannot invent cursor or structural revisions
    for (const auto& [name, state] : states) {
        static_cast<void>(name);
        Check(state->navigationRevision == start.navigationRevision && state->menuRevision == start.menuRevision,
            "Actual initial opening changed a public epoch at redraw or readiness");
    }
    struct ChildProfile { const char* name; const char* title; const char* context; std::size_t count, selected;
        std::uint64_t navigation, menu; std::optional<MenuOpenTransitionPhase> phase; bool fade; };
    const ChildProfile children[] = {{"retained-rebuild", "Menu Preferences", "PREVIEW", 12, 8, 23, 8, rebuild, false},
        {"retained-drawn", "Menu Preferences", "PREVIEW", 12, 8, 23, 8, cooldown, true},
        {"empty-rebuild", "Messages", "PREVIEW", 0, 0, 10, 6, rebuild, false},
        {"empty-drawn", "Messages", "PREVIEW", 0, 0, 10, 6, cooldown, false},
        {"single-drawn", "Players Menu", "DISCONNECTED", 1, 0, 8, 6, cooldown, false},
        {"dynamic-drawn", "Players Menu", "DISCONNECTED", 3, 0, 8, 7, cooldown, true},
        {"dynamic-changed", "Players Menu", "DISCONNECTED", 1, 0, 8, 8, cooldown, false}};
    // Retained pages keep their exact title, cursor and local capabilities while reopening
    for (const auto& profile : children) {
        const auto state = ReadOpenProfile(directory, profile.name, hints);
        Check(state->isOpen && !state->isRootMenu && state->title == profile.title && state->context == profile.context
            && state->rows.size() == profile.count && state->selectedIndex == profile.selected
            && state->navigationRevision == profile.navigation && state->menuRevision == profile.menu
            && state->appearance == MenuAppearance{} && state->accent == "#3998FE" && !state->smoothRainbowTheme
            && state->instructions == hints && !state->widthEditor && !state->instructionPositionEditor
            && !state->menuPositionEditor && !state->menuCredits && !state->menuUnlock
            && !state->menuPreferenceRefresh && !state->menuDesignTransition && !state->presentationFocusRevision,
            "Actual retained opening changed its page, cursor, appearance or offline capability context");
        Check(state->menuOpenTransition == profile.phase
            && state->menuOpening == (profile.fade ? std::optional{MenuOpening{2, 0}} : std::nullopt)
            && runtime::InputModeForSnapshot(*state) == runtime::InputMode::MenuLocked,
            "Actual retained opening changed its next fade identity or independent input cooldown");
        if (std::string(profile.title) == "Menu Preferences") {
            const char* labels[] = {"Design", "Bool Display", "Bool Box Location", "Scroll Animation Time (ms)",
                "Quick Exit", "Disable Quick Menu", "Spotlight Cursor", "Colored Cursor", "Large Cursor", "Option Counter", "Stealth UI", "No Target While In Menu"};
            const char* values[] = {"Apparition", "Boxes", "Right", "120 ms", "OFF", "UNAVAILABLE", "ON", "ON", "OFF", "OFF", "OFF", "UNAVAILABLE"};
            for (std::size_t row = 0; row < std::size(labels); ++row) {
                Check(state->rows[row].label == labels[row] && state->rows[row].value == values[row] && state->rows[row].enabled == (row != 5 && row != 11),
                    "Actual retained preference changed an exact row value or local capability");
            }
        } else if (std::string(profile.title) == "Players Menu") {
            const char* labels[] = {"Refresh players", "Friend", "Other"};
            for (std::size_t row = 0; row < state->rows.size(); ++row) {
                Check(state->rows[row].label == labels[row] && state->rows[row].value == (row == 0 ? "" : ">")
                    && !state->rows[row].enabled == (row != 5 && row != 11), "Actual offline roster changed an exact label or advertised a game action");
            }
        }
        states.emplace(profile.name, state);
    }
    for (const auto& [before, after] : {std::pair{"retained-rebuild", "retained-drawn"}, std::pair{"empty-rebuild", "empty-drawn"}}) {
        Check(states.at(before)->navigationRevision == states.at(after)->navigationRevision
            && states.at(before)->menuRevision == states.at(after)->menuRevision,
            "Actual retained redraw changed an unchanged cursor or structural epoch");
    }
    const auto& dynamic = *states.at("dynamic-drawn"); const auto& changed = *states.at("dynamic-changed");
    Check(dynamic.menuRevision && changed.menuRevision == *dynamic.menuRevision + 1
        && changed.navigationRevision == dynamic.navigationRevision && !changed.menuOpening
        && changed.menuOpenTransition == MenuOpenTransitionPhase::Cooldown,
        "Actual roster redraw restarted the input wait or retained stale option opacity");
    // A hidden display owns no active metadata while preserving the drawn owner's retained identity
    const auto hidden = ReadOpenProfile(directory, "focus-hidden", hints);
    const auto& drawn = *states.at("drawn");
    Check(!hidden->isOpen && hidden->isRootMenu && !hidden->menuOpenTransition && !hidden->menuOpening
        && hidden->title == drawn.title && hidden->context == drawn.context && hidden->appearance == drawn.appearance
        && hidden->selectedIndex == drawn.selectedIndex && hidden->navigationRevision == drawn.navigationRevision
        && hidden->menuRevision == drawn.menuRevision && hidden->rows.size() == drawn.rows.size(),
        "Actual focus projection retained invalid open metadata or changed the owner identity");
    for (std::size_t row = 0; row < hidden->rows.size(); ++row) {
        Check(hidden->rows[row].label == drawn.rows[row].label && hidden->rows[row].value == drawn.rows[row].value
            && hidden->rows[row].enabled == drawn.rows[row].enabled, "Actual focus projection changed a retained root row");
    }
    for (const auto& [first, last, ready, redraw] : {std::tuple{"rebuild", "pre-unlock", "input-ready", std::uint64_t{50}},
        std::tuple{"rebuild", "late-pre-unlock", "late-ready", std::uint64_t{1000}}}) {
        runtime::NativeInputRouter router(bindings); std::set<int> held;
        const auto poll = [&](std::uint64_t now, const MenuSnapshot& state) {
            return router.Poll(true, state.isOpen, false, [&](int key) { return held.contains(key); }, now,
                state.navigationRevision, state.appearance.quickExit, state.isRootMenu,
                runtime::InputModeForSnapshot(state), state.appearance.scrollAnimationMilliseconds, state.menuRevision);
        };
        poll(0, *states.at(first)); held = {70}; Check(!poll(redraw + 549, *states.at(last)), "Actual initial cooldown queued held selection");
        Check(!poll(redraw + 550, *states.at(ready)), "Actual initial readiness replayed a selection pressed during the wait");
        held.clear(); poll(redraw + 551, *states.at(ready)); held = {70};
        Check(poll(redraw + 551, *states.at(ready)) == runtime::Input::Select, "Actual initial fade delayed fresh input after readiness");
    }
}
}
