#include "TestSupport.h"
#include <set>

namespace iw::overlay::tests {
namespace {
Json RebuildPacket() {
    auto packet = StateJson();
    packet["isOpen"] = true;
    packet["isRootMenu"] = false;
    packet["menuPreferenceRebuild"] = {{"phase", "rebuild"}};
    return packet;
}
} // namespace
void PreferenceRebuildPreservesLegacyAndOwnsBothLockedPhases() {
    auto packet = RebuildPacket();
    packet.erase("menuPreferenceRebuild");
    Check(!runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot->menuPreferenceRebuild,
          "Legacy preference packet invented a rebuild");
    packet["menuPreferenceRebuild"] = nullptr;
    Reject([&] { runtime::ParseState(packet.dump(), GetCurrentProcessId()); });
    for (const auto *phase : {"rebuild", "cooldown"}) {
        packet["menuPreferenceRebuild"] = {{"phase", phase}};
        const auto state = runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot;
        packet["menuPreferenceRebuild"]["phase"] = "changed";
        Check(state->menuPreferenceRebuild == (std::string(phase) == "rebuild"
                                                   ? MenuPreferenceRebuildPhase::Rebuild
                                                   : MenuPreferenceRebuildPhase::Cooldown) &&
                  runtime::InputModeForSnapshot(*state) == runtime::InputMode::MenuLocked,
              "Preference rebuild was not owned or did not pause controls");
    }
}
void PreferenceRebuildRejectsMalformedPhasesExtrasAndDecodedDuplicates() {
    for (const Json &invalid :
         {Json(false), Json(1), Json("rebuild"), Json::array(), Json::object(), Json{{"phase", nullptr}},
          Json{{"phase", true}}, Json{{"phase", 1}}, Json{{"phase", "pending"}}, Json{{"phase", "Rebuild"}},
          Json{{"phase", "rebuild"}, {"footerDesign", "AIO"}}}) {
        auto packet = RebuildPacket();
        packet["menuPreferenceRebuild"] = invalid;
        Reject([&] { runtime::ParseState(packet.dump(), GetCurrentProcessId()); });
    }
    for (const auto *duplicate : {"\"phase\":\"cooldown\"", "\"\\u0070hase\":\"cooldown\""}) {
        auto bytes = RebuildPacket().dump();
        const std::string field = "\"phase\":\"rebuild\"";
        bytes.replace(bytes.find(field), field.size(), field + "," + duplicate);
        Reject([&] { runtime::ParseState(bytes, GetCurrentProcessId()); });
    }
    // Duplicate top-level metadata cannot erase the active pause with a second value
    for (const auto *duplicate : {"\"menuPreferenceRebuild\":null", "\"menuPreference\\u0052ebuild\":null"}) {
        auto bytes = RebuildPacket().dump();
        const std::string field = "\"menuPreferenceRebuild\":{\"phase\":\"rebuild\"}";
        bytes.replace(bytes.find(field), field.size(), field + "," + duplicate);
        Reject([&] { runtime::ParseState(bytes, GetCurrentProcessId()); });
    }
}
void PreferenceRebuildRejectsInvalidRowsAndEveryCompetingModal() {
    // Each pause belongs to one populated child menu and cannot borrow another modal lifetime
    for (const auto *phase : {"rebuild", "cooldown"})
        for (int conflict = 0; conflict < 14; ++conflict) {
            auto packet = RebuildPacket();
            packet["menuPreferenceRebuild"]["phase"] = phase;
            if (conflict == 0)
                packet["isOpen"] = false;
            if (conflict == 1)
                packet["isRootMenu"] = true;
            if (conflict == 2)
                packet["rows"] = Json::array();
            if (conflict == 3)
                packet["selectedIndex"] = 99;
            if (conflict == 4)
                packet["widthEditor"] = {{"step", 1}};
            if (conflict == 5)
                packet["instructionPositionEditor"] = {{"phase", "editing"}};
            if (conflict == 6)
                packet["menuPositionEditor"] = {{"step", 1}};
            if (conflict == 7)
                packet["menuCredits"] = {{"elapsedMilliseconds", 0}};
            if (conflict == 8)
                packet["menuUnlock"] = "rebuild";
            if (conflict == 9)
                packet["menuPreferenceRefresh"] = {{"phase", "pending"}};
            if (conflict == 10)
                packet["menuPreferenceRefresh"] = {{"phase", "cooldown"}};
            if (conflict == 11)
                packet["menuDesignTransition"] = {{"phase", "rebuild"}};
            if (conflict == 12)
                packet["menuDesignTransition"] = {{"phase", "cooldown"}};
            if (conflict == 13)
                packet["menuOpenTransition"] = {{"phase", "cooldown"}};
            if (conflict >= 4) {
                if (conflict <= 7)
                    packet["rows"] = Json::array();
                auto standalone = packet;
                standalone.erase("menuPreferenceRebuild");
                runtime::ParseState(standalone.dump(), GetCurrentProcessId());
            }
            Reject([&] { runtime::ParseState(packet.dump(), GetCurrentProcessId()); });
        }
}
void PreferenceRebuildAllowsOnlyPostRedrawOpeningWithinStealthCapacity() {
    auto packet = RebuildPacket();
    packet["rows"] = Json::array();
    for (int row = 0; row < 12; ++row)
        packet["rows"].push_back({{"label", "Row"}, {"value", "OFF"}, {"enabled", true}});
    packet["appearance"] = {{"booleanDisplay", "Text"},    {"boolBoxLocation", "Right"},
                            {"instructionsVisible", true}, {"optionCounter", false},
                            {"coloredCursor", true},       {"spotlightCursor", true},
                            {"largeCursor", false},        {"stealthUI", true}};
    packet["menuOpening"] = {{"revision", 2}, {"elapsedMilliseconds", 0}};
    Reject([&] { runtime::ParseState(packet.dump(), GetCurrentProcessId()); });
    packet["menuPreferenceRebuild"]["phase"] = "cooldown";
    packet["menuOpening"]["elapsedMilliseconds"] = 399;
    const auto state = runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot;
    Check(state->menuOpening == MenuOpening{2, 399} &&
              runtime::InputModeForSnapshot(*state) == runtime::InputMode::MenuLocked,
          "Redrawn Stealth fade changed its independent input pause");
    packet["menuOpening"]["elapsedMilliseconds"] = 400;
    Reject([&] { runtime::ParseState(packet.dump(), GetCurrentProcessId()); });
}
void PreferenceRebuildConsumesHeldPendingAndFocusEdgesUntilFreshRelease() {
    for (const std::set<int> &keys :
         {std::set<int>{1}, {2}, {38}, {40}, {51}, {52}, {70}, {86}, {6}, {2, 86}}) {
        auto bindings = BindingsJson();
        bindings["previousChoice"] = {51};
        bindings["nextChoice"] = {52};
        runtime::NativeInputRouter router(runtime::ParseBindings(bindings.dump()));
        auto packet = RebuildPacket();
        packet["navigationRevision"] = 7;
        packet["menuRevision"] = 6;
        std::set<int> held;
        bool focused = true, pending = false;
        const auto poll = [&](std::uint64_t now) {
            const auto state = runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot;
            return router.Poll(
                focused, true, pending, [&](int key) { return held.contains(key); }, now,
                state->navigationRevision, true, false, runtime::InputModeForSnapshot(*state), 120,
                state->menuRevision);
        };
        poll(0);
        held = keys;
        Check(!poll(49), "Preference rebuild dispatched held controls");
        packet["menuPreferenceRebuild"]["phase"] = "cooldown";
        packet["menuRevision"] = 7;
        Check(!poll(50), "Redraw replayed a consumed edge");
        focused = false;
        pending = true;
        Check(!poll(100), "Hidden pending rebuild emitted input");
        focused = true;
        pending = false;
        Check(!poll(249), "Focus return unlocked preference controls");
        packet.erase("menuPreferenceRebuild");
        Check(!poll(250) && !poll(251), "Unlock replayed a held action");
        held.clear();
        poll(252);
        held = {70};
        Check(poll(252) == runtime::Input::Select, "Fresh selection failed after preference unlock");
    }
}
} // namespace iw::overlay::tests

namespace iw::overlay::tests {
void PreferencePendingRefreshPreservesOpeningUntilActualRedraw() {
    auto packet = RebuildPacket();
    packet.erase("menuPreferenceRebuild");
    packet["rows"] = Json::array();
    for (int row = 0; row < 12; ++row)
        packet["rows"].push_back({{"label", "Row"}, {"value", "OFF"}, {"enabled", true}});
    packet["menuPreferenceRefresh"] = {{"phase", "pending"}};
    packet["menuOpening"] = {{"revision", 2}, {"elapsedMilliseconds", 729}};
    // The pending refresh keeps the old valid window until the backend publishes its redraw
    const auto state = runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot;
    Check(state->menuOpening == MenuOpening{2, 729} &&
              runtime::InputModeForSnapshot(*state) == runtime::InputMode::MenuLocked,
          "Pending preference admission canceled an active opening fade");
    packet["menuPreferenceRefresh"]["phase"] = "cooldown";
    Reject([&] { runtime::ParseState(packet.dump(), GetCurrentProcessId()); });
    packet.erase("menuOpening");
    Check(
        runtime::InputModeForSnapshot(*runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot) ==
            runtime::InputMode::MenuLocked,
        "Redraw fade cancellation unlocked preference input early");
}
} // namespace iw::overlay::tests
