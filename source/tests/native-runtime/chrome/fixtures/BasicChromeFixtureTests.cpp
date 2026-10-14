#include "BasicChromeFixtureTests.h"
#include "BasicChromeFixtureFiles.h"
#include "../../TestSupport.h"
#include "../../../native-layout/BackendPreview.h"
#include "../../../../native/render/chrome/BasicScrollerHistory.h"
#include "../../../../native/render/chrome/BasicScrollerPresence.h"
#include "../../../../native/render/chrome/BasicScrollerClosedObservation.h"
#include "../../../../native/render/SmoothRainbow.h"

namespace iw::overlay::tests::chrome {
void ActualBasicChromeGraphKeepsCreationSeparateFromRowsCallbacksAndClose(const std::filesystem::path& directory) {
    CheckChromeInventory(directory);
    BasicScrollerHistory history;
    for (std::size_t index = 0; index < ChromeProfiles.size(); ++index) {
        const auto& expected = ChromeProfiles[index];
        const auto path = directory / expected.file;
        const auto bytes = ReadChromePacket(path);
        const auto parsed = runtime::ParseState(bytes, 42);
        const auto& state = *parsed.snapshot;
        const auto preview = ReadBackendPreview(path);
        const auto wire = Json::parse(bytes);
        // These epochs and raw rows come from the controller, not from presentation history
        RequireChrome(state.menuChromeRevision == expected.chrome && state.navigationRevision == expected.navigation
            && state.menuRevision == expected.menu && parsed.lastSequence == expected.sequence
            && state.selectedIndex == expected.selected && state.rows.size() == expected.rows
            && state.isOpen == expected.open && state.smoothRainbowTheme == expected.rainbow
            && state.appearance.design == MenuDesign::Basic && state.menuSurface == MenuSurface::Main
            && state.context == "CONNECTED" && state.accent == (index < 4 ? "#3998FE" : "#FF0000"),
            "The Basic controller graph changed creation, command or current theme facts");
        RequireChrome(HasExistingBasicScroller(state) == expected.exists,
            "The Basic graph confused hidden soft locks with destroyed chrome");
        const bool callback = index == 1 || index == 2 || index == 4 || index == 5;
        RequireChrome(state.menuOptionCallback.has_value() == callback,
            "Chrome history changed an independently owned palette callback");
        if (callback) {
            RequireChrome(state.menuOptionCallback->revision == (index < 4 ? 1u : 2u)
                && state.menuOptionCallback->kind == MenuOptionCallbackKind::Boolean
                && state.menuOptionCallback->phase == ((index == 1 || index == 4)
                    ? MenuOptionCallbackPhase::Pending : MenuOptionCallbackPhase::Cooldown),
                "The actual palette callback changed its source redraw boundary");
        }
        RequireChrome(state.localMenuEligibility && state.localMenuEligibility->revision == (index >= 14 ? 11u : 10u)
            && state.localMenuEligibility->life == PlayerLifeState::Alive && state.localMenuEligibility->hasMenu == true
            && !state.localMenuEligibility->blocked,
            "Chrome history replaced an authenticated admission fact");
        const bool controlsDisabled = callback || index == 9 || index == 10 || index == 11 || index == 12 || index == 15;
        RequireChrome(state.localMenuEligibility->controlsDisabled == controlsDisabled,
            "Historical RGB changed the actual local input lock");
        const auto expectedMode = index == 11 ? runtime::InputMode::WidthEditor
            : (controlsDisabled ? runtime::InputMode::MenuLocked : runtime::InputMode::Menu);
        RequireChrome(runtime::InputModeForSnapshot(state) == expectedMode,
            "Creation history introduced an input route or dropped an existing wait");
        // Public creation metadata is a scalar only and never carries an actor address or account ID
        RequireChrome(wire.at("menuChromeRevision").is_number_unsigned()
            && bytes.find("1234567890ABCDEF") == std::string::npos,
            "The chrome descriptor exposed private identity or lost its scalar shape");
        RequireChrome(preview.menuChromeRevision == state.menuChromeRevision
            && preview.menuRevision == state.menuRevision && preview.navigationRevision == state.navigationRevision
            && preview.menuOptionCallback == state.menuOptionCallback && preview.menuOpenTransition == state.menuOpenTransition
            && preview.menuUnlock == state.menuUnlock && preview.localMenuEligibility == state.localMenuEligibility
            && preview.accent == state.accent && preview.smoothRainbowTheme == state.smoothRainbowTheme,
            "The preview and runtime readers disagree on actual chrome ownership");
        for (std::size_t row = 0; row < state.rows.size(); ++row)
            RequireChrome(preview.rows[row].value == state.rows[row].value
                && preview.rows[row].enabled == state.rows[row].enabled,
                "Chrome ownership altered a current row value or permission");
        if (!state.isOpen) ObserveClosedBasicScroller(history, &state);
        const auto sample = state.smoothRainbowTheme
            ? std::optional<layout::Color>{SampleSmoothRainbow(expected.clock)} : std::nullopt;
        const auto color = history.Update(state.menuChromeRevision, expected.exists, sample);
        RequireChrome(color.has_value() == expected.exists && (!color || SameChromeColor(*color, expected.color)),
            "Actual creation and fixed palette frames lost the last direct Basic RGB sample");
        if (state.rows.size() == 37) {
            const auto paintRevision = index < 2 ? 1u : (index < 5 ? 2u : 3u);
            const auto paintedOn = index < 2 ? 6u : (index < 5 ? 36u : 0u);
            RequireChrome(state.menuBooleanPaint && state.menuBooleanPaint->revision == paintRevision
                && state.menuBooleanPaint->count == 37 && preview.menuBooleanPaint == state.menuBooleanPaint,
                "Scroller history replaced copied checkbox ownership");
            for (std::size_t row = 0; row < 37; ++row)
                RequireChrome(state.menuBooleanPaint->rows[row].index == row
                    && BooleanPaintText(state.menuBooleanPaint->rows[row].value) == (row == paintedOn ? "ON" : "OFF"),
                    "Creation history changed the actual delayed palette sample");
            const auto currentOn = index == 0 ? 6u : (index < 4 ? 36u : 0u);
            for (std::size_t row = 0; row < state.rows.size(); ++row)
                RequireChrome(state.rows[row].value == (row == currentOn ? "ON" : "OFF"),
                    "Historical scroller RGB rewrote current palette booleans");
        }
    }
}
}
