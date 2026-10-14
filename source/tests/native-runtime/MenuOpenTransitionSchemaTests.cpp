#include "TestSupport.h"
#include <set>

namespace iw::overlay::tests {
namespace {
// A normal open retains ordinary rows and may begin at the root or a saved child
Json OpenPacket(std::size_t count = 1, bool root = true) {
    auto packet = StateJson(); packet["isOpen"] = true; packet["isRootMenu"] = root;
    packet["rows"] = Json::array();
    for (std::size_t row = 0; row < count; ++row) packet["rows"].push_back({{"label", "Row"}, {"value", "OFF"}, {"enabled", true}});
    packet["menuOpenTransition"] = {{"phase", "rebuild"}};
    return packet;
}
}
void MenuOpenTransitionRejectsMalformedPhasesAndDecodedDuplicates() {
    // Missing and null are inactive; every supplied active value must have the exact shape
    for (const Json& invalid : {Json(false), Json(1), Json("rebuild"), Json::array(), Json::object(),
        Json{{"phase", nullptr}}, Json{{"phase", true}}, Json{{"phase", 1}}, Json{{"phase", 1.0}},
        Json{{"phase", "Rebuild"}}, Json{{"phase", "pause"}}, Json{{"phase", ""}},
        Json{{"phase", "rebuild"}, {"elapsedMilliseconds", 0}}}) {
        auto packet = OpenPacket(); packet["menuOpenTransition"] = invalid;
        Reject([&] { runtime::ParseState(packet.dump(), GetCurrentProcessId()); });
    }
    // Decoded duplicate names cannot replace a confirmed phase
    for (const auto* duplicate : {"{\"phase\":\"rebuild\",\"phase\":\"cooldown\"}",
        "{\"phase\":\"rebuild\",\"\\u0070hase\":\"cooldown\"}"}) {
        auto bytes = OpenPacket().dump(); const std::string object = "{\"phase\":\"rebuild\"}";
        const auto start = bytes.find(object); Check(start != std::string::npos, "Open transition duplicate fixture lost its object");
        bytes.replace(start, object.size(), duplicate);
        Reject([&] { runtime::ParseState(bytes, GetCurrentProcessId()); });
    }
    // A duplicate top-level name cannot erase the active phase with an inactive null
    for (const auto* duplicate : {"\"menuOpenTransition\":null", "\"menu\\u004fpenTransition\":null"}) {
        auto bytes = OpenPacket().dump(); const std::string field = "\"menuOpenTransition\":{\"phase\":\"rebuild\"}";
        const auto start = bytes.find(field); Check(start != std::string::npos, "Open duplicate fixture lost its metadata");
        bytes.replace(start, field.size(), field + "," + duplicate);
        Reject([&] { runtime::ParseState(bytes, GetCurrentProcessId()); });
    }
}

void MenuOpenTransitionPreservesLegacyAndOwnsRootChildAndEmptyRows() {
    auto packet = OpenPacket(); packet.erase("menuOpenTransition");
    const auto legacy = runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot;
    Check(!legacy->menuOpenTransition && runtime::InputModeForSnapshot(*legacy) == runtime::InputMode::Menu,
        "Missing normal open metadata changed legacy controls");
    packet["menuOpenTransition"] = nullptr;
    Check(!runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot->menuOpenTransition,
        "Null normal open metadata invented a pause");
    // Zero and one-row retained pages still open even though neither has an active row fade
    for (bool root : {false, true}) for (std::size_t count : {std::size_t{0}, std::size_t{1}, std::size_t{10}}) {
        for (const auto* phase : {"rebuild", "cooldown"}) {
            packet = OpenPacket(count, root); packet["menuOpenTransition"]["phase"] = phase;
            packet["navigationRevision"] = 7; packet["menuRevision"] = 6;
            const auto state = runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot;
            packet["menuOpenTransition"]["phase"] = "changed";
            Check(state->menuOpenTransition == (std::string(phase) == "rebuild" ? MenuOpenTransitionPhase::Rebuild : MenuOpenTransitionPhase::Cooldown)
                && state->rows.size() == count && state->selectedIndex == 0 && state->isRootMenu == root
                && state->navigationRevision == 7 && state->menuRevision == 6 && !state->menuOpening
                && runtime::InputModeForSnapshot(*state) == runtime::InputMode::MenuLocked,
                "Normal open retained caller storage or changed its page, rows or public epochs");
        }
    }
    // Typed callers also remain paused if they construct conflicting views outside JSON
    MenuSnapshot typed; typed.menuOpenTransition = MenuOpenTransitionPhase::Cooldown; typed.menuCredits = MenuCredits{};
    Check(runtime::InputModeForSnapshot(typed) == runtime::InputMode::MenuLocked, "A typed opening conflict escaped the paused route");
}

void MenuOpenTransitionRejectsClosedInvalidSelectionsAndEveryModalConflict() {
    for (const auto* phase : {"rebuild", "cooldown"}) for (int conflict = 0; conflict < 16; ++conflict) {
        auto packet = OpenPacket(2, false); packet["menuOpenTransition"]["phase"] = phase;
        // Every modal owns a separate lifetime, even if it also uses the locked input route
        if (conflict == 0) packet["isOpen"] = false;
        if (conflict == 1) { packet["rows"] = Json::array(); packet["selectedIndex"] = 1; }
        if (conflict == 2) packet["selectedIndex"] = 2;
        if (conflict == 3) packet["selectedIndex"] = -1;
        if (conflict == 4) packet["selectedIndex"] = 0.0;
        if (conflict == 5) packet["rows"] = Json::object();
        if (conflict == 6) packet["widthEditor"] = {{"step", 1}};
        if (conflict == 7) packet["instructionPositionEditor"] = {{"phase", "entering"}};
        if (conflict == 8) packet["instructionPositionEditor"] = {{"phase", "editing"}};
        if (conflict == 9) packet["menuPositionEditor"] = {{"step", 1}};
        if (conflict == 10) packet["menuCredits"] = {{"elapsedMilliseconds", 0}};
        if (conflict == 11) packet["menuUnlock"] = "rebuild";
        if (conflict == 12) packet["menuUnlock"] = "pause";
        if (conflict == 13) packet["menuPreferenceRefresh"] = {{"phase", "pending"}};
        if (conflict == 14) packet["menuDesignTransition"] = {{"phase", "rebuild"}};
        if (conflict == 15) packet["menuDesignTransition"] = {{"phase", "cooldown"}};
        if (conflict >= 6) {
            // Validate the other modal by itself so an unrelated row-shape failure cannot hide this exclusion
            if (conflict <= 10) packet["rows"] = Json::array();
            auto standalone = packet; standalone.erase("menuOpenTransition");
            runtime::ParseState(standalone.dump(), GetCurrentProcessId());
        }
        Reject([&] { runtime::ParseState(packet.dump(), GetCurrentProcessId()); });
    }
    auto packet = OpenPacket(); packet["menuPreferenceRefresh"] = {{"phase", "cooldown"}};
    Reject([&] { runtime::ParseState(packet.dump(), GetCurrentProcessId()); });
    // Existing modal return still rejects root packets rather than becoming normal opening
    packet.erase("menuOpenTransition"); packet["menuUnlock"] = "rebuild"; packet.erase("menuPreferenceRefresh");
    Reject([&] { runtime::ParseState(packet.dump(), GetCurrentProcessId()); });
}

void MenuOpenTransitionAllowsFadesOnlyAfterRedrawWithoutOwningTheirDuration() {
    auto packet = OpenPacket(10); packet["menuOpening"] = {{"revision", 1}, {"elapsedMilliseconds", 0}};
    Reject([&] { runtime::ParseState(packet.dump(), GetCurrentProcessId()); });
    packet["menuOpenTransition"]["phase"] = "cooldown";
    const auto drawing = runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot;
    Check(drawing->menuOpening == MenuOpening{1, 0} && runtime::InputModeForSnapshot(*drawing) == runtime::InputMode::MenuLocked,
        "Normal redraw failed to retain its independent row fade");
    // A short fade can finish before input readiness while a longer one can outlive it
    packet["menuOpening"] = nullptr;
    Check(runtime::InputModeForSnapshot(*runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot) == runtime::InputMode::MenuLocked,
        "Completed opacity unlocked normal opening early");
    packet["menuOpenTransition"] = nullptr; packet["menuOpening"] = {{"revision", 1}, {"elapsedMilliseconds", 550}};
    Check(runtime::InputModeForSnapshot(*runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot) == runtime::InputMode::Menu,
        "Continuing opacity kept normal opening locked");
    for (std::size_t count : {std::size_t{0}, std::size_t{1}}) {
        packet = OpenPacket(count); packet["menuOpenTransition"]["phase"] = "cooldown";
        packet["menuOpening"] = {{"revision", 1}, {"elapsedMilliseconds", 0}};
        Reject([&] { runtime::ParseState(packet.dump(), GetCurrentProcessId()); });
    }
}

void MenuOpenTransitionConsumesAllHeldControlsThroughHealthyAndLateUnlocks() {
    auto bindings = BindingsJson(); bindings["previousChoice"] = {51}; bindings["nextChoice"] = {52};
    // A late first redraw moves readiness to R + 550 rather than backdating an unseen HUD
    for (std::uint64_t redraw : {std::uint64_t{50}, std::uint64_t{1000}}) for (bool root : {false, true}) {
        for (const std::set<int>& keys : {std::set<int>{1}, {2}, {38}, {40}, {51}, {52}, {70}, {86}, {6}, {2, 86}}) {
            runtime::NativeInputRouter router(runtime::ParseBindings(bindings.dump()));
            auto packet = OpenPacket(10, root); packet["navigationRevision"] = 7; packet["menuRevision"] = 6;
            std::set<int> held;
            const auto poll = [&](std::uint64_t now) {
                const auto state = runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot;
                Check(state->navigationRevision == 7 && state->menuRevision == 6, "Normal redraw invented a public epoch");
                return router.Poll(true, true, false, [&](int key) { return held.contains(key); }, now,
                    state->navigationRevision, true, root, runtime::InputModeForSnapshot(*state),
                    state->appearance.scrollAnimationMilliseconds, state->menuRevision);
            };
            poll(0); held = keys; Check(!poll(49), "Rebuild allowed a held monitor command");
            packet["menuOpenTransition"]["phase"] = "cooldown"; packet["menuOpening"] = {{"revision", 1}, {"elapsedMilliseconds", 0}};
            Check(!poll(redraw), "Observed redraw replayed held input");
            packet["menuOpening"]["elapsedMilliseconds"] = 549;
            Check(!poll(redraw + 549), "Normal cooldown ended before its full source wait");
            packet["menuOpenTransition"] = nullptr; packet["menuOpening"]["elapsedMilliseconds"] = 550;
            Check(!poll(redraw + 550) && !poll(redraw + 551), "Normal unlock carried an old hold into active rows");
            // Another saved knife cannot bypass aggregate release after the lock consumed its hold
            if (held.contains(86)) { held.insert(6); Check(!poll(redraw + 551), "An alternate knife bypassed consumed aggregate release"); }
            held.clear(); Check(!poll(redraw + 552), "Consumed release produced a delayed Back or choice");
            held = {70}; Check(poll(redraw + 552) == runtime::Input::Select, "Fresh selection stayed locked after normal open");
        }
    }
}

void MenuOpenTransitionConsumesFocusPendingAndHiddenPacketsWithoutReplayingToggle() {
    runtime::NativeInputRouter router(runtime::ParseBindings(BindingsJson().dump()));
    auto packet = StateJson(); std::set<int> held; bool focused = true, pending = false;
    const auto poll = [&](std::uint64_t now) {
        const auto state = runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot;
        return router.Poll(focused, state->isOpen, pending, [&](int key) { return held.contains(key); }, now,
            state->navigationRevision, true, state->isRootMenu, runtime::InputModeForSnapshot(*state),
            state->appearance.scrollAnimationMilliseconds, state->menuRevision);
    };
    poll(0); held = {2, 86}; Check(poll(1) == runtime::Input::Toggle, "Fresh closed opening chord failed");
    pending = true; Check(!poll(2), "Pending opening replayed the toggle chord");
    packet = OpenPacket(8); packet["navigationRevision"] = 1; packet["menuRevision"] = 2;
    Check(!poll(3), "Acknowledged opening leaked the admission chord");
    held = {70}; Check(!poll(49), "Pending rebuild queued selection");
    // Focus projection closes only the display; the owner transition can continue off screen
    focused = false; packet["isOpen"] = false; packet["menuOpenTransition"] = nullptr; Check(!poll(50), "Focus loss emitted input");
    focused = true; pending = false; packet["isOpen"] = true; packet["menuOpenTransition"] = {{"phase", "cooldown"}};
    Check(!poll(599), "Focus return inherited a hidden selection");
    packet["menuOpenTransition"] = nullptr; Check(!poll(600), "Unlocked focus recovery replayed selection");
    held.clear(); poll(601); held = {70}; Check(poll(601) == runtime::Input::Select, "Fresh retry failed after hidden normal open");
}

void MenuOpenTransitionResumesRootChildPrioritiesAndRepeatWithoutNewGestures() {
    for (bool root : {false, true}) {
        // Unlocked rows resume the source monitor order while knife alone still returns
        for (const auto& [keys, expected] : {std::pair{std::set<int>{2, 86}, runtime::Input::Up},
            std::pair{std::set<int>{1, 70}, runtime::Input::Down},
            std::pair{std::set<int>{1, 2, 70}, runtime::Input::Select},
            std::pair{std::set<int>{70, 86}, runtime::Input::Select},
            std::pair{std::set<int>{86}, runtime::Input::Back}}) {
            runtime::NativeInputRouter router(runtime::ParseBindings(BindingsJson().dump()));
            auto packet = OpenPacket(8, root); packet["menuOpenTransition"]["phase"] = "cooldown";
            std::set<int> held;
            const auto poll = [&](std::uint64_t now) {
                const auto state = runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot;
                return router.Poll(true, true, false, [&](int key) { return held.contains(key); }, now, 7,
                    false, root, runtime::InputModeForSnapshot(*state), 120, std::uint64_t{6});
            };
            poll(50); packet["menuOpenTransition"] = nullptr; poll(600); held = keys;
            Check(poll(600) == expected, "Normal opening changed fresh root or child priority");
        }
    }
    // Fade completion does not interrupt an ordinary scroll armed after the independent unlock
    runtime::NativeInputRouter router(runtime::ParseBindings(BindingsJson().dump())); auto packet = OpenPacket(8);
    packet["menuOpenTransition"]["phase"] = "cooldown"; packet["menuOpening"] = {{"revision", 1}, {"elapsedMilliseconds", 0}};
    std::set<int> held;
    const auto poll = [&](std::uint64_t now) {
        const auto state = runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot;
        return router.Poll(true, true, false, [&](int key) { return held.contains(key); }, now, 7,
            false, true, runtime::InputModeForSnapshot(*state), 120, std::uint64_t{6});
    };
    poll(50); packet["menuOpenTransition"] = nullptr; packet["menuOpening"]["elapsedMilliseconds"] = 550; poll(600);
    held = {1}; Check(poll(600) == runtime::Input::Down, "Independent unlock failed to start a fresh scroll");
    packet["menuOpening"] = nullptr; Check(!poll(750) && !poll(794), "Fade completion shortened the captured repeat");
    Check(poll(795) == runtime::Input::Down, "Fade completion reset the established scroll repeat");
}
}
