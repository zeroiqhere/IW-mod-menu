#include "TestSupport.h"
#include "ControlLabels.h"
#include <fstream>
#include <set>

namespace iw::overlay::tests {
namespace {
std::shared_ptr<const MenuSnapshot> ReadDesignProfile(const std::filesystem::path& directory, const std::string& name,
    const std::vector<std::string>& normalHints, const std::vector<std::string>& widthHints) {
    const auto path = directory / name;
    Check(std::filesystem::is_regular_file(path), "An actual design profile is missing");
    const auto size = std::filesystem::file_size(path);
    Check(size > 0 && size <= runtime::MaximumFileBytes, "An actual design profile exceeds protocol bounds");
    std::ifstream input(path, std::ios::binary);
    const std::string bytes{std::istreambuf_iterator<char>(input), std::istreambuf_iterator<char>()};
    const auto packet = Json::parse(bytes);
    Check(packet.at("appearance").contains("design") && !packet.contains("presentationFocusRevision"),
        "An actual design producer omitted design or supplied a private focus epoch");
    const auto state = runtime::ParseState(bytes, 42, &normalHints, nullptr, nullptr, &widthHints).snapshot;
    Check(!state->menuOpenTransition, "An actual Design profile retained its settled initial opening pause");
    return state;
}
void CheckDesignPreferenceRows(const MenuSnapshot& state, const std::string& candidate,
    bool colored, bool counter) {
    const char* labels[] = {"Design", "Bool Display", "Bool Box Location", "Scroll Animation Time (ms)",
        "Quick Exit", "Disable Quick Menu", "Spotlight Cursor", "Colored Cursor", "Large Cursor", "Option Counter", "Stealth UI", "No Target While In Menu"};
    const std::string values[] = {candidate, "Boxes", "Right", "120 ms", "OFF", "UNAVAILABLE", "ON",
        colored ? "ON" : "OFF", "OFF", counter ? "ON" : "OFF", "OFF", "UNAVAILABLE"};
    Check(state.title == "Menu Preferences" && state.rows.size() == std::size(labels) && state.selectedIndex == 0,
        "Actual design preference rows lost their title, count or selected Design row");
    for (std::size_t row = 0; row < std::size(labels); ++row) {
        // Cached design packets keep historical capability bits; current admission uses fresh exports
        Check(state.rows[row].label == labels[row] && state.rows[row].value == values[row]
            && ((row == 7 || row == 9) || state.rows[row].enabled == (row != 5 && row != 11)),
            "An actual design row lost its exact value or design-specific capability");
    }
}
}
void BackendMenuDesignStatesPreserveActualControllerChoicesAndTransitions(const std::filesystem::path& directory) {
    auto bindingPacket = BindingsJson(); bindingPacket["attack"] = {1};
    bindingPacket["previousChoice"] = {51}; bindingPacket["nextChoice"] = {52};
    const auto bindings = runtime::ParseBindings(bindingPacket.dump());
    const auto normalHints = runtime::DescribeControls(bindings);
    const auto widthHints = runtime::DescribeWidthEditorControls(bindings);
    const char* profiles[] = {"classic", "native", "aio", "basic"};
    const char* names[] = {"Classic", "Native", "AIO", "Basic"};
    const MenuDesign designs[] = {MenuDesign::Classic, MenuDesign::Native, MenuDesign::Aio, MenuDesign::Basic};
    const char* phases[] = {"rebuild", "pre-rebuild", "cooldown", "pre-unlock", "unlocked"};
    const std::uint64_t times[] = {0, 49, 50, 249, 250};
    for (std::size_t design = 0; design < std::size(profiles); ++design) {
        std::vector<std::shared_ptr<const MenuSnapshot>> states;
        for (std::size_t phase = 0; phase < std::size(phases); ++phase) {
            const auto name = std::string("preferences-design-") + profiles[design] + "-" + phases[phase] + ".json";
            const auto state = ReadDesignProfile(directory, name, normalHints, widthHints);
            const bool colorRestricted = design < 2;
            MenuAppearance expected; expected.coloredCursor = !colorRestricted; expected.optionCounter = colorRestricted;
            // Rebuild retains the prior design while normalized flags commit immediately
            expected.design = phase < 2 ? MenuDesign::Apparition : designs[design];
            Check(state->appearance == expected && state->isOpen && !state->isRootMenu && state->context == "PREVIEW"
                && state->accent == "#3998FE" && !state->smoothRainbowTheme && state->instructions == normalHints
                && !state->widthEditor && !state->instructionPositionEditor && !state->menuPositionEditor
                && !state->menuCredits && !state->menuUnlock && !state->menuPreferenceRefresh,
                "An actual design transition changed independent preferences or modal ownership");
            CheckDesignPreferenceRows(*state, names[design], !colorRestricted, colorRestricted);
            const auto transition = phase < 2 ? std::optional{MenuDesignTransitionPhase::Rebuild}
                : phase < 4 ? std::optional{MenuDesignTransitionPhase::Cooldown} : std::nullopt;
            Check(state->menuOpening == (phase < 2 ? std::nullopt
                : std::optional{MenuOpening{3, phase == 2 ? 0 : phase == 3 ? 199 : 200}}),
                "Changed design did not start its independent opening at the observed redraw");
            Check(state->menuDesignFooterDesign == (phase < 2 ? std::optional{designs[design]} : std::nullopt)
                && state->navigationRevision == 25 && state->menuRevision == (phase < 2 ? 7u : 8u),
                "An actual committed footer target changed captured styling or its exact public epochs");
            Check(state->menuDesignTransition == transition && runtime::InputModeForSnapshot(*state)
                == (phase < 4 ? runtime::InputMode::MenuLocked : runtime::InputMode::Menu),
                "Actual design progress selected an incorrect input route");
            if (phase < 4) Check(state->notice.find("controls paused") != std::string::npos,
                "An actual design transition omitted paused controls guidance");
            states.push_back(state);
        }
        Check(states[0]->menuRevision && *states[0]->menuRevision < UINT64_MAX,
            "Actual design entry lost bounded page identity");
        for (std::size_t phase = 0; phase < states.size(); ++phase) {
            Check(states[phase]->navigationRevision == states[0]->navigationRevision
                && states[phase]->menuRevision == *states[0]->menuRevision + (phase >= 2 ? 1u : 0u),
                "Actual design rebuilding changed cursor identity or missed its single presentation increment");
        }
        // The same real transition packets consume every saved hold until release and a fresh retry
        for (int key : {1, 2, 51, 52, 70, 86, 6}) {
            runtime::NativeInputRouter router(bindings); std::set<int> held;
            const auto poll = [&](std::uint64_t now, const MenuSnapshot& state) {
                return router.Poll(true, true, false, [&](int candidate) { return held.contains(candidate); }, now,
                    state.navigationRevision, state.appearance.quickExit, false, runtime::InputModeForSnapshot(state),
                    state.appearance.scrollAnimationMilliseconds, state.menuRevision);
            };
            poll(0, *states[0]); held = {key};
            for (std::size_t phase = 0; phase < states.size(); ++phase) {
                Check(!poll(times[phase], *states[phase]), "Actual design transition replayed a held control");
            }
            held.clear(); Check(!poll(251, *states.back()), "Actual design release queued a deferred command");
            held = {70}; Check(poll(251, *states.back()) == runtime::Input::Select,
                "Fresh selection failed after the actual design transition unlocked");
        }
    }
    const auto draft = ReadDesignProfile(directory, "preferences-design-draft.json", normalHints, widthHints);
    Check(draft->appearance == MenuAppearance{} && !draft->menuDesignTransition && !draft->menuDesignFooterDesign && !draft->menuOpening
        && runtime::InputModeForSnapshot(*draft) == runtime::InputMode::Menu,
        "An actual design candidate changed the applied appearance or started rebuilding before Use");
    CheckDesignPreferenceRows(*draft, "Classic", true, false);

    // Re-selecting the applied design waits without rebuilding or replacing either epoch
    std::vector<std::shared_ptr<const MenuSnapshot>> sameStates;
    for (const auto* phase : {"cooldown", "pre-unlock", "unlocked"}) {
        const auto state = ReadDesignProfile(directory, std::string("preferences-design-same-") + phase + ".json", normalHints, widthHints);
        const bool unlocked = std::string(phase) == "unlocked";
        Check(state->appearance == MenuAppearance{} && !state->menuDesignFooterDesign && !state->menuOpening && state->isOpen && !state->isRootMenu && state->context == "PREVIEW"
            && state->accent == "#3998FE" && state->instructions == normalHints
            && state->menuDesignTransition == (unlocked ? std::nullopt : std::optional{MenuDesignTransitionPhase::Cooldown})
            && runtime::InputModeForSnapshot(*state) == (unlocked ? runtime::InputMode::Menu : runtime::InputMode::MenuLocked),
            "An actual same-design choice rebuilt, changed appearance or skipped its independent Use cooldown");
        CheckDesignPreferenceRows(*state, "Apparition", true, false);
        if (!sameStates.empty()) Check(state->navigationRevision == sameStates[0]->navigationRevision
            && state->menuRevision == sameStates[0]->menuRevision,
            "Actual same-design acknowledgement invalidated an unchanged cursor or presentation epoch");
        sameStates.push_back(state);
    }
    for (int key : {1, 2, 51, 52, 70, 86, 6}) {
        runtime::NativeInputRouter router(bindings); std::set<int> held;
        const auto poll = [&](std::uint64_t now, const MenuSnapshot& state) {
            return router.Poll(true, true, false, [&](int candidate) { return held.contains(candidate); }, now,
                state.navigationRevision, false, false, runtime::InputModeForSnapshot(state),
                state.appearance.scrollAnimationMilliseconds, state.menuRevision);
        };
        poll(0, *sameStates[0]); held = {key};
        Check(!poll(0, *sameStates[0]) && !poll(249, *sameStates[1]) && !poll(250, *sameStates[2]),
            "Actual same-design unlock replayed a hold while its epochs remained unchanged");
        held.clear(); Check(!poll(251, *sameStates[2]), "Actual same-design release deferred a command");
        held = {70}; Check(poll(251, *sameStates[2]) == runtime::Input::Select,
            "Fresh retry failed after the actual same-design cooldown");
    }
    // The adaptive AIO gap uses its committed target without replacing the captured body style
    for (const auto* name : {"preferences-design-adaptive-aio-rebuild.json",
        "preferences-design-adaptive-aio-pre-rebuild.json"}) {
        const auto state = ReadDesignProfile(directory, name, normalHints, widthHints);
        MenuAppearance expected;
        expected.adaptiveInstructions = true;
        Check(state->appearance == expected && state->menuDesignFooterDesign == MenuDesign::Aio
            && state->menuDesignTransition == MenuDesignTransitionPhase::Rebuild
            && state->isOpen && !state->isRootMenu && state->context == "PREVIEW"
            && state->accent == "#3998FE" && !state->smoothRainbowTheme && state->instructions == normalHints
            && state->navigationRevision == 31 && state->menuRevision == 15 && !state->presentationFocusRevision
            && !state->widthEditor && !state->instructionPositionEditor && !state->menuPositionEditor
            && !state->menuCredits && !state->menuUnlock && !state->menuPreferenceRefresh && !state->menuOpening
            && runtime::InputModeForSnapshot(*state) == runtime::InputMode::MenuLocked,
            "An actual adaptive AIO gap changed captured appearance, exact epochs or modal ownership");
        CheckDesignPreferenceRows(*state, "AIO", true, false);
        const auto bytes = [&] {
            std::ifstream input(directory / name, std::ios::binary);
            return std::string{std::istreambuf_iterator<char>(input), std::istreambuf_iterator<char>()};
        }();
        const auto packet = Json::parse(bytes);
        Check(packet.at("lastSequence") == 30 && state->notice.find("controls paused") != std::string::npos,
            "The actual adaptive gap changed its acknowledged command or paused guidance");
    }
    // Every existing profile either owns its exact rebuild target or preserves absent legacy geometry
    std::size_t profileCount = 0, descriptorCount = 0;
    for (const auto& entry : std::filesystem::directory_iterator(directory)) {
        if (!entry.is_regular_file() || entry.path().extension() != ".json") continue;
        ++profileCount;
        const auto name = entry.path().filename().string();
        std::ifstream input(entry.path(), std::ios::binary);
        const std::string bytes{std::istreambuf_iterator<char>(input), std::istreambuf_iterator<char>()};
        const auto packet = Json::parse(bytes);
        const auto state = runtime::ParseState(bytes, 42).snapshot;
        std::optional<MenuDesign> expected;
        for (std::size_t index = 0; index < std::size(profiles); ++index) {
            const auto prefix = std::string("preferences-design-") + profiles[index];
            if (name == prefix + "-rebuild.json" || name == prefix + "-pre-rebuild.json") expected = designs[index];
        }
        if (name == "preferences-design-adaptive-aio-rebuild.json"
            || name == "preferences-design-adaptive-aio-pre-rebuild.json") expected = MenuDesign::Aio;
        Check(state->menuDesignFooterDesign == expected && !packet.contains("menuDesignFooterDesign"),
            "An actual producer profile invented, lost or misplaced its committed footer descriptor");
        const auto& transition = packet.at("menuDesignTransition");
        Check((transition.is_object() && transition.contains("footerDesign")) == expected.has_value(),
            "An actual producer supplied a footer descriptor outside its changed-design rebuild");
        if (expected) ++descriptorCount;
    }
    const auto expectedProfileCount = std::filesystem::exists(directory / "opening-cleanup-retry.json") ? 233u : std::filesystem::exists(directory / "opening-closed.json") ? 232u : std::filesystem::exists(directory / "quick-root-unbound.json") ? 218u : std::filesystem::exists(directory / "navigation-shared-parent-after-back.json") ? 217u : std::filesystem::exists(directory / "quick-controls-default.json")
        ? 214u : std::filesystem::exists(directory / "spacer-scroll-ordinary-down-before.json") ? 192u : std::filesystem::exists(directory / "open-controls-default.json") ? 180u : 158u;
    Check(profileCount == expectedProfileCount && descriptorCount == 10,
        "The actual producer set lost an established profile or changed the ten intentional rebuild descriptors");
    const auto main = ReadDesignProfile(directory, "main-design-native.json", normalHints, widthHints);
    MenuAppearance nativeExpected; nativeExpected.design = MenuDesign::Native;
    nativeExpected.coloredCursor = false; nativeExpected.optionCounter = true;
    Check(main->appearance == nativeExpected && main->isOpen && main->isRootMenu && main->title == "Main Menu"
        && main->context == "PREVIEW" && main->selectedIndex == 2 && main->rows.size() > 2 && main->rows[2].label == "Menu Customization"
        && !main->menuDesignTransition && !main->menuDesignFooterDesign && !main->menuOpening
        && runtime::InputModeForSnapshot(*main) == runtime::InputMode::Menu,
        "Actual Native root lost its independent title, retained selection or compatible counter");
    const auto ordinaryMain = ReadDesignProfile(directory, "main.json", normalHints, widthHints);
    Check(main->rows.size() == ordinaryMain->rows.size(), "Native styling replaced root navigation rows");
    for (std::size_t row = 0; row < main->rows.size(); ++row) {
        Check(main->rows[row].label == ordinaryMain->rows[row].label && main->rows[row].value == ordinaryMain->rows[row].value
            && main->rows[row].enabled == ordinaryMain->rows[row].enabled,
            "Native root styling changed an actual offline row value or capability");
    }
    const auto width = ReadDesignProfile(directory, "width-editor-design-classic.json", normalHints, widthHints);
    MenuAppearance classicExpected = nativeExpected; classicExpected.design = MenuDesign::Classic;
    Check(width->appearance == classicExpected && width->isOpen && !width->isRootMenu && width->rows.empty()
        && width->selectedIndex == 0 && width->widthEditor && width->widthEditor->step == 1
        && width->widthEditor->instructions == widthHints && width->widthEditor->footerInstructions[2] == widthHints[4]
        && !width->menuDesignTransition && !width->menuDesignFooterDesign && !width->menuOpening && runtime::InputModeForSnapshot(*width) == runtime::InputMode::WidthEditor,
        "Actual Classic styling lost a coherent width editor or leaked the finished design transition");
}
}
