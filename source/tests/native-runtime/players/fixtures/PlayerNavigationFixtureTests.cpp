#include "PlayerNavigationFixtureExpected.h"
#include "PlayerNavigationFixtureTests.h"
#include "../../TestSupport.h"
#include "../../../native-layout/BackendPreview.h"
#include "../../../../native/runtime/ControlLabels.h"
#include "../../../../native/shared/paint/RowDisplayValue.h"
#include <fstream>
#include <map>
#include <set>

namespace iw::overlay::tests::playerMenus {
namespace {
const runtime::Bindings SavedKeys{{2}, {86, 6}, {2, 38}, {1, 40}, {70}, {51}, {52}, {1},
    {38}, {40}, {71}, {69}, {16}, {17}, {32}};
std::string Read(const std::filesystem::path& path) {
    Check(std::filesystem::symlink_status(path).type() == std::filesystem::file_type::regular,
        "An actual player fixture is not a regular file");
    const auto size = std::filesystem::file_size(path);
    Check(size > 0 && size <= runtime::MaximumFileBytes, "An actual player fixture is empty or oversized");
    std::ifstream input(path, std::ios::binary); Check(input.good(), "An actual player fixture could not be opened");
    std::string bytes(static_cast<std::size_t>(size), '\0');
    input.read(bytes.data(), static_cast<std::streamsize>(size));
    Check(input.gcount() == static_cast<std::streamsize>(size) && input.peek() == std::char_traits<char>::eof(),
        "An actual player fixture changed during its bounded read");
    return bytes;
}
void CheckSourceRows(const MenuSnapshot& state, const Expected& expected) {
    if (expected.rows == 31) {
        for (std::size_t index = 0; index < Basic.size(); ++index) {
            Check(state.rows[index].label == Basic[index], "Selected Basic Scripts lost source declaration order");
            if (index != 0) Check(!state.rows[index].enabled && state.rows[index].value == "UNAVAILABLE",
                "An unsupported player callback claims an applied value or game capability");
        }
    } else if (expected.rows == 12) {
        for (std::size_t index = 0; index < AllPlayers.size(); ++index) {
            Check(state.rows[index].label == AllPlayers[index], "All Players lost its eleven source rows or named IW extension");
            if (index != 11) Check(!state.rows[index].enabled && state.rows[index].value == "UNAVAILABLE",
                "A source group declaration cannot borrow another feature's support");
        }
        Check(state.rows[11].enabled && state.rows[11].value == "OFF", "The IW lobby extension lost its separate confirmed state");
    } else if (!expected.quick) {
        for (std::size_t index = 0; index < Options.size(); ++index) {
            Check(state.rows[index].label == Options[index], "Options lost its fourteen source declarations");
            Check(state.rows[index].enabled == (index == 1), "Options exposed an unsupported callback or hid captured target browsing");
            Check(state.rows[index].value == (index == 1 ? ">" : "UNAVAILABLE"), "Options fabricated a feature state");
        }
    } else {
        Check(state.rows[0].label == "God Mode" && state.rows[3].label == "Unlimited Ammo"
            && !state.rows[3].enabled && state.rows[3].value == "UNAVAILABLE", "Quick self gained unsupported per-player ammo");
    }
}
void CheckRouting(const std::map<std::string, runtime::ParsedState>& states) {
    // Every held edge is consumed through pending and cooldown, including late healthy reports
    for (const int held : {1, 2, 38, 40, 70, 86, 6, 51, 52, 69, 32}) {
        runtime::NativeInputRouter router(SavedKeys); std::set<int> keys;
        const auto poll = [&](const char* name, std::uint64_t now, bool pending = false, bool foreground = true) {
            const auto& state = *states.at(name).snapshot;
            return router.Poll(foreground, state.isOpen, pending, [&](int key) { return keys.contains(key); }, now,
                state.navigationRevision, state.appearance.quickExit, state.isRootMenu, runtime::InputModeForSnapshot(state),
                state.appearance.scrollAnimationMilliseconds, state.menuRevision, &*state.mainOpeningControls, 0, 0,
                &*state.openingPolicies, &*state.localMenuEligibility, state.menuSurface);
        };
        Check(!poll("player-navigation-peer-boolean0.json", 0), "A player callback seed emitted input");
        keys = {held};
        Check(!poll("player-navigation-peer-boolean0.json", 1) && !poll("player-navigation-peer-boolean179.json", 179, true)
            && !poll("player-navigation-peer-boolean180.json", 180) && !poll("player-navigation-peer-boolean429.json", 429)
            && !poll("player-navigation-peer-boolean430.json", 430) && !poll("player-navigation-peer-boolean430.json", 1000),
            "A player callback or unlock replayed a held edge");
        keys.clear(); Check(!poll("player-navigation-peer-boolean430.json", 1001), "Releasing a player edge emitted input");
        keys = {70}; Check(poll("player-navigation-peer-boolean430.json", 1002) == runtime::Input::Select,
            "The player callback unlock refused a fresh ordinary Select");
        router.Reset(); keys.clear(); poll("player-navigation-peer-options.json", 2000); keys = {held};
        Check(!poll("player-navigation-peer-options.json", 2001) && !poll("player-navigation-peer-options.json", 2100, false, false)
            && !poll("player-navigation-peer-parent.json", 2250), "Plain selection or focus recovery replayed an old edge");
    }
}
}
void ActualPlayerNavigationKeepsSourceInventoryScopesAndCallerLocks(const std::filesystem::path& directory) {
    Check(std::filesystem::is_directory(directory), "The actual player navigation graph has not been exported");
    std::set<std::string> actual, wanted;
    for (const auto& expected : Profiles) wanted.emplace(expected.file);
    for (const auto& entry : std::filesystem::directory_iterator(directory)) {
        Check(entry.symlink_status().type() == std::filesystem::file_type::regular, "Player inventory contains a non-regular entry");
        Check(actual.emplace(entry.path().filename().string()).second && actual.size() <= Profiles.size(), "Player inventory exceeded its fixed bound");
    }
    Check(actual == wanted, "Player inventory differs from the fixed twenty-profile graph");
    const auto labels = runtime::DescribeOpeningActions(SavedKeys);
    std::map<std::string, runtime::ParsedState> states;
    for (const auto& expected : Profiles) {
        const auto path = directory / expected.file; const auto bytes = Read(path);
        const auto wire = Json::parse(bytes);
        const auto parsed = runtime::ParseState(bytes, 42, nullptr, nullptr, nullptr, nullptr, nullptr,
            std::nullopt, nullptr, nullptr, &labels);
        Check(parsed.snapshot != nullptr, "Production parsing returned no player snapshot");
        const auto& state = *parsed.snapshot; const auto preview = ReadBackendPreview(path, &labels);
        Check(state.rows.size() == expected.rows && preview.rows.size() == expected.rows
            && wire.at("rows").size() == expected.rows, "A player row count differs between producer and readers");
        Check(state.isOpen && state.isRootMenu == expected.quick && state.title == expected.title && state.context == "CONNECTED"
            && state.selectedIndex == expected.selected && state.navigationRevision == expected.navigation
            && state.menuRevision == expected.menu && parsed.lastSequence == expected.sequence && parsed.generation == 0,
            "A captured actor page, cursor, ACK or callback epoch changed");
        Check(state.menuSurface == (expected.quick ? MenuSurface::Quick : MenuSurface::Main)
            && state.rows[0].value == expected.current && state.rows[0].enabled == expected.enabled,
            "Self and selected-peer facts crossed menu families or private ownership");
        CheckSourceRows(state, expected);
        Check(state.menuOptionCallback == expected.callback
            && runtime::InputModeForSnapshot(state) == (expected.callback ? runtime::InputMode::MenuLocked : runtime::InputMode::Menu),
            "The actual player caller wait changed input admission");
        Check(state.localMenuEligibility && state.localMenuEligibility->revision == 10
            && state.localMenuEligibility->life == PlayerLifeState::Alive && state.localMenuEligibility->hasMenu == true
            && state.localMenuEligibility->controlsDisabled == expected.callback.has_value()
            && !state.localMenuEligibility->blocked, "Player state confused callback waiting with local actor authority");
        Check(state.menuBooleanPaint.has_value() == (expected.paintRevision != 0), "Captured BOOL paint availability changed");
        if (expected.paintRevision != 0) {
            const auto& paint = *state.menuBooleanPaint;
            Check(paint.revision == expected.paintRevision && paint.count == 1 && paint.rows[0].index == 0
                && BooleanPaintText(paint.rows[0].value) == expected.cached && ResolveRowDisplayValue(state, 0) == expected.cached,
                "Selected actor current values replaced its copied checkbox before the source redraw");
        }
        Check(preview.menuOptionCallback == state.menuOptionCallback && preview.menuBooleanPaint == state.menuBooleanPaint
            && preview.localMenuEligibility == state.localMenuEligibility && preview.mainOpeningControls == state.mainOpeningControls
            && preview.openingPolicies == state.openingPolicies && preview.menuSurface == state.menuSurface
            && preview.navigationRevision == state.navigationRevision && preview.menuRevision == state.menuRevision
            && preview.selectedIndex == state.selectedIndex && preview.title == state.title && preview.context == state.context,
            "Production and preview readers disagree on the actual player scope");
        for (std::size_t row = 0; row < expected.rows; ++row) {
            Check(preview.rows[row].label == state.rows[row].label && preview.rows[row].value == state.rows[row].value
                && preview.rows[row].enabled == state.rows[row].enabled
                && ResolveRowDisplayValue(preview, row) == ResolveRowDisplayValue(state, row), "A reader rewrote current actor facts");
        }
        Check(bytes.find("1234567890ABCDEF") == std::string::npos && bytes.find("ABCDEF0123456789") == std::string::npos
            && !wire.contains("selectedPlayerTarget") && !wire.contains("expectedTarget") && !wire.contains("selectionScope")
            && !wire.contains("spacerScrollReceipt"), "A public player packet exposed private identity or native proof");
        states.emplace(expected.file, parsed);
    }
    CheckRouting(states);
}
}
