#include "TestSupport.h"
#include "ControlLabels.h"
#include "../../native/shared/MenuRowCapacity.h"
#include <fstream>
#include <map>
#include <set>

namespace iw::overlay::tests {
void BackendMenuOpeningStatesPreserveActualControllerEventsAndInputIndependence(const std::filesystem::path& directory) {
    struct Profile { const char* name; int elapsed; bool cooldown, eight; };
    // Profiles come from the actual controller clock at entry, progress, cancellation and expiry
    const Profile profiles[] = {{"start", 0, true, false}, {"progress", 100, true, false},
        {"input-ready", 200, false, false}, {"last", 899, false, false}, {"complete", -1, false, false},
        {"draft", 200, false, false}, {"same", 200, true, false}, {"scroll", -1, false, false},
        {"late", 0, false, false}, {"eight-start", 0, true, true}, {"eight-last", 699, false, true}, {"eight-complete", -1, false, true}};
    auto packet = BindingsJson(); packet["previousChoice"] = {51}; packet["nextChoice"] = {52};
    const auto bindings = runtime::ParseBindings(packet.dump());
    const auto hints = runtime::DescribeControls(bindings);
    std::map<std::string, std::shared_ptr<const MenuSnapshot>> states;
    for (const auto& profile : profiles) {
        const std::string name(profile.name);
        const auto path = directory / ("menu-opening-" + name + ".json");
        Check(std::filesystem::is_regular_file(path), "An actual opening profile is missing");
        const auto size = std::filesystem::file_size(path);
        Check(size > 0 && size <= runtime::MaximumFileBytes, "An actual opening profile exceeds protocol bounds");
        std::ifstream input(path, std::ios::binary);
        const std::string bytes{std::istreambuf_iterator<char>(input), std::istreambuf_iterator<char>()};
        // Parsing owns each exported packet without contacting a process or dispatching a feature
        const auto state = runtime::ParseState(bytes, 42, &hints).snapshot;
        MenuAppearance expected; expected.design = profile.eight ? MenuDesign::Native : MenuDesign::Classic;
        expected.coloredCursor = false; expected.optionCounter = profile.eight;
        Check(state->appearance == expected && state->isOpen && !state->isRootMenu && state->context == "PREVIEW"
            && state->title == "Menu Preferences" && state->accent == "#3998FE" && !state->smoothRainbowTheme
            && state->instructions == hints && !state->widthEditor && !state->instructionPositionEditor
            && !state->menuPositionEditor && !state->menuCredits && !state->menuUnlock && !state->menuPreferenceRefresh
            && !state->presentationFocusRevision && !state->menuOpenTransition && state->selectedIndex == (name == "scroll" ? 1u : 0u),
            "An actual opening changed its ordinary offline context or independent appearance");
        Check(state->menuOpening == (profile.elapsed < 0 ? std::nullopt : std::optional{MenuOpening{profile.eight ? 3u : 2u, profile.elapsed}})
            && state->menuDesignTransition == (profile.cooldown ? std::optional{MenuDesignTransitionPhase::Cooldown} : std::nullopt)
            && runtime::InputModeForSnapshot(*state) == (profile.cooldown ? runtime::InputMode::MenuLocked : runtime::InputMode::Menu),
            "Actual opening progress changed its event identity or independent input lock");
        const char* labels[] = {"Design", "Bool Display", "Bool Box Location", "Scroll Animation Time (ms)",
            "Quick Exit", "Disable Quick Menu", "Spotlight Cursor", "Colored Cursor", "Large Cursor", "Option Counter", "Stealth UI", "No Target While In Menu"};
        const std::string values[] = {profile.eight || name == "draft" ? "Native" : "Classic", "Boxes", "Right", "120 ms",
            "OFF", "UNAVAILABLE", "ON", "OFF", "OFF", profile.eight ? "ON" : "OFF", "OFF", "UNAVAILABLE"};
        Check(state->rows.size() == std::size(labels), "Actual opening lost an exact preference row");
        for (std::size_t row = 0; row < std::size(labels); ++row) {
            // The fixed Classic packet predates the current rejected-callback row contract
            Check(state->rows[row].label == labels[row] && state->rows[row].value == values[row]
                && (row == 7 || state->rows[row].enabled == (row != 5 && row != 11)),
                "Actual opening changed row values or design-specific local capabilities");
        }
        Check(MenuRowsShown(state->appearance, state->rows.size()) == (profile.eight ? 8u : 10u)
            && MenuOpeningDurationMilliseconds(state->appearance, state->rows.size()) == (profile.eight ? 700 : 900),
            "Actual opening duration differs from source visible capacity");
        states.emplace(name, state);
    }
    // Presentation progress preserves both public epochs; accepted cursor movement only changes navigation
    const auto& start = *states.at("start");
    for (const auto* name : {"progress", "input-ready", "last", "complete", "draft", "same", "late"}) {
        const auto& state = *states.at(name);
        Check(state.navigationRevision == start.navigationRevision && state.menuRevision == start.menuRevision,
            "Opening progress, draft, re-selection, late draw or completion invalidated an unchanged public epoch");
    }
    const auto& scroll = *states.at("scroll");
    Check(scroll.menuRevision == start.menuRevision && scroll.navigationRevision == start.navigationRevision + 1
        && !scroll.menuOpening, "Accepted cursor movement retained opening or changed the structural page epoch");
    for (const auto* name : {"eight-last", "eight-complete"}) {
        Check(states.at(name)->menuRevision == states.at("eight-start")->menuRevision
            && states.at(name)->navigationRevision == states.at("eight-start")->navigationRevision,
            "An eight-row opening changed epochs while progressing or completing");
    }
    // Drawing continues beyond the real 250 ms design unlock while old holds remain consumed
    runtime::NativeInputRouter router(bindings); std::set<int> held;
    const auto poll = [&](std::uint64_t now, const MenuSnapshot& state) {
        return router.Poll(true, true, false, [&](int key) { return held.contains(key); }, now,
            state.navigationRevision, false, false, runtime::InputModeForSnapshot(state),
            state.appearance.scrollAnimationMilliseconds, state.menuRevision);
    };
    poll(50, start); held = {70}; Check(!poll(150, *states.at("progress")), "An actual opening bypassed design cooldown");
    Check(!poll(250, *states.at("input-ready")), "Actual design unlock replayed held selection during opening");
    held.clear(); poll(251, *states.at("input-ready")); held = {70};
    Check(poll(251, *states.at("input-ready")) == runtime::Input::Select,
        "Actual ongoing opening delayed a fresh selection after design controls unlocked");
    held.clear(); poll(252, *states.at("input-ready")); held = {1};
    Check(poll(252, *states.at("input-ready")) == runtime::Input::Down,
        "Actual ongoing opening blocked normal scrolling");
}
}
