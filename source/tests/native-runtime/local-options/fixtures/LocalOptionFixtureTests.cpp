#include "LocalOptionFixtureExpected.h"
#include "LocalOptionFixtureTests.h"
#include "../../TestSupport.h"
#include "../../../native-layout/BackendPreview.h"
#include "../../../../native/runtime/ControlLabels.h"
#include "../../../../native/shared/paint/RowDisplayValue.h"
#include <fstream>
#include <map>
#include <set>

namespace iw::overlay::tests::localOptions {
namespace {
std::string Read(const std::filesystem::path& path) {
    // Copy a bounded regular file and reject concurrent growth or truncation
    Check(std::filesystem::symlink_status(path).type() == std::filesystem::file_type::regular,
        "A local option fixture is not a regular file");
    const auto size = std::filesystem::file_size(path);
    Check(size > 0 && size <= runtime::MaximumFileBytes, "A local option fixture is empty or oversized");
    std::ifstream input(path, std::ios::binary);
    Check(input.good(), "A local option fixture could not be opened");
    std::string bytes(static_cast<std::size_t>(size), '\0');
    input.read(bytes.data(), static_cast<std::streamsize>(size));
    Check(input.gcount() == static_cast<std::streamsize>(size) && input.peek() == std::char_traits<char>::eof(),
        "A local option fixture changed during its bounded read");
    return bytes;
}
const runtime::Bindings SavedKeys{{2}, {86, 6}, {2, 38}, {1, 40}, {70}, {51}, {52}, {1},
    {38}, {40}, {71}, {69}, {16}, {17}, {32}};
void CheckRouting(const std::map<std::string, runtime::ParsedState>& states) {
    // Wait expiry must not replay an edge pressed during either local caller lifecycle
    for (const int held : {1, 2, 38, 40, 70, 86, 6, 51, 52, 69, 32}) {
        runtime::NativeInputRouter router(SavedKeys);
        std::set<int> keys;
        const auto poll = [&](const char* file, std::uint64_t now, bool pending = false, bool focus = true) {
            const auto& state = *states.at(file).snapshot;
            return router.Poll(focus, state.isOpen, pending, [&](int key) { return keys.contains(key); }, now,
                state.navigationRevision, state.appearance.quickExit, state.isRootMenu, runtime::InputModeForSnapshot(state),
                state.appearance.scrollAnimationMilliseconds, state.menuRevision, &*state.mainOpeningControls, 0, 0,
                &*state.openingPolicies, &*state.localMenuEligibility, state.menuSurface);
        };
        Check(!poll("palette0.json", 0), "Palette wait seed dispatched input");
        keys = {held};
        Check(!poll("palette0.json", 1) && !poll("palette179.json", 179, true)
            && !poll("palette180.json", 180) && !poll("palette430.json", 430)
            && !poll("palette430.json", 1000), "Palette wait replayed a consumed edge");
        keys.clear(); Check(!poll("palette430.json", 1001), "Palette release dispatched input");
        keys = {70}; Check(poll("palette430.json", 1002) == runtime::Input::Select,
            "Palette unlock rejected a fresh selection");
        for (const auto& pair : {std::pair{"slider-changed0.json", "slider250.json"},
            std::pair{"reset0.json", "reset250.json"}}) {
            router.Reset(); keys.clear(); Check(!poll(pair.first, 2000), "Plain wait seed dispatched input");
            keys = {held};
            Check(!poll(pair.first, 2001) && !poll(pair.first, 2249)
                && !poll(pair.second, 2250) && !poll(pair.second, 2500), "Plain wait replayed a consumed edge");
            keys.clear(); poll(pair.second, 2501); keys = {70};
            Check(poll(pair.second, 2502) == runtime::Input::Select, "Plain unlock rejected a fresh selection");
        }
        router.Reset(); keys.clear(); poll("slider-rejected0.json", 3000); keys = {held};
        Check(!poll("slider-rejected0.json", 3001) && !poll("slider-rejected0.json", 3100, false, false)
            && !poll("slider-rejected0.json", 3200), "Rejected slider or focus recovery bypassed its owned wait");
    }
}
}
void ActualLocalOptionGraphKeepsCurrentStatePaintAndCallerWaitsSeparate(const std::filesystem::path& directory) {
    Check(std::filesystem::is_directory(directory), "The real local option graph has not been exported");
    std::set<std::string> expectedNames, actualNames;
    for (const auto& expected : Profiles) expectedNames.emplace(expected.file);
    for (const auto& entry : std::filesystem::directory_iterator(directory)) {
        Check(entry.symlink_status().type() == std::filesystem::file_type::regular,
            "Local option inventory contains a non-regular entry");
        Check(actualNames.emplace(entry.path().filename().string()).second && actualNames.size() <= Profiles.size(),
            "Local option inventory exceeds its fixed bound");
    }
    Check(actualNames == expectedNames, "Local option inventory differs from the fixed twelve-profile graph");
    const auto labels = runtime::DescribeOpeningActions(SavedKeys);
    std::map<std::string, runtime::ParsedState> states;
    for (const auto& expected : Profiles) {
        const auto path = directory / expected.file;
        const auto bytes = Read(path);
        const auto parsed = runtime::ParseState(bytes, 42, nullptr, nullptr, nullptr, nullptr, nullptr,
            std::nullopt, nullptr, nullptr, &labels);
        const auto& state = *parsed.snapshot;
        const auto preview = ReadBackendPreview(path, &labels);
        const auto wire = Json::parse(bytes);
        Check(state.isOpen && !state.isRootMenu && state.menuSurface == MenuSurface::Main
            && state.title == expected.title && state.context == "CONNECTED"
            && state.rows.size() == expected.rows && state.selectedIndex == expected.selected
            && state.navigationRevision == expected.navigation && state.menuRevision == expected.menu
            && parsed.lastSequence == expected.sequence && parsed.generation == 0,
            "Actual local option page, cursor, ACK or presentation epoch changed");
        Check(state.accent == expected.accent && state.smoothRainbowTheme == expected.rainbow
            && state.appearance.booleanDisplay == expected.display && state.appearance.stealthUI == expected.stealth,
            "Local caller wait delayed or rewrote actual theme or slider state");
        Check(state.menuOptionCallback == expected.callback
            && wire.contains("menuOptionCallback") == expected.callback.has_value()
            && runtime::InputModeForSnapshot(state) == (expected.callback ? runtime::InputMode::MenuLocked : runtime::InputMode::Menu),
            "Local callback wire omission or input wait differs from its source lifecycle");
        Check(state.localMenuEligibility && state.localMenuEligibility->revision == 10
            && state.localMenuEligibility->life == PlayerLifeState::Alive && state.localMenuEligibility->hasMenu == true
            && state.localMenuEligibility->controlsDisabled == expected.callback.has_value()
            && !state.localMenuEligibility->blocked, "Local callback confused the actual wait with backend Busy or authorization");
        Check(state.openingPolicies && state.openingPolicies->revision == 1
            && state.mainOpeningControls == state.openingPolicies->main
            && state.openingPolicies->main == MainOpeningControls{1, {OpeningAction::Aim, OpeningAction::Melee}}
            && state.openingPolicies->quick == MainOpeningControls{1, {OpeningAction::Aim, OpeningAction::SecondaryOffhand}}
            && !state.openingPolicies->quickMenuDisabled && !state.spacerScrollReceipt,
            "Local option callback fabricated or replaced a private admission owner");
        Check(!state.menuOpening && !state.menuOpenTransition && !state.menuUnlock && !state.widthEditor
            && !state.instructionPositionEditor && !state.menuPositionEditor && !state.menuCredits
            && !state.menuPreferenceRefresh && !state.menuPreferenceRebuild && !state.menuDesignTransition,
            "Local option graph fabricated an unrelated modal or redraw lifecycle");
        Check(state.menuBooleanPaint.has_value() == (expected.paintRevision != 0)
            && wire.contains("menuBooleanPaint") == (expected.paintRevision != 0),
            "Local option graph lost its omitted paint boundary");
        if (expected.rows == 37) {
            const auto& paint = *state.menuBooleanPaint;
            Check(paint.revision == expected.paintRevision && paint.count == 37 && state.rows[6].label == "Dodger Blue"
                && state.rows[7].label == "Ocean Blue" && state.rows[36].label == "Smooth Rainbow",
                "Palette redraw lost its complete source catalog or paint revision");
            for (std::size_t row = 0; row < 37; ++row) {
                const auto raw = static_cast<int>(row) == expected.currentOn ? "ON" : "OFF";
                const auto cached = static_cast<int>(row) == expected.cachedOn ? "ON" : "OFF";
                Check(state.rows[row].value == raw && state.rows[row].enabled && paint.rows[row].index == row
                    && BooleanPaintText(paint.rows[row].value) == cached && ResolveRowDisplayValue(state, row) == cached,
                    "Palette current state or copied checkbox sample changed at an incorrect deadline");
            }
        } else if (expected.rows == 12) {
            Check(state.rows[1].label == "Bool Display" && state.rows[1].enabled
                && state.rows[1].value == (expected.stealth ? "Boxes" : "Text") && !state.appearance.optionCounter,
                "Slider draft was confused with its accepted or rejected applied value");
        } else {
            // Instruction reset keeps the four copied BOOL rows without resampling them
            const auto& paint = *state.menuBooleanPaint;
            Check(paint.revision == 1 && paint.count == 4,
                "Plain reset replaced or resampled the instruction paint owner");
            for (std::size_t row = 0; row < 4; ++row)
                Check(paint.rows[row].index == row && BooleanPaintText(paint.rows[row].value) == "OFF"
                    && state.rows[row].value == "OFF" && ResolveRowDisplayValue(state, row) == "OFF",
                    "Plain reset changed an instruction checkbox sample");
            Check(state.rows[5].label == "Reset Position" && state.rows[5].value.empty() && state.rows[5].enabled
                && !state.appearance.instructionPosition, "Plain reset fabricated movement or a disabled action");
        }
        if (expected.callback) {
            const auto& callback = wire.at("menuOptionCallback");
            Check(callback.is_object() && callback.size() == 3 && callback.contains("revision")
                && callback.contains("kind") && callback.contains("phase"), "Callback exposed fields beyond its typed clock");
        }
        Check(bytes.find("1234567890ABCDEF") == std::string::npos && !wire.contains("spacerScrollReceipt"),
            "Public local graph exposed private identity or a native receipt");
        // The command uses captured public epochs without inventing a new local opening scope
        const auto command = Json::parse(runtime::CommandJson(42, expected.sequence + 1, runtime::Input::Select,
            state.navigationRevision, state.openingPolicies->revision));
        Check(command.at("navigationRevision") == expected.navigation && command.at("openingControlsRevision") == 1
            && !command.contains("localAdmissionRevision"), "Local selection fabricated or lost command scope");
        Check(preview.menuOptionCallback == state.menuOptionCallback && preview.menuBooleanPaint == state.menuBooleanPaint
            && preview.localMenuEligibility == state.localMenuEligibility && preview.openingPolicies == state.openingPolicies
            && preview.mainOpeningControls == state.mainOpeningControls && preview.menuSurface == state.menuSurface
            && preview.navigationRevision == state.navigationRevision && preview.menuRevision == state.menuRevision
            && preview.accent == state.accent && preview.smoothRainbowTheme == state.smoothRainbowTheme
            && preview.appearance == state.appearance && preview.selectedIndex == state.selectedIndex
            && preview.rows.size() == state.rows.size(), "Both actual readers disagree on local option ownership");
        for (std::size_t row = 0; row < state.rows.size(); ++row)
            Check(preview.rows[row].label == state.rows[row].label && preview.rows[row].value == state.rows[row].value
                && preview.rows[row].enabled == state.rows[row].enabled
                && ResolveRowDisplayValue(preview, row) == ResolveRowDisplayValue(state, row),
                "Preview paint replaced current values or capabilities");
        // Omitting the new descriptors preserves the existing current-value legacy shape
        auto legacy = wire; legacy.erase("menuOptionCallback"); legacy.erase("menuBooleanPaint");
        const auto legacyState = runtime::ParseState(legacy.dump(), 42);
        Check(!legacyState.snapshot->menuOptionCallback && !legacyState.snapshot->menuBooleanPaint
            && legacyState.snapshot->accent == state.accent && legacyState.snapshot->rows[expected.selected].value == state.rows[expected.selected].value,
            "Legacy omission changed actual option state");
        states.emplace(expected.file, parsed);
    }
    CheckRouting(states);
}
}
