#include "InstructionOptionFixtureExpected.h"
#include "InstructionOptionFixtureTests.h"
#include "../../TestSupport.h"
#include "../../../native-layout/BackendPreview.h"
#include "../../../../native/runtime/ControlLabels.h"
#include "../../../../native/shared/paint/RowDisplayValue.h"
#include <fstream>
#include <map>
#include <set>

namespace iw::overlay::tests::instructionOptions {
namespace {
std::string Read(const std::filesystem::path& path) {
    // A bounded owned copy rejects non-regular files and concurrent growth or truncation
    Check(std::filesystem::symlink_status(path).type() == std::filesystem::file_type::regular,
        "An instruction fixture is not a regular file");
    const auto size = std::filesystem::file_size(path);
    Check(size > 0 && size <= runtime::MaximumFileBytes, "An instruction fixture is empty or oversized");
    std::ifstream input(path, std::ios::binary); Check(input.good(), "An instruction fixture could not be opened");
    std::string bytes(static_cast<std::size_t>(size), '\0');
    input.read(bytes.data(), static_cast<std::streamsize>(size));
    Check(input.gcount() == static_cast<std::streamsize>(size) && input.peek() == std::char_traits<char>::eof(),
        "An instruction fixture changed during its bounded read");
    return bytes;
}
const runtime::Bindings SavedKeys{{2}, {86, 6}, {2, 38}, {1, 40}, {70}, {51}, {52}, {1},
    {38}, {40}, {71}, {69}, {16}, {17}, {32}};
void CheckRouting(const std::map<std::string, runtime::ParsedState>& states) {
    // Actual expiry packets cover both a successful Disable and a rejected Adaptive callback
    for (const int held : {1, 2, 38, 40, 70, 86, 6, 51, 52, 69, 32}) {
        for (const auto prefix : {"disable", "adaptive-rejected"}) {
            runtime::NativeInputRouter router(SavedKeys);
            std::set<int> keys;
            const auto poll = [&](const std::string& file, std::uint64_t now, bool pending = false, bool focus = true) {
                const auto& state = *states.at(file).snapshot;
                return router.Poll(focus, state.isOpen, pending, [&](int key) { return keys.contains(key); }, now,
                    state.navigationRevision, state.appearance.quickExit, state.isRootMenu, runtime::InputModeForSnapshot(state),
                    state.appearance.scrollAnimationMilliseconds, state.menuRevision, &*state.mainOpeningControls, 0, 0,
                    &*state.openingPolicies, &*state.localMenuEligibility, state.menuSurface);
            };
            const std::string start = std::string(prefix) + "0.json";
            const std::string redraw = std::string(prefix) + "180.json";
            const std::string ready = std::string(prefix) + "430.json";
            Check(!poll(start, 0), "Instruction callback seed dispatched an input");
            keys = {held};
            Check(!poll(start, 1) && !poll(start, 179, true) && !poll(redraw, 180)
                && !poll(redraw, 429) && !poll(ready, 430) && !poll(ready, 1000),
                "An instruction wait replayed a consumed edge or rejected callback too early");
            keys.clear(); Check(!poll(ready, 1001), "Instruction release dispatched an input");
            keys = {70}; Check(poll(ready, 1002) == runtime::Input::Select,
                "Instruction deadline rejected a later released fresh selection");
            router.Reset(); keys.clear(); poll(start, 2000); keys = {held};
            Check(!poll(start, 2001, false, false) && !poll(redraw, 2180)
                && !poll(ready, 2430), "Instruction focus recovery replayed a held callback edge");
        }
    }
}
}
void ActualInstructionGraphKeepsCurrentFlagsPaintAndCallerWaitsSeparate(const std::filesystem::path& directory) {
    Check(std::filesystem::is_directory(directory), "The actual instruction graph has not been exported");
    std::set<std::string> expectedNames, actualNames;
    for (const auto& expected : Profiles) expectedNames.emplace(expected.file);
    for (const auto& entry : std::filesystem::directory_iterator(directory)) {
        Check(entry.symlink_status().type() == std::filesystem::file_type::regular,
            "Instruction inventory contains a non-regular entry");
        Check(actualNames.emplace(entry.path().filename().string()).second && actualNames.size() <= Profiles.size(),
            "Instruction inventory exceeds its fixed bound");
    }
    Check(actualNames == expectedNames, "Instruction inventory differs from the fixed sixteen-profile graph");
    const auto labels = runtime::DescribeOpeningActions(SavedKeys);
    const std::array<const char*, 6> rowLabels{"Disable", "Alternate Style", "Disable Background", "Adaptive Position", "Reposition", "Reset Position"};
    std::map<std::string, runtime::ParsedState> states;
    for (const auto& expected : Profiles) {
        const auto path = directory / expected.file; const auto bytes = Read(path);
        const auto parsed = runtime::ParseState(bytes, 42, nullptr, nullptr, nullptr, nullptr, nullptr,
            std::nullopt, nullptr, nullptr, &labels);
        const auto& state = *parsed.snapshot;
        const auto preview = ReadBackendPreview(path, &labels); const auto wire = Json::parse(bytes);
        Check(state.isOpen && !state.isRootMenu && state.menuSurface == MenuSurface::Main
            && state.title == "Menu Instructions" && state.context == "CONNECTED" && state.rows.size() == 6
            && state.selectedIndex == expected.selected && state.navigationRevision == expected.navigation
            && state.menuRevision == expected.menu && parsed.lastSequence == expected.sequence && parsed.generation == 0,
            "Actual instruction context, cursor or command identity changed");
        Check(state.accent == "#3998FE" && !state.smoothRainbowTheme && state.appearance.booleanDisplay == "Boxes"
            && !state.appearance.instructionPosition
            && state.appearance.instructionsVisible == ((expected.currentMask & 1u) == 0)
            && state.appearance.alternateInstructions == ((expected.currentMask & 2u) != 0)
            && state.appearance.instructionsBackground == ((expected.currentMask & 4u) == 0)
            && state.appearance.adaptiveInstructions == ((expected.currentMask & 8u) != 0),
            "Instruction paint replaced actual flags or lost the inverse Disable labels");
        Check(state.menuBooleanPaint && state.menuBooleanPaint->revision == expected.paintRevision
            && state.menuBooleanPaint->count == 4 && wire.contains("menuBooleanPaint"),
            "Instruction copied paint changed at an incorrect redraw boundary");
        for (std::size_t row = 0; row < 6; ++row) {
            Check(state.rows[row].label == rowLabels[row] && !state.rows[row].isSpacer,
                "Instruction catalog changed its source labels or topology");
            if (row < 4) {
                const auto current = (expected.currentMask & (1u << row)) != 0 ? "ON" : "OFF";
                const auto copied = (expected.cachedMask & (1u << row)) != 0 ? "ON" : "OFF";
                Check(state.rows[row].value == current && state.rows[row].enabled
                    && state.menuBooleanPaint->rows[row].index == row
                    && BooleanPaintText(state.menuBooleanPaint->rows[row].value) == copied
                    && ResolveRowDisplayValue(state, row) == copied,
                    "Instruction current flags, always-selectable BOOL or independent copied value changed");
            }
            Check(preview.rows[row].label == state.rows[row].label && preview.rows[row].value == state.rows[row].value
                && preview.rows[row].enabled == state.rows[row].enabled
                && ResolveRowDisplayValue(preview, row) == ResolveRowDisplayValue(state, row),
                "Preview reader changed a current instruction value or display sample");
        }
        Check(!state.rows[4].enabled && state.rows[4].value == "UNAVAILABLE" && state.rows[5].enabled
            && state.rows[5].value.empty(), "Instruction BOOL capture manufactured editor availability");
        Check(state.menuOptionCallback == expected.callback && wire.contains("menuOptionCallback") == expected.callback.has_value()
            && runtime::InputModeForSnapshot(state) == (expected.callback ? runtime::InputMode::MenuLocked : runtime::InputMode::Menu),
            "Instruction callback lost its independent source wait or omitted legacy boundary");
        Check(state.localMenuEligibility && state.localMenuEligibility->revision == 10
            && state.localMenuEligibility->life == PlayerLifeState::Alive && state.localMenuEligibility->hasMenu == true
            && state.localMenuEligibility->controlsDisabled == expected.callback.has_value() && !state.localMenuEligibility->blocked,
            "Instruction callback fabricated Busy or actor authorization");
        Check(state.openingPolicies && state.openingPolicies->revision == 1 && state.mainOpeningControls == state.openingPolicies->main
            && state.openingPolicies->main == MainOpeningControls{1, {OpeningAction::Aim, OpeningAction::Melee}}
            && state.openingPolicies->quick == MainOpeningControls{1, {OpeningAction::Aim, OpeningAction::SecondaryOffhand}}
            && !state.openingPolicies->quickMenuDisabled && !state.spacerScrollReceipt,
            "Instruction display ownership changed the captured opening policy");
        Check(!state.menuOpening && !state.menuOpenTransition && !state.menuUnlock && !state.widthEditor
            && !state.instructionPositionEditor && !state.menuPositionEditor && !state.menuCredits
            && !state.menuPreferenceRefresh && !state.menuPreferenceRebuild && !state.menuDesignTransition,
            "Instruction graph added an unrelated modal lifecycle");
        if (std::string_view(expected.file).starts_with("adaptive-rejected"))
            Check(state.notice.find("Adaptive Position") != std::string::npos && state.notice.find("Alternate") != std::string::npos,
                "Selectable rejected Adaptive callback lost its dependency error");
        const auto& paint = wire.at("menuBooleanPaint");
        Check(paint.is_object() && paint.size() == 2 && paint.contains("revision") && paint.contains("rows"),
            "Instruction paint exposed fields beyond its display contract");
        for (const auto& row : paint.at("rows"))
            Check(row.is_object() && row.size() == 2 && row.contains("index") && row.contains("value"),
                "Copied instruction row exposed fields beyond its display sample");
        Check(bytes.find("1234567890ABCDEF") == std::string::npos && !wire.contains("spacerScrollReceipt"),
            "Instruction public metadata exposed private identity or receipt data");
        Check(preview.rows.size() == state.rows.size() && preview.selectedIndex == state.selectedIndex
            && preview.menuBooleanPaint == state.menuBooleanPaint && preview.menuOptionCallback == state.menuOptionCallback
            && preview.appearance == state.appearance && preview.accent == state.accent
            && preview.localMenuEligibility == state.localMenuEligibility && preview.openingPolicies == state.openingPolicies
            && preview.mainOpeningControls == state.mainOpeningControls && preview.navigationRevision == state.navigationRevision
            && preview.menuRevision == state.menuRevision && preview.menuSurface == state.menuSurface,
            "Runtime and preview readers disagree on immutable instruction ownership");
        const auto command = Json::parse(runtime::CommandJson(42, expected.sequence + 1, runtime::Input::Select,
            state.navigationRevision, state.openingPolicies->revision));
        Check(command.at("navigationRevision") == expected.navigation && command.at("openingControlsRevision") == 1
            && !command.contains("localAdmissionRevision"), "Instruction Select lost captured scope or fabricated opening admission");
        auto legacy = wire; legacy.erase("menuOptionCallback"); legacy.erase("menuBooleanPaint");
        const auto legacyState = runtime::ParseState(legacy.dump(), 42);
        Check(!legacyState.snapshot->menuBooleanPaint && !legacyState.snapshot->menuOptionCallback
            && legacyState.snapshot->appearance == state.appearance,
            "Descriptor omission changed the legacy current instruction state");
        states.emplace(expected.file, parsed);
    }
    CheckRouting(states);
}
}
