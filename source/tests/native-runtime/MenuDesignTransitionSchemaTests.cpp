#include "TestSupport.h"
#include <set>

namespace iw::overlay::tests {
namespace {
Json TransitionPacket() {
    auto packet = StateJson(); packet["isOpen"] = true; packet["isRootMenu"] = false;
    packet["title"] = "Menu Preferences"; packet["navigationRevision"] = 7; packet["menuRevision"] = 6;
    packet["menuDesignTransition"] = {{"phase", "rebuild"}};
    return packet;
}
}
void MenuDesignTransitionPreservesLegacyAndOwnsBothLockedPhases() {
    auto packet = TransitionPacket(); packet.erase("menuDesignTransition");
    Check(!runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot->menuDesignTransition,
        "A legacy packet invented a design transition");
    packet["menuDesignTransition"] = nullptr;
    Check(runtime::InputModeForSnapshot(*runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot) == runtime::InputMode::Menu,
        "A null design transition locked normal controls");
    for (const auto* phase : {"rebuild", "cooldown"}) {
        packet["menuDesignTransition"] = {{"phase", phase}};
        const auto owned = runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot;
        packet["menuDesignTransition"]["phase"] = "changed";
        Check(owned->menuDesignTransition == (std::string(phase) == "rebuild" ? MenuDesignTransitionPhase::Rebuild
            : MenuDesignTransitionPhase::Cooldown) && runtime::InputModeForSnapshot(*owned) == runtime::InputMode::MenuLocked
            && owned->rows.size() == 1 && owned->rows[0].value == "OFF" && owned->navigationRevision == 7 && owned->menuRevision == 6,
            "A design transition changed ordinary rows, epochs or owned phase state");
    }
}
void MenuDesignTransitionRejectsMalformedPhasesAndDecodedDuplicates() {
    for (const Json& invalid : {Json(false), Json(1), Json("rebuild"), Json::array(), Json::object(),
        Json{{"phase", nullptr}}, Json{{"phase", false}}, Json{{"phase", 1}}, Json{{"phase", 0.0}},
        Json{{"phase", "pending"}}, Json{{"phase", "Rebuild"}}, Json{{"phase", ""}},
        Json{{"phase", "rebuild"}, {"duration", 50}}}) {
        auto packet = TransitionPacket(); packet["menuDesignTransition"] = invalid;
        Reject([&] { runtime::ParseState(packet.dump(), GetCurrentProcessId()); });
    }
    for (const auto* duplicate : {"{\"phase\":\"rebuild\",\"phase\":\"cooldown\"}",
        "{\"phase\":\"rebuild\",\"\\u0070hase\":\"cooldown\"}"}) {
        auto bytes = TransitionPacket().dump(); const std::string object = "{\"phase\":\"rebuild\"}";
        const auto start = bytes.find(object); Check(start != std::string::npos, "Design transition duplicate fixture lost its object");
        bytes.replace(start, object.size(), duplicate);
        Reject([&] { runtime::ParseState(bytes, GetCurrentProcessId()); });
    }
}
void MenuDesignTransitionRejectsInvalidOrdinaryContextsAndEveryModalConflict() {
    for (int conflict = 0; conflict < 16; ++conflict) {
        auto packet = TransitionPacket();
        if (conflict == 0) packet["isOpen"] = false;
        if (conflict == 1) packet["isRootMenu"] = true;
        if (conflict == 2) packet["rows"] = Json::array();
        if (conflict == 3) packet["selectedIndex"] = 1;
        if (conflict == 4) packet["rows"] = Json::object();
        if (conflict == 5) packet["widthEditor"] = {{"step", 1}};
        if (conflict == 6) packet["instructionPositionEditor"] = {{"phase", "editing"}};
        if (conflict == 7) packet["menuPositionEditor"] = {{"step", 1}};
        if (conflict == 8) packet["menuCredits"] = {{"elapsedMilliseconds", 0}};
        if (conflict == 9) packet["menuUnlock"] = "rebuild";
        if (conflict == 10) packet["menuUnlock"] = "pause";
        if (conflict == 11) packet["menuPreferenceRefresh"] = {{"phase", "pending"}};
        if (conflict == 12) packet["menuPreferenceRefresh"] = {{"phase", "cooldown"}};
        if (conflict == 13) packet["selectedIndex"] = 0.0;
        if (conflict == 14) packet["selectedIndex"] = false;
        if (conflict == 15) packet["selectedIndex"] = -1;
        Reject([&] { runtime::ParseState(packet.dump(), GetCurrentProcessId()); });
    }
}
void MenuDesignTransitionJoinsBoundariesAndConsumesPendingFocusAndHeldControls() {
    auto bindings = BindingsJson(); bindings["previousChoice"] = {51}; bindings["nextChoice"] = {52};
    for (int key : {1, 2, 38, 40, 51, 52, 70, 86, 6}) {
        runtime::NativeInputRouter router(runtime::ParseBindings(bindings.dump()));
        auto packet = TransitionPacket(); packet["menuDesignTransition"]["footerDesign"] = "AIO";
        std::set<int> held; bool focused = true, pending = false;
        const auto poll = [&](std::uint64_t now) {
            const auto state = runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot;
            return router.Poll(focused, true, pending, [&](int candidate) { return held.contains(candidate); }, now,
                state->navigationRevision, true, false, runtime::InputModeForSnapshot(*state),
                state->appearance.scrollAnimationMilliseconds, state->menuRevision);
        };
        poll(0); held = {key}; pending = true; Check(!poll(0), "Pending design transition queued a command");
        focused = false; Check(!poll(20), "Lost focus during design rebuilding queued a command");
        focused = true; pending = false; Check(!poll(49), "Design rebuild inherited a focus-return hold");
        packet["menuDesignTransition"].erase("footerDesign");
        packet["menuDesignTransition"]["phase"] = "cooldown"; packet["menuRevision"] = 7;
        Check(!poll(50) && !poll(249), "Visible design cooldown accepted held input");
        packet["menuDesignTransition"] = nullptr;
        Check(!poll(250) && !poll(1000), "Design unlock replayed a held control");
        held.clear(); Check(!poll(1001), "Design unlock release produced a deferred command");
        held = {70}; Check(poll(1001) == runtime::Input::Select, "Fresh selection failed after design unlock");
    }
}

void MenuDesignSameChoiceCooldownConsumesHoldsWithoutChangingEitherEpoch() {
    auto bindings = BindingsJson(); bindings["previousChoice"] = {51}; bindings["nextChoice"] = {52};
    for (bool recoverFocus : {false, true}) {
        for (int key : {1, 2, 38, 40, 51, 52, 70, 86, 6}) {
            runtime::NativeInputRouter router(runtime::ParseBindings(bindings.dump()));
            auto packet = TransitionPacket(); packet["menuDesignTransition"]["phase"] = "cooldown";
            std::set<int> held; bool focused = true, pending = false;
            const auto poll = [&](std::uint64_t now) {
                const auto state = runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot;
                Check(state->navigationRevision == 7 && state->menuRevision == 6,
                    "Same-design cooldown fixture changed an epoch and hid a mode-reseeding failure");
                return router.Poll(focused, true, pending, [&](int candidate) { return held.contains(candidate); }, now,
                    state->navigationRevision, false, false, runtime::InputModeForSnapshot(*state),
                    state->appearance.scrollAnimationMilliseconds, state->menuRevision);
            };
            poll(0); held = {key}; pending = true;
            Check(!poll(0), "Same-design pending cooldown accepted a saved control");
            if (recoverFocus) {
                focused = false; Check(!poll(20), "Same-design focus loss replayed a pending control");
                focused = true; Check(!poll(21), "Same-design focus return replayed a held control");
            }
            pending = false; Check(!poll(249), "Same-design acknowledgement queued a cooldown command");
            // Unlock changes only metadata and input mode, not either public epoch
            packet["menuDesignTransition"] = nullptr;
            Check(!poll(250) && !poll(1000), "Same-design unlock replayed a hold with unchanged epochs");
            held.clear(); Check(!poll(1001), "Same-design release queued a deferred command");
            held = {70}; Check(poll(1001) == runtime::Input::Select,
                "Fresh selection failed after the unchanged-epoch design cooldown");
        }
    }
}
}
