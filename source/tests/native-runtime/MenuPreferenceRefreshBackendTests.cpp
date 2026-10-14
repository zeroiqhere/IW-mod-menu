#include "TestSupport.h"
#include "ControlLabels.h"
#include <fstream>
#include <set>

namespace iw::overlay::tests {
void BackendPreferenceRefreshStatesPreserveActualControllerPhases(const std::filesystem::path& directory) {
    const auto bindings = runtime::ParseBindings(BindingsJson().dump());
    const auto normalHints = runtime::DescribeControls(bindings);
    const auto nestedHints = runtime::DescribeControls(bindings, true, false);
    // Every profile starts from the same default controller and changes one local flag
    const char* flags[] = {"spotlight", "colored", "large", "quick-exit"};
    const char* phases[] = {"pending", "pre-refresh", "cooldown", "pre-unlock", "unlocked"};
    const std::uint64_t times[] = {0, 179, 180, 429, 430};
    const std::size_t rows[] = {6, 7, 8, 4};
    for (std::size_t flag = 0; flag < std::size(flags); ++flag) {
        std::vector<std::shared_ptr<const MenuSnapshot>> states;
        for (std::size_t phase = 0; phase < std::size(phases); ++phase) {
            const auto name = std::string("preferences-refresh-") + flags[flag] + "-" + phases[phase] + ".json";
            const auto path = directory / name;
            Check(std::filesystem::is_regular_file(path), "An actual preference refresh profile is missing");
            const auto size = std::filesystem::file_size(path);
            Check(size > 0 && size <= runtime::MaximumFileBytes, "An actual refresh profile exceeds protocol bounds");
            std::ifstream input(path, std::ios::binary);
            const std::string bytes{std::istreambuf_iterator<char>(input), std::istreambuf_iterator<char>()};
            const auto parsed = runtime::ParseState(bytes, 42, &normalHints, &nestedHints);
            const auto& state = *parsed.snapshot;
            Check(state.isOpen && !state.isRootMenu && state.context == "PREVIEW" && state.title == "Menu Preferences"
                && state.rows.size() == 12 && state.selectedIndex == rows[flag] && state.accent == "#3998FE"
                && !state.smoothRainbowTheme && !state.widthEditor && !state.instructionPositionEditor
                && !state.menuPositionEditor && !state.menuCredits && !state.menuUnlock && !state.menuDesignTransition && !state.menuOpening
                && !state.presentationFocusRevision && !state.menuOpenTransition,
                "An actual refresh replaced its ordinary offline preference context");
            MenuAppearance expected;
            // Quick Exit commits immediately; the three cursor styles retain their prior presentation until 180
            expected.quickExit = flag == 3;
            if (phase >= 2) {
                if (flag == 0) expected.spotlightCursor = false;
                if (flag == 1) expected.coloredCursor = false;
                if (flag == 2) expected.largeCursor = true;
            }
            Check(state.appearance == expected && state.instructions == (flag == 3 ? nestedHints : normalHints),
                "Actual refresh changed its presentation timing or saved footer ownership");
            const char* labels[] = {"Design", "Bool Display", "Bool Box Location", "Scroll Animation Time (ms)",
                "Quick Exit", "Disable Quick Menu", "Spotlight Cursor", "Colored Cursor", "Large Cursor", "Option Counter", "Stealth UI", "No Target While In Menu"};
            const char* values[] = {"Apparition", "Boxes", "Right", "120 ms", "OFF", "UNAVAILABLE", "ON", "ON", "OFF", "OFF", "OFF", "UNAVAILABLE"};
            for (std::size_t row = 0; row < std::size(labels); ++row) {
                std::string value = values[row];
                if (row == rows[flag] && phase >= 2) value = flag < 2 ? "OFF" : "ON";
                Check(state.rows[row].label == labels[row] && state.rows[row].value == value && state.rows[row].enabled == (row != 5 && row != 11),
                    "Actual refresh changed an exact preference row value or local capability");
            }
            const auto refresh = phase < 2 ? std::optional{MenuPreferenceRefreshPhase::Pending}
                : phase < 4 ? std::optional{MenuPreferenceRefreshPhase::Cooldown} : std::nullopt;
            Check(state.menuPreferenceRefresh == refresh && runtime::InputModeForSnapshot(state)
                == (phase < 4 ? runtime::InputMode::MenuLocked : runtime::InputMode::Menu),
                "Actual refresh progress selected the wrong paused input route");
            if (phase < 4) {
                Check(state.notice.find("controls paused") != std::string::npos
                    && state.notice.find("Select to") == std::string::npos,
                    "Actual paused preference notice advertised a blocked selection");
            }
            states.push_back(parsed.snapshot);
        }
        // Only the presentation commit changes page identity; no cursor action occurs during refresh
        Check(states[0]->menuRevision.has_value() && *states[0]->menuRevision < UINT64_MAX,
            "Actual refresh lost bounded page identity");
        for (std::size_t phase = 0; phase < states.size(); ++phase) {
            Check(states[phase]->navigationRevision == states[0]->navigationRevision
                && states[phase]->menuRevision == *states[0]->menuRevision + (phase >= 2 ? 1u : 0u),
                "Actual refresh changed navigation or failed its single presentation epoch increment");
        }
        // A held selection across all five real packets remains consumed until a new aggregate press
        runtime::NativeInputRouter router(bindings); std::set<int> held;
        const auto poll = [&](std::uint64_t now, const MenuSnapshot& state) {
            return router.Poll(true, true, false, [&](int key) { return held.contains(key); }, now,
                state.navigationRevision, state.appearance.quickExit, false, runtime::InputModeForSnapshot(state),
                state.appearance.scrollAnimationMilliseconds, state.menuRevision);
        };
        poll(0, *states[0]); held = {70};
        for (std::size_t phase = 0; phase < states.size(); ++phase) {
            Check(!poll(times[phase], *states[phase]), "Actual preference refresh replayed held selection");
        }
        held.clear(); Check(!poll(431, *states.back()), "Actual refresh release deferred a selection");
        held = {70}; Check(poll(431, *states.back()) == runtime::Input::Select,
            "A fresh retry failed after the actual preference refresh unlocked");
    }
}
}
