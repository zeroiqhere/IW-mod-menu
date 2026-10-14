#include "../TestSupport.h"
#include "ScopedOpeningExpected.h"
#include "OpeningPolicyScope.h"
#include <fstream>
#include <map>
#include <set>

namespace iw::overlay::tests {
void BackendLocalEligibilityPreservesActualScopedFamiliesEpochsWaitsAndInertRows(const std::filesystem::path& directory) {
    if (!std::filesystem::exists(directory / "opening-closed.json")) return;
    const runtime::Bindings bindings{{2}, {86, 6}, {2, 38}, {1, 40}, {70}, {51}, {52}, {1},
        {38}, {40}, {71}, {69}, {16}, {17}, {32}};
    std::map<std::string, runtime::ParsedState> states;
    for (const auto& expected : opening::Profiles) {
        const auto path = directory / expected.file;
        if (!expected.hasActorEligibility && !std::filesystem::exists(path)) continue;
        Check(std::filesystem::is_regular_file(path) && std::filesystem::file_size(path) <= runtime::MaximumFileBytes,
            "An actual scoped opening packet is missing or oversized");
        std::ifstream file(path, std::ios::binary);
        const std::string bytes{std::istreambuf_iterator<char>(file), std::istreambuf_iterator<char>()};
        const auto parsed = runtime::ParseState(bytes, 42);
        const auto& snapshot = *parsed.snapshot;
        const auto eligibility = expected.hasActorEligibility
            ? std::optional<LocalMenuEligibility>{LocalMenuEligibility{expected.admission, expected.life, true, expected.controlsDisabled, expected.blocked}}
            : std::nullopt;
        Check(snapshot.menuSurface == expected.surface && snapshot.localMenuEligibility == eligibility
            && snapshot.isOpen == expected.open && snapshot.isRootMenu && snapshot.selectedIndex == expected.selected
            && snapshot.rows.size() == expected.rows && snapshot.navigationRevision == expected.navigation
            && snapshot.menuRevision == expected.menu && parsed.lastSequence == expected.sequence,
            "Actual family, admission, selected target or independent epochs changed");
        Check(snapshot.menuOpenTransition == expected.transition && snapshot.menuOpening == expected.opening,
            "Actual initial Main or immediate Quick opening wait changed");
        Check(runtime::InputModeForSnapshot(snapshot) == (expected.transition ? runtime::InputMode::MenuLocked : runtime::InputMode::Menu),
            "Actual source pause did not join native routing");
        Check(snapshot.openingPolicies && snapshot.openingPolicies->revision == 1
            && snapshot.openingPolicies->main == MainOpeningControls{1, {OpeningAction::Aim, OpeningAction::Melee}}
            && snapshot.openingPolicies->quick == MainOpeningControls{1, {OpeningAction::Aim, OpeningAction::SecondaryOffhand}}
            && !snapshot.openingPolicies->quickMenuDisabled && snapshot.mainOpeningControls == snapshot.openingPolicies->main,
            "Opening changed either configured policy owner");
        Check(snapshot.context == (expected.blocked ? "PREVIEW · WORKING" : "PREVIEW")
            && bytes.find("1234567890ABCDEF") == std::string::npos,
            "An actual pending context or private identity boundary changed");
        for (const auto& row : snapshot.rows) Check(!row.enabled || row.value != "ON", "Offline opening advertised an active game feature");
        // Serialization captures observed values and does not claim that an open packet authorizes another opening
        const auto input = expected.surface == MenuSurface::Main ? runtime::Input::OpenMain : runtime::Input::OpenQuick;
        if (snapshot.localMenuEligibility) {
            const auto command = Json::parse(runtime::CommandJson(42, expected.sequence + 1, input,
                snapshot.navigationRevision, runtime::ObservedOpeningControlsRevision(snapshot), snapshot.localMenuEligibility->revision));
            Check(command.at("navigationRevision") == expected.navigation && command.at("openingControlsRevision") == 1
                && command.at("localAdmissionRevision") == expected.admission, "Actual command lost its captured scope");
        } else {
            // Cleanup navigation carries no actor scope that could authorize another opening
            for (const auto scoped : {runtime::Input::OpenMain, runtime::Input::OpenQuick, runtime::Input::JumpBack}) {
                Reject([&] { runtime::CommandJson(42, expected.sequence + 1, scoped,
                    snapshot.navigationRevision, runtime::ObservedOpeningControlsRevision(snapshot)); });
            }
        }
        states.emplace(expected.file, parsed);
    }
    // A fresh physical completion acts on copied real packets, never desktop keys
    runtime::NativeInputRouter router(bindings);
    std::set<int> keys;
    const auto poll = [&](const char* name, std::uint64_t now) {
        const auto& state = *states.at(name).snapshot;
        return router.Poll(true, state.isOpen, false, [&](int key) { return keys.contains(key); }, now,
            state.navigationRevision, state.appearance.quickExit, state.isRootMenu, runtime::InputModeForSnapshot(state),
            state.appearance.scrollAnimationMilliseconds, state.menuRevision, &*state.mainOpeningControls, 0, 0,
            &*state.openingPolicies, state.localMenuEligibility ? &*state.localMenuEligibility : nullptr, state.menuSurface);
    };
    Check(!poll("opening-closed.json", 0), "Initial real packet replayed a chord");
    keys = {2, 69};
    Check(poll("opening-closed.json", 1) == runtime::Input::OpenQuick, "Actual living packet lost the exact configured Quick chord");
    Check(!poll("opening-pending.json", 2), "Pending real proof replayed an opening");
    keys = {70}; Check(!poll("opening-quick-wait.json", 3), "Quick wait dispatched ordinary Select");
    Check(!poll("opening-quick-ready.json", 550) && !poll("opening-quick-ready.json", 551),
        "A press inside the actual Quick pause replayed at readiness");
    keys.clear(); poll("opening-quick-ready.json", 552); keys = {70};
    Check(poll("opening-quick-ready.json", 553) == runtime::Input::Select, "Actual Quick readiness lost a later fresh selection");
    keys.clear(); Check(!poll("opening-quick-dead.json", 554), "Dead observation replayed a living key");
    keys = {32}; Check(poll("opening-quick-dead.json", 555) == runtime::Input::JumpBack, "Actual dead Quick surface lost its scoped jump-back");
    if (states.contains("opening-cleanup-retry.json")) {
        const auto& cleanup = *states.at("opening-cleanup-retry.json").snapshot;
        Check(!cleanup.localMenuEligibility && !cleanup.menuOpening && !cleanup.menuOpenTransition
            && cleanup.rows.front().value == "UNAVAILABLE" && !cleanup.rows.front().enabled
            && cleanup.rows.back().label == "Exit" && cleanup.rows.back().enabled,
            "Actorless cleanup lost its inert connection boundary or available Exit");
        // Count ordinary navigation intents against the real root order, not a fabricated actor packet
        keys.clear(); Check(!poll("opening-cleanup-retry.json", 600), "Cleanup inherited a dead-player key");
        std::size_t cursor = cleanup.selectedIndex;
        std::uint64_t now = 800;
        while (cursor + 1 < cleanup.rows.size()) {
            keys = {40};
            Check(poll("opening-cleanup-retry.json", now) == runtime::Input::Down,
                "Actorless cleanup blocked fresh ordinary navigation toward Exit");
            ++cursor;
            keys.clear(); Check(!poll("opening-cleanup-retry.json", now + 1), "Cleanup release replayed navigation");
            now += 200;
        }
        Check(cleanup.rows[cursor].label == "Exit", "Cleanup navigation did not reach the real Exit row");
        keys = {70};
        Check(poll("opening-cleanup-retry.json", now) == runtime::Input::Select,
            "Actorless cleanup blocked a fresh Exit selection");
        // Administrative selection stays an ordinary command without manufactured local authorization
        const auto command = Json::parse(runtime::CommandJson(42, states.at("opening-cleanup-retry.json").lastSequence + 1,
            runtime::Input::Select, cleanup.navigationRevision, runtime::ObservedOpeningControlsRevision(cleanup)));
        Check(command.at("input") == "select" && !command.contains("localAdmissionRevision"),
            "Cleanup selection acquired a fabricated local admission epoch");
    }

}
}
