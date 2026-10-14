#include "BooleanPaintFixtureExpected.h"
#include "BooleanPaintFixtureTests.h"
#include "../../TestSupport.h"
#include "../../../native-layout/BackendPreview.h"
#include "../../../../native/shared/paint/RowDisplayValue.h"
#include <fstream>
#include <map>
#include <set>

namespace iw::overlay::tests::booleanPaint {
namespace {
std::string Read(const std::filesystem::path& path) {
    // Fixture reads are bounded owned copies with no symlink or growing-file fallback
    Check(std::filesystem::symlink_status(path).type() == std::filesystem::file_type::regular,
        "A boolean paint fixture is not a regular file");
    const auto size = std::filesystem::file_size(path);
    Check(size > 0 && size <= runtime::MaximumFileBytes, "A boolean paint fixture is empty or oversized");
    std::ifstream stream(path, std::ios::binary); Check(stream.good(), "A boolean paint fixture could not be opened");
    std::string bytes(static_cast<std::size_t>(size), '\0');
    stream.read(bytes.data(), static_cast<std::streamsize>(size));
    Check(stream.gcount() == static_cast<std::streamsize>(size) && stream.peek() == std::char_traits<char>::eof(),
        "A boolean paint fixture changed during its bounded read");
    return bytes;
}
std::string_view CachedValue(const MenuBooleanPaint& paint, std::size_t index) {
    for (std::size_t entry = 0; entry < paint.count; ++entry)
        if (paint.rows[entry].index == index) return BooleanPaintText(paint.rows[entry].value);
    throw std::runtime_error("The actual boolean paint owner does not cover its God Mode row");
}
}
void ActualBooleanPaintGraphKeepsCurrentResultsSeparateFromCopiedHudValues(const std::filesystem::path& directory) {
    Check(std::filesystem::is_directory(directory), "The real boolean paint graph has not been exported");
    std::set<std::string> expectedNames, actualNames;
    for (const auto& expected : Profiles) expectedNames.emplace(expected.file);
    for (const auto& expected : CohortProfiles) expectedNames.emplace(expected.file);
    for (const auto& entry : std::filesystem::directory_iterator(directory)) {
        Check(entry.symlink_status().type() == std::filesystem::file_type::regular,
            "The boolean paint inventory contains a non-regular entry");
        Check(actualNames.emplace(entry.path().filename().string()).second && actualNames.size() <= Profiles.size() + CohortProfiles.size(),
            "The boolean paint inventory exceeds its fixed bound");
    }
    Check(actualNames == expectedNames, "The boolean paint inventory differs from the actual controller graph");
    std::map<std::string, runtime::ParsedState> states;
    for (const auto& expected : Profiles) {
        const auto path = directory / expected.file; const auto bytes = Read(path);
        const auto parsed = runtime::ParseState(bytes, 42); const auto& state = *parsed.snapshot;
        const auto preview = ReadBackendPreview(path); const auto wire = Json::parse(bytes);
        // Current results and admission flags remain separate from the copied checkbox sample
        Check(state.menuSurface == MenuSurface::Quick && state.isRootMenu && state.rows.size() == 14
            && state.isOpen == expected.open && state.selectedIndex == expected.selected && state.rows[0].label == "God Mode"
            && state.navigationRevision == expected.navigation && state.menuRevision == expected.menu
            && parsed.lastSequence == expected.sequence
            && state.rows[0].value == expected.current && state.rows[0].enabled == expected.enabled
            && ResolveRowDisplayValue(state, 0) == expected.display,
            "The boolean paint graph changed current values, permission or effective display");
        Check(state.menuBooleanPaint.has_value() == (expected.cached != nullptr)
            && wire.contains("menuBooleanPaint") == (expected.cached != nullptr),
            "A hidden or active graph packet lost its omitted-null paint boundary");
        if (state.menuBooleanPaint) {
            Check(state.menuBooleanPaint->revision == expected.paintRevision
                && state.menuBooleanPaint->count == 1 && state.menuBooleanPaint->rows[0].index == 0
                && CachedValue(*state.menuBooleanPaint, 0) == expected.cached,
                "The controller sampled checkbox paint at an incorrect redraw boundary");
            const auto& paint = wire.at("menuBooleanPaint");
            Check(paint.is_object() && paint.size() == 2 && paint.contains("revision") && paint.contains("rows"),
                "The public paint owner exposed fields beyond its typed display contract");
            for (const auto& row : paint.at("rows"))
                Check(row.is_object() && row.size() == 2 && row.contains("index") && row.contains("value"),
                    "A cached row exposed fields beyond its display contract");
        }
        Check(state.localMenuEligibility && state.localMenuEligibility->revision == 10
            && state.localMenuEligibility->life == PlayerLifeState::Alive
            && state.localMenuEligibility->hasMenu == true && state.localMenuEligibility->controlsDisabled == expected.controlsDisabled
            && state.localMenuEligibility->blocked == expected.blocked
            && state.context == (expected.blocked ? "CONNECTED · WORKING" : "CONNECTED"),
            "Cached checkbox paint changed the actual connected admission observation");
        const auto callback = expected.callback ? std::optional<MenuOptionCallback>{
            MenuOptionCallback{1, MenuOptionCallbackKind::Boolean, *expected.callback}} : std::nullopt;
        Check(state.menuOptionCallback == callback
            && state.menuOpenTransition == (expected.openingCooldown
                ? std::optional<MenuOpenTransitionPhase>{MenuOpenTransitionPhase::Cooldown} : std::nullopt),
            "Paint recapture fabricated or dropped an independently owned input wait");
        Check(runtime::InputModeForSnapshot(state) == (expected.callback || expected.openingCooldown
                ? runtime::InputMode::MenuLocked : runtime::InputMode::Menu),
            "Paint became an input lock rather than following the actual lifecycle");
        Check(state.openingPolicies && state.openingPolicies->revision == 1 && state.mainOpeningControls == state.openingPolicies->main
            && state.openingPolicies->main == MainOpeningControls{1, {OpeningAction::Aim, OpeningAction::Melee}}
            && state.openingPolicies->quick == MainOpeningControls{1, {OpeningAction::Aim, OpeningAction::SecondaryOffhand}}
            && !state.openingPolicies->quickMenuDisabled && bytes.find("1234567890ABCDEF") == std::string::npos,
            "Display ownership changed opening policies or exposed a private actor identity");
        // Both real readers must copy exactly the same display owner and independent epochs
        Check(preview.menuBooleanPaint == state.menuBooleanPaint && preview.menuOptionCallback == state.menuOptionCallback
            && preview.localMenuEligibility == state.localMenuEligibility && preview.menuSurface == state.menuSurface
            && preview.menuRevision == state.menuRevision && preview.navigationRevision == state.navigationRevision
            && preview.selectedIndex == state.selectedIndex && preview.rows[0].value == state.rows[0].value
            && preview.rows[0].enabled == state.rows[0].enabled && ResolveRowDisplayValue(preview, 0) == expected.display,
            "The preview reader disagrees with the real runtime paint lifecycle");
        states.emplace(expected.file, parsed);
    }
    const auto& before = *states.at("boolean179-current-on.json").snapshot;
    const auto& redraw = *states.at("boolean180-current-on.json").snapshot;
    Check(before.menuRevision && redraw.menuRevision == *before.menuRevision + 1
        && before.navigationRevision == redraw.navigationRevision
        && states.at("boolean179-current-on.json").lastSequence == states.at("boolean180-current-on.json").lastSequence,
        "The actual boolean redraw changed cursor or command identity instead of only page presentation");
    const auto& busy = *states.at("late180-busy.json").snapshot;
    const auto& complete = *states.at("late430-complete.json").snapshot;
    const auto& scroll = *states.at("late-scroll.json").snapshot;
    Check(busy.menuBooleanPaint == complete.menuBooleanPaint && complete.menuBooleanPaint == scroll.menuBooleanPaint
        && busy.menuRevision == complete.menuRevision && complete.menuRevision == scroll.menuRevision
        && complete.navigationRevision + 1 == scroll.navigationRevision,
        "Work completion or cursor movement resampled the retained checkbox value");
    const auto& reopened = *states.at("late-reopened.json").snapshot;
    Check(reopened.menuRevision != scroll.menuRevision && reopened.menuBooleanPaint->revision == 3,
        "An actual new Quick visit failed to acquire a fresh display owner");
}
void ActualCohortBooleanPaintKeepsAggregateAuthoritySeparateFromDisplaySamples(const std::filesystem::path& directory) {
    const std::array<const char*, 12> labels{{"Verification", "Teleport", "Profile Management",
        "Model Manipulation", "Malicious Options", "God Mode", "Send Message", "Kick", "Down",
        "Revive", "Respawn", "Infinite Ammo (IW lobby)"}};
    for (const auto& expected : CohortProfiles) {
        const auto check = [&](bool condition, const char* message) {
            // Each failure names the unchanged packet that violated its literal oracle
            const std::string context = std::string(expected.file) + ": " + message;
            Check(condition, context.c_str());
        };
        const auto path = directory / expected.file;
        const auto bytes = Read(path);
        const auto parsed = runtime::ParseState(bytes, 42);
        const auto& state = *parsed.snapshot;
        const auto preview = ReadBackendPreview(path);
        const auto wire = Json::parse(bytes);
        // Only source row five gains a callback; the IW ammo extension remains separate
        check(state.isOpen && !state.isRootMenu && state.menuSurface == MenuSurface::Main
            && state.rows.size() == labels.size() && preview.rows.size() == labels.size() && state.selectedIndex == expected.selected
            && state.navigationRevision == expected.navigation && state.menuRevision == expected.menu
            && parsed.lastSequence == expected.sequence && state.rows[5].value == expected.current
            && state.rows[5].enabled == expected.enabled && ResolveRowDisplayValue(state, 5) == expected.display,
            "The actual whole-cohort graph changed its source row or admission epochs");
        check(state.menuBooleanPaint && state.menuBooleanPaint->revision == expected.paintRevision
            && state.menuBooleanPaint->count == 1 && state.menuBooleanPaint->rows[0].index == 5
            && CachedValue(*state.menuBooleanPaint, 5) == expected.cached,
            "Aggregate work or heartbeat resampled the source checkbox before its redraw");
        const auto callback = expected.callback ? std::optional<MenuOptionCallback>{
            MenuOptionCallback{1, MenuOptionCallbackKind::Boolean, *expected.callback}} : std::nullopt;
        check(state.menuOptionCallback == callback && !state.menuOpenTransition
            && state.localMenuEligibility && state.localMenuEligibility->revision == 10
            && state.localMenuEligibility->life == PlayerLifeState::Alive
            && state.localMenuEligibility->hasMenu == true
            && state.localMenuEligibility->controlsDisabled == expected.controlsDisabled
            && state.localMenuEligibility->blocked == expected.blocked
            && state.context == (expected.blocked ? "CONNECTED · WORKING" : "CONNECTED"),
            "The source BOOL wait or Busy state borrowed display paint as action authority");
        for (std::size_t index = 0; index < labels.size(); ++index) {
            check(state.rows[index].label == labels[index] && preview.rows[index].label == state.rows[index].label
                && preview.rows[index].value == state.rows[index].value
                && preview.rows[index].enabled == state.rows[index].enabled
                && wire.at("rows").at(index).size() == 3,
                "A projected row changed source ordering or exposed private cohort fields");
            if (index != 5 && index != 11) Check(!state.rows[index].enabled
                && state.rows[index].value == "UNAVAILABLE", "An unimplemented cohort callback became enabled");
        }
        // Both production readers consume unchanged raw packets and keep the same copied sample
        check(preview.menuBooleanPaint == state.menuBooleanPaint && preview.menuOptionCallback == state.menuOptionCallback
            && preview.localMenuEligibility == state.localMenuEligibility
            && preview.navigationRevision == state.navigationRevision && preview.menuRevision == state.menuRevision
            && preview.selectedIndex == state.selectedIndex && preview.menuSurface == state.menuSurface
            && ResolveRowDisplayValue(preview, 5) == expected.display
            && bytes.find("ExpectedCohort") == std::string::npos
            && bytes.find("AllPlayersGodmodeTarget") == std::string::npos
            && bytes.find("AccountId") == std::string::npos,
            "The native readers changed private authority or aggregate checkbox projection");
    }
}

}
