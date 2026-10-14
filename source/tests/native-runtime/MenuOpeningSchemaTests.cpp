#include "TestSupport.h"
#include <limits>
#include <set>

namespace iw::overlay::tests {
namespace {
// Two rows form the shortest active source fade
Json OpeningPacket(std::size_t count = 2) {
    auto packet = StateJson(); packet["isOpen"] = true; packet["isRootMenu"] = false;
    packet["rows"] = Json::array();
    for (std::size_t row = 0; row < count; ++row) packet["rows"].push_back({{"label", "Row"}, {"value", "OFF"}, {"enabled", true}});
    packet["menuOpening"] = {{"revision", 1}, {"elapsedMilliseconds", 0}};
    return packet;
}
}
void MenuOpeningRejectsMalformedIdentitiesElapsedValuesAndDecodedDuplicates() {
    for (const Json& invalid : {Json(false), Json(1), Json("opening"), Json::array(), Json::object(),
        Json{{"revision", 1}}, Json{{"elapsedMilliseconds", 0}},
        Json{{"revision", 0}, {"elapsedMilliseconds", 0}}, Json{{"revision", -1}, {"elapsedMilliseconds", 0}},
        Json{{"revision", nullptr}, {"elapsedMilliseconds", 0}}, Json{{"revision", true}, {"elapsedMilliseconds", 0}},
        Json{{"revision", "1"}, {"elapsedMilliseconds", 0}}, Json{{"revision", 1.0}, {"elapsedMilliseconds", 0}},
        Json{{"revision", 1}, {"elapsedMilliseconds", nullptr}}, Json{{"revision", 1}, {"elapsedMilliseconds", false}},
        Json{{"revision", 1}, {"elapsedMilliseconds", "0"}}, Json{{"revision", 1}, {"elapsedMilliseconds", 0.0}},
        Json{{"revision", 1}, {"elapsedMilliseconds", -1}}, Json{{"revision", 1}, {"elapsedMilliseconds", 100}},
        Json{{"revision", 1}, {"elapsedMilliseconds", std::numeric_limits<std::uint64_t>::max()}},
        Json{{"revision", 1}, {"elapsedMilliseconds", 0}, {"duration", 100}}}) {
        auto packet = OpeningPacket(); packet["menuOpening"] = invalid;
        Reject([&] { runtime::ParseState(packet.dump(), GetCurrentProcessId()); });
    }
    // Duplicate decoded names cannot change either validated event field
    for (const auto* field : {"\"revision\":1", "\"elapsedMilliseconds\":0"}) {
        for (bool escaped : {false, true}) {
            auto bytes = OpeningPacket().dump(); const std::string original(field);
            const auto start = bytes.find(original); Check(start != std::string::npos, "Opening duplicate fixture lost its field");
            const std::string duplicate = original == "\"revision\":1"
                ? escaped ? "\"\\u0072evision\":2" : "\"revision\":2"
                : escaped ? "\"\\u0065lapsedMilliseconds\":1" : "\"elapsedMilliseconds\":1";
            bytes.replace(start, original.size(), original + "," + duplicate);
            Reject([&] { runtime::ParseState(bytes, GetCurrentProcessId()); });
        }
    }
}

void MenuOpeningPreservesLegacyDefaultsAndOwnsPositiveUnsignedIdentities() {
    auto packet = OpeningPacket(); packet.erase("menuOpening");
    Check(!runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot->menuOpening,
        "A legacy packet invented an opening event");
    packet["menuOpening"] = nullptr;
    Check(!runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot->menuOpening,
        "Null opening metadata changed a legacy packet");
    // Event identity stays unsigned and private focus remains supplied by the native caller
    for (auto revision : {std::uint64_t{1}, std::numeric_limits<std::uint64_t>::max()}) {
        for (int elapsed : {0, 99}) {
            packet["menuOpening"] = {{"revision", revision}, {"elapsedMilliseconds", elapsed}};
            const auto owned = runtime::ParseState(packet.dump(), GetCurrentProcessId(), nullptr, nullptr, nullptr,
                nullptr, nullptr, std::uint64_t{17}).snapshot;
            packet["menuOpening"]["elapsedMilliseconds"] = 1;
            Check(owned->menuOpening == MenuOpening{revision, elapsed} && owned->presentationFocusRevision == 17
                && !owned->menuRevision && owned->navigationRevision == 0 && owned->rows.size() == 2,
                "Opening retained caller storage or invented a public page or navigation epoch");
        }
    }
}
void MenuOpeningRejectsShortRowsInvalidSelectionsAndEveryForbiddenModal() {
    // Ordinary row fades cannot overlap views that own a separate row or modal lifetime
    for (int conflict = 0; conflict < 15; ++conflict) {
        auto packet = OpeningPacket();
        if (conflict == 0) packet["isOpen"] = false;
        if (conflict == 1) packet["rows"] = Json::array();
        if (conflict == 2) packet["rows"] = StateJson().at("rows");
        if (conflict == 3) packet["selectedIndex"] = 2;
        if (conflict == 4) packet["widthEditor"] = {{"step", 1}};
        if (conflict == 5) packet["instructionPositionEditor"] = {{"phase", "editing"}};
        if (conflict == 6) packet["menuPositionEditor"] = {{"step", 1}};
        if (conflict == 7) packet["menuCredits"] = {{"elapsedMilliseconds", 0}};
        if (conflict == 8) packet["menuUnlock"] = "rebuild";
        if (conflict == 9) packet["menuUnlock"] = "pause";
        if (conflict == 10) packet["menuPreferenceRebuild"] = {{"phase", "rebuild"}};
        if (conflict == 11) packet["menuPreferenceRefresh"] = {{"phase", "cooldown"}};
        if (conflict == 12) packet["menuDesignTransition"] = {{"phase", "rebuild"}};
        if (conflict == 13) packet["selectedIndex"] = 0.0;
        if (conflict == 14) packet["rows"] = Json::object();
        Reject([&] { runtime::ParseState(packet.dump(), GetCurrentProcessId()); });
    }
}
void MenuOpeningAllowsOrdinaryRootChildAndDesignCooldownWithoutOwningItsLock() {
    auto packet = OpeningPacket();
    for (bool root : {false, true}) {
        packet["isRootMenu"] = root;
        const auto state = runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot;
        Check(state->menuOpening.has_value() && runtime::InputModeForSnapshot(*state) == runtime::InputMode::Menu,
            "Opening alone paused an ordinary root or child menu");
    }
    packet["isRootMenu"] = false; packet["menuDesignTransition"] = {{"phase", "cooldown"}};
    Check(runtime::InputModeForSnapshot(*runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot) == runtime::InputMode::MenuLocked,
        "Opening overrode the independent design cooldown lock");
    // A short fade can finish before the independent Use cooldown ends
    packet["menuOpening"] = nullptr;
    Check(runtime::InputModeForSnapshot(*runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot) == runtime::InputMode::MenuLocked,
        "Opening expiration prematurely unlocked the design cooldown");
}
void MenuOpeningRoutesEveryOrdinaryFreshControlWithoutNewKeysOrCommands() {
    auto bindings = BindingsJson(); bindings["previousChoice"] = {51}; bindings["nextChoice"] = {52};
    // Existing saved groups keep their ordinary command mapping while rows fade
    const int keys[] = {1, 2, 70, 51, 52, 86};
    const runtime::Input commands[] = {runtime::Input::Down, runtime::Input::Up, runtime::Input::Select,
        runtime::Input::PreviousChoice, runtime::Input::NextChoice, runtime::Input::Back};
    const auto state = runtime::ParseState(OpeningPacket().dump(), GetCurrentProcessId()).snapshot;
    for (std::size_t index = 0; index < std::size(keys); ++index) {
        runtime::NativeInputRouter router(runtime::ParseBindings(bindings.dump())); std::set<int> held;
        const auto poll = [&] {
            return router.Poll(true, true, false, [&](int key) { return held.contains(key); }, 0,
                state->navigationRevision, false, false, runtime::InputModeForSnapshot(*state),
                state->appearance.scrollAnimationMilliseconds, state->menuRevision);
        };
        poll(); held = {keys[index]};
        Check(poll() == commands[index], "An active opening changed ordinary input routing");
    }
}
void MenuOpeningExpirationLeavesEpochsAndAnEstablishedScrollRepeatUnchanged() {
    runtime::NativeInputRouter router(runtime::ParseBindings(BindingsJson().dump()));
    auto packet = OpeningPacket(); packet["navigationRevision"] = 7; packet["menuRevision"] = 6;
    std::set<int> held;
    const auto poll = [&](std::uint64_t now) {
        const auto state = runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot;
        Check(state->navigationRevision == 7 && state->menuRevision == 6, "Opening expiration invented an epoch");
        return router.Poll(true, true, false, [&](int key) { return held.contains(key); }, now,
            state->navigationRevision, false, false, runtime::InputModeForSnapshot(*state),
            state->appearance.scrollAnimationMilliseconds, state->menuRevision);
    };
    poll(0); held = {1}; Check(poll(0) == runtime::Input::Down, "Opening blocked the initial ordinary scroll");
    // Removing presentation metadata does not restart the captured 195 ms repeat
    packet["menuOpening"] = nullptr; Check(!poll(100) && !poll(194), "Opening expiration shortened scroll timing");
    Check(poll(195) == runtime::Input::Down, "Opening disappearance reset an established scroll repeat");
}
void MenuOpeningContinuesAfterDesignUnlockWhileHeldKeysRequireFreshRelease() {
    runtime::NativeInputRouter router(runtime::ParseBindings(BindingsJson().dump()));
    auto packet = OpeningPacket(10); packet["navigationRevision"] = 7; packet["menuRevision"] = 6;
    packet["menuDesignTransition"] = {{"phase", "cooldown"}};
    std::set<int> held;
    const auto poll = [&](std::uint64_t now) {
        const auto state = runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot;
        return router.Poll(true, true, false, [&](int key) { return held.contains(key); }, now,
            state->navigationRevision, false, false, runtime::InputModeForSnapshot(*state),
            state->appearance.scrollAnimationMilliseconds, state->menuRevision);
    };
    poll(50); held = {70}; packet["menuOpening"]["elapsedMilliseconds"] = 199;
    Check(!poll(249), "Opening bypassed the still-active design cooldown");
    // The lock ends independently while the same fade event continues
    packet["menuDesignTransition"] = nullptr; packet["menuOpening"]["elapsedMilliseconds"] = 200;
    Check(!poll(250), "Design unlock carried an old selection into an ongoing opening");
    held.clear(); poll(251); held = {70};
    Check(poll(251) == runtime::Input::Select, "Opening kept controls locked after fresh release at design unlock");
}
}
