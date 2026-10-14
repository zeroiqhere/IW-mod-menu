#include "CallbackFixtureExpected.h"
#include "CallbackFixtureTests.h"
#include "../../TestSupport.h"
#include "../../../native-layout/BackendPreview.h"
#include "../../../../native/runtime/OpeningPolicyScope.h"
#include <fstream>
#include <map>
#include <set>

namespace iw::overlay::tests::callback {
namespace {
std::string ReadFixture(const std::filesystem::path& path) {
    // A bounded owned copy keeps malformed or replaced files out of the parser
    Check(std::filesystem::symlink_status(path).type() == std::filesystem::file_type::regular,
        "A callback fixture is not a regular file");
    const auto size = std::filesystem::file_size(path);
    Check(size > 0 && size <= runtime::MaximumFileBytes, "A callback fixture is empty or oversized");
    std::ifstream stream(path, std::ios::binary);
    Check(stream.good(), "A callback fixture could not be opened");
    std::string bytes(static_cast<std::size_t>(size), '\0');
    stream.read(bytes.data(), static_cast<std::streamsize>(size));
    Check(stream.gcount() == static_cast<std::streamsize>(size) && stream.peek() == std::char_traits<char>::eof(),
        "A callback fixture changed while being read");
    return bytes;
}
}

void ActualCallbacksKeepOwnedWaitsSeparateFromBackendWork(const std::filesystem::path& directory) {
    std::set<std::string> names;
    for (const auto& expected : Profiles) names.emplace(expected.file);
    std::set<std::string> actual;
    Check(std::filesystem::is_directory(directory), "The callback fixture directory is missing");
    for (const auto& entry : std::filesystem::directory_iterator(directory)) {
        Check(entry.symlink_status().type() == std::filesystem::file_type::regular,
            "The callback inventory contains a non-regular entry");
        Check(actual.emplace(entry.path().filename().string()).second, "The callback inventory repeats a file");
        Check(actual.size() <= Profiles.size(), "The callback inventory exceeds its expected bound");
    }
    Check(actual == names, "The callback inventory differs from its controller contract");
    std::map<std::string, runtime::ParsedState> states;
    for (const auto& expected : Profiles) {
        const auto path = directory / expected.file;
        const auto bytes = ReadFixture(path);
        const auto parsed = runtime::ParseState(bytes, 42);
        const auto& state = *parsed.snapshot;
        const auto preview = ReadBackendPreview(path);
        const auto expectedSurface = expected.quick ? MenuSurface::Quick : MenuSurface::Main;
        const auto expectedOpening = expected.openingElapsed
            ? std::optional<MenuOpening>{MenuOpening{1, *expected.openingElapsed}} : std::nullopt;
        const auto eligibility = LocalMenuEligibility{expected.admission, PlayerLifeState::Alive, true,
            expected.controlsDisabled, expected.blocked};
        // Eleven source rows precede the named IW extension and its eleven navigation steps
        const std::size_t selectedRow = expected.quick ? 0 : 11;
        // Exact epochs prove one boolean redraw and no fabricated plain redraw
        Check(state.isOpen == expected.open && state.isRootMenu == expected.root && state.selectedIndex == selectedRow
            && state.menuSurface == expectedSurface && state.navigationRevision == expected.navigation
            && state.menuRevision == expected.menu && parsed.lastSequence == expected.sequence,
            "A callback packet lost its actual family, selection or independent epochs");
        Check(state.menuOptionCallback == expected.callback && state.menuOpening == expectedOpening
            && state.localMenuEligibility == eligibility, "A callback packet changed its actual wait or admission facts");
        Check(!state.menuOpenTransition && !state.menuPreferenceRefresh && !state.menuPreferenceRebuild
            && !state.menuDesignTransition && !state.menuUnlock && !state.widthEditor
            && !state.menuPositionEditor && !state.instructionPositionEditor && !state.menuCredits,
            "A callback packet acquired another modal owner");
        Check(state.rows.size() == (expected.quick ? 14 : 12)
            && state.rows[selectedRow].label == (expected.quick ? "God Mode" : "Infinite Ammo (IW lobby)")
            && state.rows[selectedRow].value == (expected.busy ? "UNKNOWN" : "OFF")
            && state.rows[selectedRow].enabled == expected.enabled,
            "A callback wait fabricated a current feature value or capability");
        if (!expected.quick) {
            // Unimplemented source callbacks remain visibly separate from the working IW control
            constexpr std::array<const char*, 11> labels{{"Verification", "Teleport", "Profile Management",
                "Model Manipulation", "Malicious Options", "God Mode", "Send Message", "Kick", "Down", "Revive", "Respawn"}};
            for (std::size_t index = 0; index < labels.size(); ++index) {
                Check(state.rows[index].label == labels[index] && state.rows[index].value == "UNAVAILABLE"
                    && !state.rows[index].enabled, "A source row borrowed the IW extension's value or permission");
            }
        }
        Check(state.context == (expected.busy ? "CONNECTED · WORKING" : "CONNECTED")
            && bytes.find("1234567890ABCDEF") == std::string::npos,
            "A callback packet lost connected provenance or exposed private identity");
        Check(state.openingPolicies && state.openingPolicies->revision == 1
            && state.openingPolicies->main == MainOpeningControls{1, {OpeningAction::Aim, OpeningAction::Melee}}
            && state.openingPolicies->quick == MainOpeningControls{1, {OpeningAction::Aim, OpeningAction::SecondaryOffhand}}
            && !state.openingPolicies->quickMenuDisabled && state.mainOpeningControls == state.openingPolicies->main,
            "A callback wait changed either opening policy owner");
        // Both production readers must retain the same immutable presentation fields
        Check(preview.menuOptionCallback == state.menuOptionCallback && preview.menuOpening == state.menuOpening
            && preview.localMenuEligibility == state.localMenuEligibility && preview.menuSurface == state.menuSurface
            && preview.navigationRevision == state.navigationRevision && preview.menuRevision == state.menuRevision
            && preview.rows.size() == state.rows.size() && preview.rows.front().value == state.rows.front().value
            && preview.rows.front().enabled == state.rows.front().enabled && preview.context == state.context,
            "The preview reader disagrees with the runtime callback packet");
        for (std::size_t index = 0; index < state.rows.size(); ++index) {
            // Both readers preserve every disabled source row and the selected extension
            Check(preview.rows[index].label == state.rows[index].label
                && preview.rows[index].value == state.rows[index].value
                && preview.rows[index].enabled == state.rows[index].enabled,
                "The preview reader changed a callback inventory row");
        }
        const auto wire = Json::parse(bytes);
        Check(wire.contains("menuOptionCallback") == expected.callback.has_value(),
            "An inactive callback was serialized as a supplied null owner");
        Check(runtime::InputModeForSnapshot(state) == (expected.callback ? runtime::InputMode::MenuLocked : runtime::InputMode::Menu),
            "The parsed callback wait did not own the existing locked input route");
        const auto command = Json::parse(runtime::CommandJson(42, expected.sequence + 1, runtime::Input::Select,
            state.navigationRevision, runtime::ObservedOpeningControlsRevision(state)));
        Check(command.at("navigationRevision") == expected.navigation && command.at("openingControlsRevision") == 1
            && !command.contains("localAdmissionRevision"), "Ordinary callback selection fabricated a scoped actor opening");
        states.emplace(expected.file, parsed);
    }

    const runtime::Bindings bindings{{2}, {86, 6}, {2, 38}, {1, 40}, {70}, {51}, {52}, {1},
        {38}, {40}, {71}, {69}, {16}, {17}, {32}};
    // Each physical edge is consumed through the actual wait, independently of backend Busy
    for (const int heldKey : {1, 2, 38, 40, 70, 86, 6, 51, 52, 69, 32}) {
        runtime::NativeInputRouter router(bindings);
        std::set<int> keys;
        const auto poll = [&](const char* file, std::uint64_t now, bool pending = false, bool foreground = true) {
            const auto& state = *states.at(file).snapshot;
            return router.Poll(foreground, state.isOpen, pending, [&](int key) { return keys.contains(key); }, now,
                state.navigationRevision, state.appearance.quickExit, state.isRootMenu, runtime::InputModeForSnapshot(state),
                state.appearance.scrollAnimationMilliseconds, state.menuRevision, &*state.mainOpeningControls, 0, 0,
                &*state.openingPolicies, &*state.localMenuEligibility, state.menuSurface);
        };
        Check(!poll("boolean-pending-immediate.json", 0), "The callback seed dispatched an input");
        keys = {heldKey};
        Check(!poll("boolean-pending-immediate.json", 1) && !poll("boolean179.json", 179, true)
            && !poll("boolean180.json", 180) && !poll("boolean429.json", 429)
            && !poll("boolean430-ready.json", 430) && !poll("boolean430-ready.json", 1000),
            "An edge pressed during a callback wait replayed after its deadline");
        keys.clear(); Check(!poll("boolean430-ready.json", 1001), "A callback release dispatched input");
        keys = {70}; Check(poll("boolean430-ready.json", 1002) == runtime::Input::Select,
            "Callback readiness rejected a later fresh selection");
        router.Reset(); keys.clear(); Check(!poll("boolean-pending-busy.json", 2000), "Busy callback seed dispatched");
        keys = {70};
        Check(!poll("boolean-pending-busy.json", 2001) && !poll("boolean180-busy.json", 2180)
            && !poll("boolean430-busy.json", 2430), "Callback expiry bypassed ongoing backend work");
        keys.clear(); poll("boolean430-busy.json", 2431); keys = {70};
        Check(!poll("boolean430-busy.json", 2432), "Busy work accepted a fresh selection after callback expiry");
        router.Reset(); keys.clear(); Check(!poll("plain-lobby-cooldown0.json", 3000), "Plain callback seed dispatched");
        keys = {heldKey};
        Check(!poll("plain-lobby-cooldown0.json", 3001) && !poll("plain-lobby-cooldown249.json", 3249)
            && !poll("plain-lobby250-ready.json", 3250) && !poll("plain-lobby250-ready.json", 3500),
            "A plain callback wait replayed a held edge");
        keys.clear(); poll("plain-lobby250-ready.json", 3501); keys = {70};
        Check(poll("plain-lobby250-ready.json", 3502) == runtime::Input::Select,
            "Plain callback readiness rejected a later fresh selection");
        // Hidden, retired and shutdown packets never replay the earlier callback edge
        Check(!poll("focus-hidden-pending.json", 4000, false, false)
            && !poll("replaced-owner-closed.json", 4001) && !poll("shutdown-no-callback.json", 4002),
            "A hidden, retired or shutdown owner replayed input");
    }
}
}
