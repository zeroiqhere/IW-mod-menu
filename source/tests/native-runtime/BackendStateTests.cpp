#include "TestSupport.h"
#include "quick/LocalMenuSchemaTests.h"
#include <filesystem>
#include <fstream>
#include <algorithm>
#include <set>
#include <map>
#include "ControlLabels.h"

namespace iw::overlay::tests {

void BackendPreviewStatesPreserveActualControllerPreferences(const std::filesystem::path& directory) {
    const char* files[] = {"main.json", "basic-scripts.json", "all-players.json", "customization.json",
        "theme-colors-default.json", "theme-colors-mint.json", "preferences-default.json",
        "preferences-draft.json", "preferences-text-left.json", "preferences-quick-exit.json", "root-quick-exit.json",
        "width-editor-default.json", "width-editor-wide.json", "customization-wide.json",
        "customization-unlock-rebuild.json", "customization-unlock-pause.json",
        "instructions-default.json", "instructions-alternate-no-background.json",
        "instructions-adaptive.json", "instructions-hidden.json",
        "instructions-position-entering.json", "instructions-position-editing.json",
        "instructions-position-moved.json", "instructions-position-exiting.json",
        "instructions-position-reset.json", "instructions-hidden-reset.json",
        "theme-colors-rainbow.json", "theme-colors-rainbow-no-op.json", "theme-colors-rainbow-stopped.json",
        "preferences-scroll-draft.json", "preferences-scroll-committed.json", "preferences-scroll-min.json",
        "preferences-scroll-max.json", "scroll-motion-root-start.json", "scroll-motion-root-down.json",
        "scroll-motion-root-reopened.json", "scroll-motion-colors-start.json", "scroll-motion-colors-window.json",
        "scroll-motion-customization.json", "menu-position-editor-default.json", "menu-position-editor-moved.json",
        "menu-position-editor-step-max.json", "menu-position-unlock-rebuild.json", "menu-position-unlock-pause.json",
        "customization-positioned.json", "menu-credits-enter.json", "menu-credits-fade-in.json",
        "menu-credits-overlap.json", "menu-credits-late.json", "menu-credits-unlock-rebuild.json",
        "menu-credits-unlock-pause.json", "customization-after-credits.json"};
    auto packet = BindingsJson();
    packet["attack"] = {1}; packet["previousChoice"] = {51}; packet["nextChoice"] = {52};
    packet["up"] = {2, 38}; packet["down"] = {1, 40}; packet["moveUp"] = {38}; packet["moveDown"] = {40};
    packet["frag"] = {71}; packet["secondaryOffhand"] = {69};
    const auto bindings = runtime::ParseBindings(packet.dump());
    const auto normalHints = runtime::DescribeControls(bindings);
    const auto nestedHints = runtime::DescribeControls(bindings, true, false);
    const auto rootHints = runtime::DescribeControls(bindings, true, true);
    const auto editorHints = runtime::DescribeWidthEditorControls(bindings);
    const auto positionHints = runtime::DescribeInstructionPositionControls(bindings);
    const auto menuPositionHints = runtime::DescribeMenuPositionControls(bindings);
    const auto creditsHints = runtime::DescribeMenuCreditsControls(bindings);
    std::shared_ptr<const MenuSnapshot> actualEditor, actualParent, actualRebuild, actualPause;
    std::shared_ptr<const MenuSnapshot> positionEntering, positionEditing, positionMoved, positionExiting, positionParent;
    std::uint64_t originalEpoch = 0, draftEpoch = 0, changedEpoch = 0;
    std::shared_ptr<const MenuSnapshot> rainbowStarted, rainbowNoOp, rainbowStopped;
    std::uint64_t rainbowStartedSequence = 0, rainbowNoOpSequence = 0;
    std::map<std::string, std::shared_ptr<const MenuSnapshot>> scrollStates, menuPositionStates, creditsStates;
    for (const auto* filename : files) {
        const auto path = directory / filename;
        Check(std::filesystem::is_regular_file(path), "An exported backend fixture is missing");
        const auto size = std::filesystem::file_size(path);
        Check(size > 0 && size <= runtime::MaximumFileBytes, "An exported fixture exceeds protocol bounds");
        std::ifstream input(path, std::ios::binary);
        const std::string bytes{std::istreambuf_iterator<char>(input), std::istreambuf_iterator<char>()};
        // The offline exporter owns PID 42 and has no game transport dependency
        const auto parsed = runtime::ParseState(bytes, 42, &normalHints, &nestedHints, &rootHints, &editorHints, &positionHints, std::nullopt, &menuPositionHints, &creditsHints);
        const auto& snapshot = *parsed.snapshot;
        Check(!snapshot.menuOpenTransition, "An established settled profile retained its initial open pause");
        Check(!snapshot.menuOpening, "An established non-design profile unexpectedly started opening");
        Check(!snapshot.menuDesignTransition && snapshot.appearance.design == MenuDesign::Apparition,
            "An established profile changed design or retained a design transition");
        Check(!snapshot.menuPreferenceRefresh, "An established completed profile retained a local refresh pause");
        Check(snapshot.isOpen && snapshot.context == "PREVIEW", "An offline fixture claims a live connection");
        const std::string name(filename);
        const bool creditsVisit = name == "menu-credits-enter.json" || name == "menu-credits-fade-in.json"
            || name == "menu-credits-overlap.json" || name == "menu-credits-late.json";
        const bool creditsUnlocking = name.starts_with("menu-credits-unlock-");
        const bool creditsProfile = creditsVisit || creditsUnlocking || name == "customization-after-credits.json";
        if (creditsProfile) creditsStates.emplace(name, parsed.snapshot);
        Check(snapshot.menuCredits.has_value() == creditsVisit, "Actual credits metadata lost its active visit identity");
        if (creditsVisit) {
            const int elapsed = name == "menu-credits-enter.json" ? 0 : name == "menu-credits-fade-in.json" ? 500
                : name == "menu-credits-overlap.json" ? 1000 : 39999;
            Check(snapshot.title == "Menu Customization" && snapshot.menuCredits->elapsedMilliseconds == elapsed
                && snapshot.menuCredits->instructions == creditsHints, "Actual credits progress or saved exit hint changed");
        }
        const bool menuEditor = name.starts_with("menu-position-editor-");
        const bool menuUnlocking = name.starts_with("menu-position-unlock-");
        const bool menuPositionProfile = menuEditor || menuUnlocking || name == "customization-positioned.json";
        if (menuPositionProfile) menuPositionStates.emplace(name, parsed.snapshot);
        Check(snapshot.menuPositionEditor.has_value() == menuEditor, "Actual menu position editor identity was lost");
        if (menuEditor) {
            Check(snapshot.title == "Menu Customization" && snapshot.menuPositionEditor->step
                == (name == "menu-position-editor-step-max.json" ? 10 : 1)
                && snapshot.menuPositionEditor->instructions == menuPositionHints
                && snapshot.menuPositionEditor->footerInstructions[2] == menuPositionHints[6],
                "Actual menu positioning metadata or saved help differs from controller contract");
        }
        const bool rainbow = name == "theme-colors-rainbow.json" || name == "theme-colors-rainbow-no-op.json";
        const bool stopped = name == "theme-colors-rainbow-stopped.json";
        // The real producer publishes a top-level mode rather than deriving it from row text
        const auto producer = Json::parse(bytes);
        Check(producer.contains("menuRevision") && snapshot.menuRevision.has_value()
            && !producer.contains("presentationFocusRevision") && !snapshot.presentationFocusRevision,
            "Actual producer lost page identity or supplied a private focus epoch");
        if (name.starts_with("preferences-scroll-") || name.starts_with("scroll-motion-")) {
            scrollStates.emplace(name, parsed.snapshot);
        }
        Check(producer.contains("smoothRainbowTheme") && producer.at("smoothRainbowTheme").is_boolean()
            && snapshot.smoothRainbowTheme == rainbow, "Actual producer theme mode differs from its committed selection");
        if (name.starts_with("theme-colors-") || name.starts_with("scroll-motion-colors-")) {
            const char* labels[] = {"Red", "Green", "Blue", "Black", "White", "Gray", "Dodger Blue", "Ocean Blue",
                "Deep Blue", "Midnight Blue", "Sky Blue", "Cyan", "Aqua", "Teal", "Pink", "AIO Pink", "Hot Pink",
                "Rose", "Fuchsia", "Purple", "Lavender", "Violet", "Indigo", "Plasma Purple", "Neon Purple",
                "Crimson", "Fire Red", "Ruby", "Orange", "Deep Orange", "Yellow", "Gold", "Mint", "Lime",
                "Toxic Green", "Emerald", "Smooth Rainbow"};
            Check(snapshot.title == "Main Design Color" && snapshot.rows.size() == std::size(labels),
                "Actual theme palette lost its exact source rows");
            const std::size_t checked = rainbow ? 36 : stopped || name == "theme-colors-mint.json" ? 32 : 6;
            for (std::size_t index = 0; index < std::size(labels); ++index) {
                Check(snapshot.rows[index].label == labels[index] && snapshot.rows[index].enabled
                    && snapshot.rows[index].value == (index == checked ? "ON" : "OFF"),
                    "Actual theme indicators differ from the source palette or mode");
            }
            if (rainbow || stopped) {
                Check(snapshot.selectedIndex == (rainbow ? 36 : 32), "Actual theme action lost its selected row");
            }
            if (name == "theme-colors-rainbow.json") {
                rainbowStarted = parsed.snapshot; rainbowStartedSequence = parsed.lastSequence;
            } else if (name == "theme-colors-rainbow-no-op.json") {
                rainbowNoOp = parsed.snapshot; rainbowNoOpSequence = parsed.lastSequence;
            } else if (stopped) rainbowStopped = parsed.snapshot;
        }
        const bool editor = name == "width-editor-default.json" || name == "width-editor-wide.json";
        const bool positionEditor = name == "instructions-position-entering.json" || name == "instructions-position-editing.json"
            || name == "instructions-position-moved.json" || name == "instructions-position-exiting.json";
        const bool widthUnlocking = name == "customization-unlock-rebuild.json" || name == "customization-unlock-pause.json";
        const bool unlocking = widthUnlocking || menuUnlocking || creditsUnlocking;
        Check(editor || positionEditor || menuEditor || creditsVisit ? snapshot.rows.empty() && snapshot.selectedIndex == 0
            : !snapshot.rows.empty() && snapshot.selectedIndex < snapshot.rows.size(),
            "The actual controller exported an invalid selection");
        Check(snapshot.navigationRevision > 0, "Actual controller navigation was lost by the native parser");
        const bool changed = std::string(filename) == "preferences-text-left.json";
        const bool quick = std::string(filename) == "preferences-quick-exit.json" || std::string(filename) == "root-quick-exit.json";
        const bool root = std::string(filename) == "main.json" || std::string(filename) == "root-quick-exit.json"
            || name.starts_with("scroll-motion-root-");
        MenuAppearance expected;
        expected.quickExit = quick;
        expected.scrollAnimationMilliseconds = name == "preferences-scroll-committed.json" ? 130
            : name == "preferences-scroll-min.json" ? 100 : name == "preferences-scroll-max.json" ? 250 : 120;
        expected.menuWidth = name == "width-editor-wide.json" || name == "customization-wide.json" || widthUnlocking ? 500 : 260;
        // Appended instruction exports come from a fresh default-theme controller
        const bool instructionPage = name.starts_with("instructions-");
        const bool alternate = name == "instructions-alternate-no-background.json";
        const bool adaptive = name == "instructions-adaptive.json";
        const bool hidden = name == "instructions-hidden.json" || name == "instructions-hidden-reset.json";
        expected.alternateInstructions = alternate;
        expected.instructionsBackground = !alternate;
        expected.adaptiveInstructions = adaptive;
        if (hidden) expected.instructionsVisible = false;
        if (name == "instructions-position-moved.json" || name == "instructions-position-exiting.json") {
            expected.instructionPosition = InstructionPosition{263, 480};
        }
        if (changed || quick) {
            expected.booleanDisplay = "Text";
            expected.boolBoxLocation = "Left";
            expected.instructionsVisible = false;
            expected.optionCounter = true;
            expected.coloredCursor = false;
            expected.spotlightCursor = false;
            expected.largeCursor = true;
        }
        if (name == "menu-position-editor-moved.json") expected.menuPosition = MenuPosition{1, 86};
        else if (menuPositionProfile && name != "menu-position-editor-default.json") expected.menuPosition = MenuPosition{11, 96};
        Check(producer.at("appearance").contains("design") && producer.at("appearance").at("design") == "Apparition",
            "An established producer profile omitted its exact applied design");
        Check(producer.at("appearance").contains("menuPosition"), "Actual producer omitted its nullable panel position");
        Check(snapshot.appearance == expected, "Native preferences differ from actual controller choices");
        Check(snapshot.isRootMenu == root, "Native root identity differs from actual controller navigation");
        Check(snapshot.instructions == (quick ? root ? rootHints : nestedHints : normalHints),
            "Actual root and quick-exit state selected the wrong saved control hints");
        if ((name.starts_with("customization") || name == "scroll-motion-customization.json" || menuUnlocking || creditsUnlocking) && !menuEditor && !creditsVisit) {
            // The new page link does not move the established Width Editor selection
            const char* labels[] = {"Menu Credits", "Open Controls", "Width Editor", "Reposition Menu", "Menu Instructions", "Main Design Color", "Menu Preferences"};
            Check(snapshot.rows.size() == std::size(labels), "Actual customization page lost a source row");
            for (std::size_t index = 0; index < std::size(labels); ++index) {
                Check(snapshot.rows[index].label == labels[index], "Customization source row order changed");
            }
            Check(snapshot.rows[0].enabled && snapshot.rows[0].value == ">"
                && !snapshot.rows[1].enabled && snapshot.rows[1].value == "UNAVAILABLE"
                && snapshot.rows[4].enabled, "Actual customization export lost its local instruction page");
            Check(snapshot.rows[3].enabled == menuPositionProfile
                && snapshot.rows[3].value == (menuPositionProfile ? ">" : "UNAVAILABLE"),
                "Actual Reposition Menu row claimed an unverified saved-control capability");
        }
        if (instructionPage && !positionEditor) {
            const bool verifiedPosition = name == "instructions-position-reset.json";
            const char* labels[] = {"Disable", "Alternate Style", "Disable Background", "Adaptive Position", "Reposition", "Reset Position"};
            const char* values[] = {hidden ? "ON" : "OFF", alternate ? "ON" : "OFF",
                alternate ? "ON" : "OFF", adaptive ? "ON" : "OFF", verifiedPosition ? ">" : "UNAVAILABLE", ""};
            Check(snapshot.title == "Menu Instructions" && snapshot.rows.size() == std::size(labels),
                "Actual instruction page lost its title or source rows");
            for (std::size_t index = 0; index < std::size(labels); ++index) {
                Check(snapshot.rows[index].label == labels[index] && snapshot.rows[index].value == values[index]
                    // The source keeps all four BOOL rows selectable and rejects Adaptive inside its callback
                    && snapshot.rows[index].enabled == (index < 4 || index == 5 || (index == 4 && verifiedPosition)),
                    "Actual instruction row differs from its committed setting or availability");
            }
            // The real producer must publish the fields rather than relying on native defaults
            const auto appearance = Json::parse(bytes).at("appearance");
            Check(appearance.contains("alternateInstructions") && appearance.contains("instructionsBackground")
                && appearance.contains("adaptiveInstructions"), "An actual instruction export omitted its committed fields");
        }
        Check(snapshot.instructionPositionEditor.has_value() == positionEditor, "Actual position editor identity was lost");
        if (positionEditor) {
            const auto phase = name == "instructions-position-entering.json" ? InstructionPositionEditorPhase::Entering
                : name == "instructions-position-exiting.json" ? InstructionPositionEditorPhase::Exiting : InstructionPositionEditorPhase::Editing;
            Check(snapshot.title == "Menu Instructions" && snapshot.instructionPositionEditor->phase == phase
                && snapshot.instructionPositionEditor->instructions == positionHints,
                "Actual editor phase or private saved-action hints changed");
            if (name == "instructions-position-entering.json") positionEntering = parsed.snapshot;
            else if (name == "instructions-position-editing.json") positionEditing = parsed.snapshot;
            else if (name == "instructions-position-moved.json") positionMoved = parsed.snapshot;
            else positionExiting = parsed.snapshot;
        }
        if (name == "instructions-position-reset.json" || name == "instructions-hidden-reset.json") {
            Check(snapshot.selectedIndex == 5 && !snapshot.appearance.instructionPosition
                && snapshot.rows[5].enabled, "Reset must retain its row, clear saved position and remain available while hidden");
            if (name == "instructions-position-reset.json") positionParent = parsed.snapshot;
        }
        if (snapshot.title == "Menu Preferences") {
            const char* labels[] = {"Design", "Bool Display", "Bool Box Location", "Scroll Animation Time (ms)",
                "Quick Exit", "Disable Quick Menu", "Spotlight Cursor", "Colored Cursor", "Large Cursor", "Option Counter", "Stealth UI", "No Target While In Menu"};
            Check(snapshot.rows.size() == std::size(labels), "An actual preference source row was lost");
            for (std::size_t index = 0; index < std::size(labels); ++index) {
                Check(snapshot.rows[index].label == labels[index], "Actual preference row order changed");
            }
            Check(snapshot.rows[0].value == "Apparition" && snapshot.rows[0].enabled,
                "An existing preference profile lost its applied Design row");
            const int candidate = name == "preferences-scroll-draft.json" ? 130 : expected.scrollAnimationMilliseconds;
            Check(snapshot.rows[3].value == std::to_string(candidate) + " ms" && snapshot.rows[3].enabled,
                "Numeric duration help changed its actual candidate or saved-control availability");
            const auto option = std::find_if(snapshot.rows.begin(), snapshot.rows.end(), [](const auto& row) { return row.label == "Quick Exit"; });
            Check(option != snapshot.rows.end() && option->value == (quick ? "ON" : "OFF") && option->enabled,
                "Quick-exit row differs from its applied actual-controller setting");
        }
        // Editor metadata comes from the actual controller rather than a hand-built native packet
        Check(snapshot.widthEditor.has_value() == editor, "Actual export lost its editor identity");
        if (editor) {
            Check(snapshot.widthEditor->step == (name == "width-editor-wide.json" ? 10 : 1)
                && snapshot.widthEditor->instructions == editorHints
                && snapshot.widthEditor->footerInstructions[2] == editorHints[4],
                "Actual editor metadata or saved exit hints differ from the controller contract");
            if (name == "width-editor-wide.json") actualEditor = parsed.snapshot;
        }
        Check(snapshot.menuUnlock.has_value() == unlocking, "Actual exports lost their pause or resumed ordinary menu");
        if (unlocking) {
            const bool rebuild = name == "customization-unlock-rebuild.json" || name == "menu-position-unlock-rebuild.json"
                || name == "menu-credits-unlock-rebuild.json";
            Check(snapshot.menuUnlock == (rebuild ? MenuUnlockPhase::Rebuild : MenuUnlockPhase::Pause)
                && snapshot.selectedIndex == (menuUnlocking ? 3u : creditsUnlocking ? 0u : 2u)
                && snapshot.rows[menuUnlocking ? 3 : creditsUnlocking ? 0 : 2].enabled,
                "Pause phases must retain logical row capabilities and the original parent cursor");
            Check(snapshot.notice.find("controls paused") != std::string::npos
                && snapshot.notice.find("Select to open") == std::string::npos,
                "Paused parent help cannot advertise a selection that is currently blocked");
            if (widthUnlocking) { if (rebuild) actualRebuild = parsed.snapshot; else actualPause = parsed.snapshot; }
        }
        if (name == "customization-wide.json") {
            actualParent = parsed.snapshot;
            Check(snapshot.selectedIndex == 2 && snapshot.rows[2].label == "Width Editor" && snapshot.rows[2].enabled,
                "Finishing the editor must retain its enabled parent row and selection");
        }
        // Source palette selection remains independent from local appearance settings
        const bool draft = std::string(filename) == "preferences-draft.json";
        const bool mint = changed || quick || draft || std::string(filename) == "theme-colors-mint.json"
            || std::string(filename) == "preferences-default.json" || stopped;
        Check(snapshot.accent == (mint ? "#98FF98" : "#3998FE"), "Native accent differs from the exported selection");
        if (std::string(filename) == "preferences-default.json") originalEpoch = snapshot.navigationRevision;
        if (draft) {
            draftEpoch = snapshot.navigationRevision;
            Check(snapshot.rows[1].value == "Text" && snapshot.appearance.booleanDisplay == "Boxes",
                "Previewing a candidate changed committed appearance before Use");
        }
        if (changed) changedEpoch = snapshot.navigationRevision;
        if (std::string(filename) == "basic-scripts.json" || std::string(filename) == "all-players.json") {
            for (const auto& row : snapshot.rows) {
                Check(!row.enabled && (row.value == "UNKNOWN" || row.value == "UNAVAILABLE"),
                    "An offline fixture claims an active game feature");
            }
        }
    }
    Check(creditsStates.size() == 7, "Actual credits progress and return profiles were not all joined");
    const auto& creditsEnter = *creditsStates.at("menu-credits-enter.json");
    for (const auto* name : {"menu-credits-fade-in.json", "menu-credits-overlap.json", "menu-credits-late.json"}) {
        const auto& progress = *creditsStates.at(name);
        Check(progress.navigationRevision == 6 && progress.menuRevision == 6
            && progress.navigationRevision == creditsEnter.navigationRevision && progress.menuRevision == creditsEnter.menuRevision
            && progress.selectedIndex == 0 && runtime::InputModeForSnapshot(progress) == runtime::InputMode::MenuCredits,
            "Actual credits time updates changed the modal page or navigation epoch");
    }
    Check(creditsEnter.navigationRevision == 6 && creditsEnter.menuRevision == 6,
        "Actual credits entry lost its exact source-page context");
    for (const auto* name : {"menu-credits-unlock-rebuild.json", "menu-credits-unlock-pause.json", "customization-after-credits.json"}) {
        const auto& parent = *creditsStates.at(name);
        Check(parent.navigationRevision == 7 && parent.menuRevision == 8 && parent.selectedIndex == 0
            && !parent.menuCredits && parent.rows[0].label == "Menu Credits" && parent.rows[0].enabled,
            "Natural credits completion lost its exact parent cursor or retained active metadata");
        Check(runtime::InputModeForSnapshot(parent) == (name == std::string("customization-after-credits.json")
            ? runtime::InputMode::Menu : runtime::InputMode::MenuLocked), "Actual credits return selected the wrong input mode");
    }
    // Actual owner timing metadata joins the fresh saved-melee route and return reseeding
    runtime::NativeInputRouter creditsRouter(bindings); std::set<int> creditsKeys;
    const auto creditsPoll = [&](std::uint64_t now, const MenuSnapshot& state) {
        return creditsRouter.Poll(true, state.isOpen, false, [&](int key) { return creditsKeys.contains(key); }, now,
            state.navigationRevision, state.appearance.quickExit, state.isRootMenu, runtime::InputModeForSnapshot(state),
            state.appearance.scrollAnimationMilliseconds, state.menuRevision);
    };
    creditsPoll(0, creditsEnter); creditsKeys = {86}; const auto finish = creditsPoll(0, creditsEnter);
    Check(finish == runtime::Input::FinishMenuCredits && Json::parse(runtime::CommandJson(42, 1, *finish, creditsEnter.navigationRevision))
        .at("navigationRevision") == 6, "Actual credits exit lost its saved action or captured epoch");
    Check(!creditsPoll(10, *creditsStates.at("menu-credits-unlock-rebuild.json"))
        && !creditsPoll(60, *creditsStates.at("menu-credits-unlock-pause.json"))
        && !creditsPoll(160, *creditsStates.at("customization-after-credits.json")),
        "Actual credits return replayed held exit into ordinary menu input");
    creditsKeys.clear(); creditsPoll(170, *creditsStates.at("customization-after-credits.json")); creditsKeys = {70};
    Check(creditsPoll(170, *creditsStates.at("customization-after-credits.json")) == runtime::Input::Select,
        "Actual parent retained credits routing after completion");
    Check(menuPositionStates.size() == 6, "Actual menu position profiles were not all joined");
    const auto& menuDefault = *menuPositionStates.at("menu-position-editor-default.json");
    const auto& menuMoved = *menuPositionStates.at("menu-position-editor-moved.json");
    const auto& menuMaximum = *menuPositionStates.at("menu-position-editor-step-max.json");
    const auto& menuRebuild = *menuPositionStates.at("menu-position-unlock-rebuild.json");
    const auto& menuPause = *menuPositionStates.at("menu-position-unlock-pause.json");
    const auto& menuParent = *menuPositionStates.at("customization-positioned.json");
    Check(menuDefault.navigationRevision == 10 && menuMoved.navigationRevision == 10 && menuMaximum.navigationRevision == 10
        && menuDefault.menuRevision == 6 && menuMoved.menuRevision == 6 && menuMaximum.menuRevision == 6
        && menuRebuild.navigationRevision == 12 && menuPause.navigationRevision == 12 && menuParent.navigationRevision == 12
        && menuRebuild.menuRevision == 8 && menuPause.menuRevision == 8 && menuParent.menuRevision == 8
        && menuParent.selectedIndex == 3, "Actual editor moves, step changes or completion lost exact navigation identity");
    Check(runtime::InputModeForSnapshot(menuDefault) == runtime::InputMode::MenuPositionEditor
        && runtime::InputModeForSnapshot(menuRebuild) == runtime::InputMode::MenuLocked
        && runtime::InputModeForSnapshot(menuPause) == runtime::InputMode::MenuLocked
        && runtime::InputModeForSnapshot(menuParent) == runtime::InputMode::Menu,
        "Actual menu position transition enabled an incorrect input route");
    // Fresh actual producer metadata joins the same router and context-scoped command encoder
    runtime::NativeInputRouter menuRouter(bindings); std::set<int> menuKeys;
    const auto menuPoll = [&](std::uint64_t now, const MenuSnapshot& state) {
        return menuRouter.Poll(true, state.isOpen, false, [&](int key) { return menuKeys.contains(key); },
            now, state.navigationRevision, state.appearance.quickExit, state.isRootMenu,
            runtime::InputModeForSnapshot(state), state.appearance.scrollAnimationMilliseconds, state.menuRevision);
    };
    menuPoll(0, menuDefault); menuKeys = {38};
    const auto move = menuPoll(0, menuDefault);
    Check(move == runtime::Input::MoveMenuUp && Json::parse(runtime::CommandJson(42, 1, *move, menuDefault.navigationRevision))
        .at("navigationRevision") == 10, "Actual editor movement lost its exact command or captured epoch");
    menuKeys.clear(); menuPoll(25, menuMoved); menuKeys = {69};
    Check(menuPoll(25, menuMoved) == runtime::Input::DecreaseMenuPositionStep, "Actual editor ignored the exact smoke offset action");
    menuKeys.clear(); menuPoll(150, menuMaximum); menuKeys = {86};
    Check(menuPoll(150, menuMaximum) == runtime::Input::FinishMenuPositionEditor, "Actual editor exit leaked into normal Back");
    Check(!menuPoll(150, menuRebuild) && !menuPoll(200, menuPause)
        && !menuPoll(300, menuParent) && !menuPoll(1000, menuParent), "Actual unlock inherited a held editor exit");
    menuKeys.clear(); menuPoll(1001, menuParent); menuKeys = {70};
    Check(menuPoll(1001, menuParent) == runtime::Input::Select, "Actual parent retained menu position routing");
    Check(scrollStates.size() == 10, "The actual scroll profiles were not all joined");
    const auto& draftScroll = *scrollStates.at("preferences-scroll-draft.json");
    for (const auto* name : {"preferences-scroll-committed.json", "preferences-scroll-min.json", "preferences-scroll-max.json"}) {
        const auto& committed = *scrollStates.at(name);
        Check(committed.menuRevision == draftScroll.menuRevision && committed.navigationRevision == draftScroll.navigationRevision
            && committed.selectedIndex == 3, "Applying duration changed page or cursor identity");
    }
    const auto& rootStart = *scrollStates.at("scroll-motion-root-start.json");
    const auto& rootDown = *scrollStates.at("scroll-motion-root-down.json");
    const auto& rootReopened = *scrollStates.at("scroll-motion-root-reopened.json");
    Check(rootStart.selectedIndex == 0 && rootDown.selectedIndex == 1 && rootReopened.selectedIndex == 1
        && rootStart.menuRevision == rootDown.menuRevision && rootDown.menuRevision != rootReopened.menuRevision
        && rootStart.navigationRevision != rootDown.navigationRevision,
        "Actual cursor motion or reopening lost its independent page identity");
    const auto& colorsStart = *scrollStates.at("scroll-motion-colors-start.json");
    const auto& colorsWindow = *scrollStates.at("scroll-motion-colors-window.json");
    const auto& customization = *scrollStates.at("scroll-motion-customization.json");
    Check(colorsStart.selectedIndex == 4 && colorsWindow.selectedIndex == 5
        && colorsStart.menuRevision == colorsWindow.menuRevision
        && colorsStart.navigationRevision != colorsWindow.navigationRevision
        && customization.menuRevision != colorsWindow.menuRevision && customization.selectedIndex == 5,
        "Actual window motion or Back lost page identity or the retained parent selection");
    // Re-selecting an active source mode acknowledges input without replacing its context
    Check(rainbowStarted && rainbowNoOp && rainbowStopped
        && rainbowStarted->navigationRevision == rainbowNoOp->navigationRevision
        && rainbowStarted->menuRevision == rainbowNoOp->menuRevision
        && rainbowNoOp->menuRevision == rainbowStopped->menuRevision
        && rainbowNoOpSequence == rainbowStartedSequence + 1
        && rainbowStarted->accent == rainbowNoOp->accent
        && rainbowStarted->appearance == rainbowNoOp->appearance
        && rainbowStarted->selectedIndex == rainbowNoOp->selectedIndex,
        "Re-selecting Smooth Rainbow changed its palette, context or acknowledgement contract");
    Check(!rainbowStopped->smoothRainbowTheme && rainbowStopped->accent == "#98FF98",
        "Selecting a fixed color failed to stop the actual rainbow mode");
    Check(originalEpoch == draftEpoch && changedEpoch != draftEpoch,
        "Controller draft updates changed navigation or real navigation lost its epoch");
    Check(actualEditor && actualParent && actualRebuild && actualPause
        && actualRebuild->navigationRevision == actualPause->navigationRevision
        && actualPause->navigationRevision == actualParent->navigationRevision
        && actualEditor->navigationRevision != actualParent->navigationRevision,
        "Editor completion must establish a different parent navigation epoch");
    Check(positionEntering && positionEditing && positionMoved && positionExiting && positionParent
        && positionEntering->navigationRevision == positionEditing->navigationRevision
        && positionEditing->navigationRevision == positionMoved->navigationRevision
        && positionMoved->navigationRevision == positionExiting->navigationRevision
        && positionParent->navigationRevision != positionExiting->navigationRevision,
        "Actual position phases must share one editor context and return to a different parent epoch");
    Check(runtime::InputModeForSnapshot(*positionEntering) == runtime::InputMode::MenuLocked
        && runtime::InputModeForSnapshot(*positionEditing) == runtime::InputMode::InstructionPositionEditor
        && runtime::InputModeForSnapshot(*positionExiting) == runtime::InputMode::MenuLocked
        && runtime::InputModeForSnapshot(*positionParent) == runtime::InputMode::Menu,
        "Actual producer phases enabled movement during a transition or kept the parent in editor routing");
    runtime::NativeInputRouter editorRouter(bindings);
    std::set<int> editorKeys;
    const auto editorPoll = [&](std::uint64_t time, const MenuSnapshot& state) {
        return editorRouter.Poll(true, state.isOpen, false, [&](int key) { return editorKeys.contains(key); },
            time, state.navigationRevision, state.appearance.quickExit, state.isRootMenu,
            state.menuUnlock ? runtime::InputMode::MenuLocked
                : state.widthEditor ? runtime::InputMode::WidthEditor : runtime::InputMode::Menu);
    };
    editorPoll(0, *actualEditor); editorKeys = {1};
    Check(editorPoll(0, *actualEditor) == runtime::Input::IncreaseWidth,
        "An actual editor export dispatched normal Down instead of a width command");
    editorKeys.clear(); editorPoll(10, *actualEditor); editorKeys = {86};
    Check(editorPoll(10, *actualEditor) == runtime::Input::FinishWidthEditor,
        "An actual editor export dispatched Back or Close instead of Finish");
    Check(!editorPoll(20, *actualRebuild), "Rebuilding the parent replayed the held editor exit key");
    editorKeys = {70};
    Check(!editorPoll(70, *actualPause), "The actual visible pause dispatched a selection");
    Check(!editorPoll(170, *actualParent) && !editorPoll(180, *actualParent),
        "A key pressed during the actual pause replayed into the resumed parent");
    editorKeys.clear(); editorPoll(190, *actualParent); editorKeys = {1};
    Check(editorPoll(190, *actualParent) == runtime::Input::Down,
        "The actual finished editor retained width routing in its normal parent");
    runtime::NativeInputRouter router{runtime::Bindings{{2}, {86}, {2}, {1}, {70}, {51}, {52}}};
    bool held = false;
    const auto keys = [&](int key) { return held && key == 52; };
    router.Poll(true, true, false, keys, 0, originalEpoch);
    held = true;
    Check(router.Poll(true, true, false, keys, 0, originalEpoch) == runtime::Input::NextChoice,
        "Actual controller epoch rejected a fresh saved choice");
    Check(router.Poll(true, true, false, keys, 180, draftEpoch) == runtime::Input::NextChoice,
        "An actual draft snapshot interrupted the same-row repeat");
    Check(!router.Poll(true, true, false, keys, 360, changedEpoch),
        "A real controller navigation snapshot replayed the held choice");
    // The twenty phase profiles join separately without weakening the established fifty-two checks
    BackendPreferenceRefreshStatesPreserveActualControllerPhases(directory);
    BackendMenuDesignStatesPreserveActualControllerChoicesAndTransitions(directory);
    BackendMenuOpeningStatesPreserveActualControllerEventsAndInputIndependence(directory);
    BackendMenuOpenTransitionStatesPreserveActualRedrawsAndIndependentInputReadiness(directory);
    BackendStealthStatesPreserveActualTransitionsCapacityAndFreshInput(directory);
    BackendOpeningControlsPreserveActualPolicyRowsAndCapturedEpochs(directory);
    BackendOpeningPoliciesPreserveIndependentOwnersAndGlobalCommandScopes(directory);
    BackendLocalEligibilityPreservesActualScopedFamiliesEpochsWaitsAndInertRows(directory);
    BackendSpacerScrollPromotesExactControllerAcksAcrossAllProductionBranches(directory);
}

}
