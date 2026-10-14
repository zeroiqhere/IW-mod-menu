#include "ClosedLevelOpeningGraphSupport.h"
#include <array>
#include <algorithm>

namespace iw::overlay::tests::closedLevelGraph {
namespace {
struct Expected {
    const char* name;
    std::uint64_t ack, navigation, menu;
    bool open;
    MenuSurface surface;
    std::size_t rows, index;
    std::uint64_t policy;
    bool quickDisabled;
    std::uint64_t admission;
    PlayerLifeState life;
    bool hasMenu, controlsDisabled, blocked;
    std::uint64_t wait, receipt;
    int transition;
    std::uint64_t fade;
    int fadeAge;
    const char* label;
    const char* value;
    bool enabled;
};
// Literal epochs and source outcomes are checked before comparing either reader
const std::array expected{
    Expected{"main-before0.json", 1001, 1, 2, true, MenuSurface::Main, 8, 0, 1, false, 10, PlayerLifeState::Alive, true, false, false, 0, 0, -1, 1, 550, "Connect after spawning", "CONNECTED", false},
    Expected{"main-back0.json", 1002, 2, 3, false, MenuSurface::Main, 8, 0, 1, false, 10, PlayerLifeState::Alive, true, false, false, 1, 1, -1, 0, -1, "Connect after spawning", "CONNECTED", false},
    Expected{"main-back249.json", 1002, 2, 3, false, MenuSurface::Main, 8, 0, 1, false, 10, PlayerLifeState::Alive, true, false, false, 1, 1, -1, 0, -1, "Connect after spawning", "CONNECTED", false},
    Expected{"main-back250.json", 1002, 2, 3, false, MenuSurface::Main, 8, 0, 1, false, 10, PlayerLifeState::Alive, true, false, false, 0, 1, -1, 0, -1, "Connect after spawning", "CONNECTED", false},
    Expected{"main-pending250.json", 1003, 2, 3, false, MenuSurface::Main, 8, 0, 1, false, 10, PlayerLifeState::Alive, true, false, true, 0, 0, -1, 0, -1, "Connect after spawning", "CONNECTED", false},
    Expected{"main-rebuild250.json", 1003, 3, 4, true, MenuSurface::Main, 8, 0, 1, false, 10, PlayerLifeState::Alive, true, true, false, 0, 0, 0, 0, -1, "Connect after spawning", "CONNECTED", false},
    Expected{"main-rebuild299.json", 1003, 3, 4, true, MenuSurface::Main, 8, 0, 1, false, 10, PlayerLifeState::Alive, true, true, false, 0, 0, 0, 0, -1, "Connect after spawning", "CONNECTED", false},
    Expected{"main-drawn300.json", 1003, 3, 4, true, MenuSurface::Main, 8, 0, 1, false, 10, PlayerLifeState::Alive, true, true, false, 0, 0, 1, 2, 0, "Connect after spawning", "CONNECTED", false},
    Expected{"main-wait849.json", 1003, 3, 4, true, MenuSurface::Main, 8, 0, 1, false, 10, PlayerLifeState::Alive, true, true, false, 0, 0, 1, 2, 549, "Connect after spawning", "CONNECTED", false},
    Expected{"main-ready850.json", 1003, 3, 4, true, MenuSurface::Main, 8, 0, 1, false, 10, PlayerLifeState::Alive, true, false, false, 0, 0, -1, 2, 550, "Connect after spawning", "CONNECTED", false},
    Expected{"quick-before0.json", 0, 0, 1, false, MenuSurface::Main, 8, 0, 1, false, 10, PlayerLifeState::Alive, true, false, false, 0, 0, -1, 0, -1, "Connect after spawning", "CONNECTED", false},
    Expected{"quick-pending0.json", 1001, 0, 1, false, MenuSurface::Main, 8, 0, 1, false, 10, PlayerLifeState::Alive, true, false, true, 0, 0, -1, 0, -1, "Connect after spawning", "CONNECTED", false},
    Expected{"quick-drawn0.json", 1001, 1, 3, true, MenuSurface::Quick, 14, 0, 1, false, 10, PlayerLifeState::Alive, true, true, false, 0, 0, 1, 1, 0, "God Mode", "OFF", false},
    Expected{"quick-wait549.json", 1001, 1, 3, true, MenuSurface::Quick, 14, 0, 1, false, 10, PlayerLifeState::Alive, true, true, false, 0, 0, 1, 1, 549, "God Mode", "OFF", false},
    Expected{"quick-ready550.json", 1001, 1, 3, true, MenuSurface::Quick, 14, 0, 1, false, 10, PlayerLifeState::Alive, true, false, false, 0, 0, -1, 1, 550, "God Mode", "OFF", true},
    Expected{"dead-before0.json", 0, 0, 1, false, MenuSurface::Main, 8, 0, 1, false, 11, PlayerLifeState::Dead, true, false, false, 0, 0, -1, 0, -1, "Connect after spawning", "CONNECTED", false},
    Expected{"dead-pending0.json", 1001, 0, 1, false, MenuSurface::Main, 8, 0, 1, false, 11, PlayerLifeState::Dead, true, false, true, 0, 0, -1, 0, -1, "Connect after spawning", "CONNECTED", false},
    Expected{"dead-drawn0.json", 1001, 1, 3, true, MenuSurface::Quick, 3, 0, 1, false, 11, PlayerLifeState::Dead, true, true, false, 0, 0, 1, 1, 0, "Respawn", "UNAVAILABLE", false},
    Expected{"dead-wait549.json", 1001, 1, 3, true, MenuSurface::Quick, 3, 0, 1, false, 11, PlayerLifeState::Dead, true, true, false, 0, 0, 1, 0, -1, "Respawn", "UNAVAILABLE", false},
    Expected{"dead-ready550.json", 1001, 1, 3, true, MenuSurface::Quick, 3, 0, 1, false, 11, PlayerLifeState::Dead, true, false, false, 0, 0, -1, 0, -1, "Respawn", "UNAVAILABLE", false},
    Expected{"dead-jump550.json", 1002, 2, 4, false, MenuSurface::Quick, 3, 0, 1, false, 11, PlayerLifeState::Dead, true, false, false, 1, 1, -1, 0, -1, "Respawn", "UNAVAILABLE", false},
    Expected{"dead-jump799.json", 1002, 2, 4, false, MenuSurface::Quick, 3, 0, 1, false, 11, PlayerLifeState::Dead, true, false, false, 1, 1, -1, 0, -1, "Respawn", "UNAVAILABLE", false},
    Expected{"dead-jump800.json", 1002, 2, 4, false, MenuSurface::Quick, 3, 0, 1, false, 11, PlayerLifeState::Dead, true, false, false, 0, 1, -1, 0, -1, "Respawn", "UNAVAILABLE", false},
    Expected{"dead-reopen-pending800.json", 1003, 2, 4, false, MenuSurface::Quick, 3, 0, 1, false, 11, PlayerLifeState::Dead, true, false, true, 0, 0, -1, 0, -1, "Respawn", "UNAVAILABLE", false},
    Expected{"dead-reopened800.json", 1003, 3, 5, true, MenuSurface::Quick, 3, 0, 1, false, 11, PlayerLifeState::Dead, true, true, false, 0, 0, 1, 2, 0, "Respawn", "UNAVAILABLE", false},
    Expected{"permission-before0.json", 0, 0, 1, false, MenuSurface::Main, 8, 0, 1, false, 11, PlayerLifeState::Alive, false, false, false, 0, 0, -1, 0, -1, "Connect after spawning", "CONNECTED", false},
    Expected{"permission-denied0.json", 1001, 0, 1, false, MenuSurface::Main, 8, 0, 1, false, 11, PlayerLifeState::Alive, false, false, false, 0, 0, -1, 0, -1, "Connect after spawning", "CONNECTED", false},
    Expected{"permission-recovered0.json", 1001, 0, 1, false, MenuSurface::Main, 8, 0, 1, false, 12, PlayerLifeState::Alive, true, false, false, 0, 0, -1, 0, -1, "Connect after spawning", "CONNECTED", false},
    Expected{"permission-pending0.json", 1002, 0, 1, false, MenuSurface::Main, 8, 0, 1, false, 12, PlayerLifeState::Alive, true, false, true, 0, 0, -1, 0, -1, "Connect after spawning", "CONNECTED", false},
    Expected{"permission-drawn0.json", 1002, 1, 3, true, MenuSurface::Quick, 14, 0, 1, false, 12, PlayerLifeState::Alive, true, true, false, 0, 0, 1, 1, 0, "God Mode", "OFF", false},
    Expected{"disabled-before0.json", 1022, 25, 12, false, MenuSurface::Main, 8, 4, 2, true, 10, PlayerLifeState::Alive, true, false, false, 0, 3, -1, 0, -1, "Menu Customization", ">", true},
    Expected{"disabled-denied0.json", 1023, 25, 12, false, MenuSurface::Main, 8, 4, 2, true, 10, PlayerLifeState::Alive, true, false, false, 0, 3, -1, 0, -1, "Menu Customization", ">", true},
    Expected{"disabled-main-pending0.json", 1024, 25, 12, false, MenuSurface::Main, 8, 4, 2, true, 10, PlayerLifeState::Alive, true, false, true, 0, 0, -1, 0, -1, "Menu Customization", ">", true},
    Expected{"disabled-main-rebuild0.json", 1024, 26, 13, true, MenuSurface::Main, 8, 4, 2, true, 10, PlayerLifeState::Alive, true, true, false, 0, 0, 0, 0, -1, "Menu Customization", ">", true},
    Expected{"disabled-main50.json", 1024, 26, 13, true, MenuSurface::Main, 8, 4, 2, true, 10, PlayerLifeState::Alive, true, true, false, 0, 0, 1, 2, 0, "Menu Customization", ">", true},
    Expected{"unknown-before0.json", 0, 0, 1, false, MenuSurface::Main, 8, 0, 1, false, 11, PlayerLifeState::Unknown, true, false, false, 0, 0, -1, 0, -1, "Connect after spawning", "CONNECTED", false},
    Expected{"unknown-denied0.json", 1001, 0, 1, false, MenuSurface::Main, 8, 0, 1, false, 11, PlayerLifeState::Unknown, true, false, false, 0, 0, -1, 0, -1, "Connect after spawning", "CONNECTED", false},
    Expected{"missing-before0.json", 0, 0, 1, false, MenuSurface::Main, 8, 0, 1, false, 0, PlayerLifeState::Unknown, false, false, false, 0, 0, -1, 0, -1, "Connect after spawning", "CONNECTED", false},
    Expected{"missing-denied0.json", 1001, 0, 1, false, MenuSurface::Main, 8, 0, 1, false, 0, PlayerLifeState::Unknown, false, false, false, 0, 0, -1, 0, -1, "Connect after spawning", "CONNECTED", false},
    Expected{"stale-navigation-capture0.json", 0, 0, 1, false, MenuSurface::Main, 8, 0, 1, false, 10, PlayerLifeState::Alive, true, false, false, 0, 0, -1, 0, -1, "Connect after spawning", "CONNECTED", false},
    Expected{"stale-navigation-current800.json", 1002, 2, 4, false, MenuSurface::Quick, 14, 0, 1, false, 10, PlayerLifeState::Alive, true, false, false, 0, 1, -1, 0, -1, "God Mode", "OFF", true},
    Expected{"stale-navigation-denied800.json", 1003, 2, 4, false, MenuSurface::Quick, 14, 0, 1, false, 10, PlayerLifeState::Alive, true, false, false, 0, 1, -1, 0, -1, "God Mode", "OFF", true},
    Expected{"stale-policy-capture0.json", 0, 0, 1, false, MenuSurface::Main, 8, 0, 1, false, 10, PlayerLifeState::Alive, true, false, false, 0, 0, -1, 0, -1, "Connect after spawning", "CONNECTED", false},
    Expected{"stale-policy-current1780.json", 1022, 25, 12, false, MenuSurface::Main, 8, 4, 2, true, 10, PlayerLifeState::Alive, true, false, false, 0, 3, -1, 0, -1, "Menu Customization", ">", true},
    Expected{"stale-policy-denied1780.json", 1023, 25, 12, false, MenuSurface::Main, 8, 4, 2, true, 10, PlayerLifeState::Alive, true, false, false, 0, 3, -1, 0, -1, "Menu Customization", ">", true},
    Expected{"stale-admission-capture0.json", 0, 0, 1, false, MenuSurface::Main, 8, 0, 1, false, 10, PlayerLifeState::Alive, true, false, false, 0, 0, -1, 0, -1, "Connect after spawning", "CONNECTED", false},
    Expected{"stale-admission-current0.json", 0, 0, 1, false, MenuSurface::Main, 8, 0, 1, false, 11, PlayerLifeState::Alive, true, false, false, 0, 0, -1, 0, -1, "Connect after spawning", "CONNECTED", false},
    Expected{"stale-admission-denied0.json", 1001, 0, 1, false, MenuSurface::Main, 8, 0, 1, false, 11, PlayerLifeState::Alive, true, false, false, 0, 0, -1, 0, -1, "Connect after spawning", "CONNECTED", false},
    Expected{"queued-focus-before0.json", 0, 0, 1, false, MenuSurface::Main, 8, 0, 1, false, 10, PlayerLifeState::Alive, true, false, false, 0, 0, -1, 0, -1, "Connect after spawning", "CONNECTED", false},
    Expected{"queued-focus-pending0.json", 1001, 0, 1, false, MenuSurface::Main, 8, 0, 1, false, 10, PlayerLifeState::Alive, true, false, true, 0, 0, -1, 0, -1, "Connect after spawning", "CONNECTED", false},
    Expected{"queued-focus-retired0.json", 1001, 0, 1, false, MenuSurface::Main, 8, 0, 1, false, 10, PlayerLifeState::Alive, true, false, false, 0, 0, -1, 0, -1, "Connect after spawning", "CONNECTED", false},
    Expected{"queued-admission-before0.json", 0, 0, 1, false, MenuSurface::Main, 8, 0, 1, false, 10, PlayerLifeState::Alive, true, false, false, 0, 0, -1, 0, -1, "Connect after spawning", "CONNECTED", false},
    Expected{"queued-admission-pending0.json", 1001, 0, 1, false, MenuSurface::Main, 8, 0, 1, false, 10, PlayerLifeState::Alive, true, false, true, 0, 0, -1, 0, -1, "Connect after spawning", "CONNECTED", false},
    Expected{"queued-admission-retired0.json", 1001, 0, 1, false, MenuSurface::Main, 8, 0, 1, false, 11, PlayerLifeState::Alive, true, false, false, 0, 0, -1, 0, -1, "Connect after spawning", "CONNECTED", false},
    Expected{"queued-owner-before0.json", 0, 0, 1, false, MenuSurface::Main, 8, 0, 1, false, 10, PlayerLifeState::Alive, true, false, false, 0, 0, -1, 0, -1, "Connect after spawning", "CONNECTED", false},
    Expected{"queued-owner-pending0.json", 1001, 0, 1, false, MenuSurface::Main, 8, 0, 1, false, 10, PlayerLifeState::Alive, true, false, true, 0, 0, -1, 0, -1, "Connect after spawning", "CONNECTED", false},
    Expected{"queued-owner-retired0.json", 1001, 0, 1, false, MenuSurface::Main, 8, 0, 1, false, 11, PlayerLifeState::Alive, true, false, false, 0, 0, -1, 0, -1, "Connect after spawning", "CONNECTED", false},
};
struct ChromeExpected {
    const char* name;
    std::uint64_t revision;
    bool exists;
};
// Main replaces chrome after its draw wait, while Quick creates it immediately
const std::array chromeExpected{
    ChromeExpected{"main-before0.json", 1, true},
    ChromeExpected{"main-back0.json", 1, false},
    ChromeExpected{"main-back249.json", 1, false},
    ChromeExpected{"main-back250.json", 1, false},
    ChromeExpected{"main-pending250.json", 1, false},
    ChromeExpected{"main-rebuild250.json", 1, false},
    ChromeExpected{"main-rebuild299.json", 1, false},
    ChromeExpected{"main-drawn300.json", 2, true},
    ChromeExpected{"main-wait849.json", 2, true},
    ChromeExpected{"main-ready850.json", 2, true},
    ChromeExpected{"quick-before0.json", 0, false},
    ChromeExpected{"quick-pending0.json", 0, false},
    ChromeExpected{"quick-drawn0.json", 1, true},
    ChromeExpected{"quick-wait549.json", 1, true},
    ChromeExpected{"quick-ready550.json", 1, true},
    ChromeExpected{"dead-before0.json", 0, false},
    ChromeExpected{"dead-pending0.json", 0, false},
    ChromeExpected{"dead-drawn0.json", 1, true},
    ChromeExpected{"dead-wait549.json", 1, true},
    ChromeExpected{"dead-ready550.json", 1, true},
    ChromeExpected{"dead-jump550.json", 1, false},
    ChromeExpected{"dead-jump799.json", 1, false},
    ChromeExpected{"dead-jump800.json", 1, false},
    ChromeExpected{"dead-reopen-pending800.json", 1, false},
    ChromeExpected{"dead-reopened800.json", 2, true}
};
}
void ActualClosedInventoryMatchesLiteralPermissionsTimingAndBothReaders(const std::filesystem::path& directory) {
    std::set<std::string> actual, names;
    for (const auto& entry : std::filesystem::directory_iterator(directory)) {
        Check(entry.is_regular_file() && entry.path().extension() == ".json", "Opening graph contains an unexpected entry");
        actual.insert(entry.path().filename().string());
    }
    const auto labels = runtime::DescribeOpeningActions(useGraph::Bindings());
    for (const auto& item : expected) {
        names.insert(item.name);
        const auto state = Read(directory / item.name);
        const auto& frame = *state.snapshot;
        const auto preview = ReadBackendPreview(directory / item.name, &labels);
        Check(state.lastSequence == item.ack && frame.navigationRevision == item.navigation
            && frame.menuRevision == item.menu && frame.isOpen == item.open && frame.isRootMenu
            && frame.menuSurface == item.surface && frame.rows.size() == item.rows && frame.selectedIndex == item.index,
            "Actual opening changed its epochs, family, root or source rows");
        Check(frame.openingPolicies && frame.mainOpeningControls && frame.openingPolicies->revision == item.policy
            && frame.openingPolicies->quickMenuDisabled == item.quickDisabled
            && *frame.mainOpeningControls == frame.openingPolicies->main
            && frame.mainOpeningControls->actions == std::vector{OpeningAction::Aim, OpeningAction::Melee}
            && frame.openingPolicies->quick.actions == std::vector{OpeningAction::Aim, OpeningAction::SecondaryOffhand},
            "Actual opening changed its independent saved tuples or disable preference");
        Check(frame.localMenuEligibility.has_value() == (item.admission != 0), "Missing opening facts became an invented actor");
        if (item.admission) Check(*frame.localMenuEligibility == LocalMenuEligibility{item.admission, item.life,
            item.hasMenu, item.controlsDisabled, item.blocked}, "Actual opening permission or pending state changed");
        const auto& row = frame.rows[item.index];
        Check(row.label == item.label && row.value == item.value && row.enabled == item.enabled,
            "Opening changed confirmed values or exposed an unavailable callback");
        Check(frame.menuMonitorWait.has_value() == (item.wait != 0)
            && frame.menuMonitorContinuation.has_value() == (item.receipt != 0), "Closed opening borrowed a mechanical receipt");
        if (item.wait) Check(*frame.menuMonitorWait == MenuMonitorWait{item.wait, MenuMonitorWaitKind::Back}, "Actual Back wait changed");
        if (item.receipt) Check(frame.menuMonitorContinuation->revision == item.receipt
            && frame.menuMonitorContinuation->kind == MenuMonitorWaitKind::Back
            && frame.menuMonitorContinuation->admissionRevision == item.admission
            && frame.menuMonitorContinuation->sequence <= item.ack, "Actual closed result lost original causal scope");
        Check(frame.menuOpenTransition.has_value() == (item.transition >= 0), "Opening gained or lost a caller phase");
        if (item.transition >= 0) Check(*frame.menuOpenTransition == (item.transition == 0
            ? MenuOpenTransitionPhase::Rebuild : MenuOpenTransitionPhase::Cooldown), "Main and Quick creation timing changed");
        Check(frame.menuOpening.has_value() == (item.fade != 0), "Opening fade was inferred from readiness");
        if (item.fade) Check(*frame.menuOpening == MenuOpening{item.fade, item.fadeAge}, "Actual fade identity or observed age changed");
        // No continuation descriptor grants independent closed opening permission
        Check(!frame.menuUseContinuation && !frame.menuOptionCallback, "Opening overlapped a Use callback");
        Check(frame.localMenuEligibility == preview.localMenuEligibility && frame.openingPolicies == preview.openingPolicies
            && frame.mainOpeningControls == preview.mainOpeningControls && frame.menuMonitorWait == preview.menuMonitorWait
            && frame.menuMonitorContinuation == preview.menuMonitorContinuation && frame.menuOpenTransition == preview.menuOpenTransition
            && frame.menuOpening == preview.menuOpening && frame.navigationRevision == preview.navigationRevision
            && frame.menuRevision == preview.menuRevision && frame.isOpen == preview.isOpen && frame.isRootMenu == preview.isRootMenu
            && frame.menuSurface == preview.menuSurface && frame.selectedIndex == preview.selectedIndex
            && frame.rows.size() == preview.rows.size(), "Opening readers disagree about ownership or timing");
        for (std::size_t i = 0; i < frame.rows.size(); ++i) Check(frame.rows[i].label == preview.rows[i].label
            && frame.rows[i].value == preview.rows[i].value && frame.rows[i].enabled == preview.rows[i].enabled
            && frame.rows[i].isSpacer == preview.rows[i].isSpacer && frame.rows[i].openingAction == preview.rows[i].openingAction,
            "Opening readers changed actual row facts");
        const auto chrome = std::find_if(chromeExpected.begin(), chromeExpected.end(),
            [&](const auto& entry) { return std::string(entry.name) == item.name; });
        if (chrome != chromeExpected.end()) {
        const auto revision = chrome->revision ? std::optional<std::uint64_t>(chrome->revision) : std::nullopt;
        const auto creation = chrome->exists ? revision : std::nullopt;
        // Destroyed chrome retains its serial but never its former object ownership
        Check(frame.menuChromeRevision == revision && frame.chromeTheme
            && frame.chromeTheme->exists == chrome->exists
            && frame.chromeTheme->creationRevision == creation
            && frame.chromeTheme->currentRoleMask == (chrome->exists ? 31U : 0U),
            "Actual chrome creation or destruction disagrees with the source draw phase");
        Check(preview.menuChromeRevision == revision && preview.chromeTheme
            && preview.chromeTheme->exists == chrome->exists
            && preview.chromeTheme->creationRevision == creation
            && preview.chromeTheme->currentRoleMask == (chrome->exists ? 31U : 0U),
            "Preview changed actual chrome creation or destruction");
        }
    }
    Check(names == actual && names.size() == 57, "Actual57 opening inventory changed silently");

}
}
