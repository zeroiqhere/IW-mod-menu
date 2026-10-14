#include "TestSupport.h"
#include <set>

namespace iw::overlay::tests {
namespace {
Json RefreshPacket() {
    auto packet = StateJson();
    packet["isOpen"] = true; packet["isRootMenu"] = false;
    packet["title"] = "Menu Preferences";
    packet["menuPreferenceRefresh"] = {{"phase", "pending"}};
    return packet;
}
}
void PreferenceRefreshRejectsMalformedPhasesAndDuplicateFields() {
    // An inactive null is handled separately from every malformed active shape
    for (const Json& invalid : {Json(false), Json(1), Json("pending"), Json::array(), Json::object(),
        Json{{"phase", nullptr}}, Json{{"phase", true}}, Json{{"phase", 1}}, Json{{"phase", 1.0}},
        Json{{"phase", "Pending"}}, Json{{"phase", "pause"}}, Json{{"phase", ""}},
        Json{{"phase", "pending"}, {"instructions", Json::array()}}}) {
        auto packet = RefreshPacket(); packet["menuPreferenceRefresh"] = invalid;
        Reject([&] { runtime::ParseState(packet.dump(), GetCurrentProcessId()); });
    }
    for (const auto* duplicate : {"{\"phase\":\"pending\",\"phase\":\"cooldown\"}",
        "{\"phase\":\"pending\",\"\\u0070hase\":\"cooldown\"}"}) {
        auto bytes = RefreshPacket().dump(); const std::string object = "{\"phase\":\"pending\"}";
        const auto start = bytes.find(object); Check(start != std::string::npos, "Refresh duplicate fixture lost its object");
        bytes.replace(start, object.size(), duplicate);
        Reject([&] { runtime::ParseState(bytes, GetCurrentProcessId()); });
    }
}

void PreferenceRefreshPreservesLegacyAndOwnsBothPhasesWithoutChangingRows() {
    auto packet = RefreshPacket(); packet.erase("menuPreferenceRefresh");
    Check(!runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot->menuPreferenceRefresh,
        "Legacy state invented a preference refresh");
    packet["menuPreferenceRefresh"] = nullptr;
    Check(!runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot->menuPreferenceRefresh,
        "Inactive refresh changed legacy controls");
    for (const auto* phase : {"pending", "cooldown"}) {
        packet["menuPreferenceRefresh"] = {{"phase", phase}};
        const auto snapshot = runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot;
        packet["menuPreferenceRefresh"]["phase"] = "changed";
        const auto expected = std::string(phase) == "pending" ? MenuPreferenceRefreshPhase::Pending
            : MenuPreferenceRefreshPhase::Cooldown;
        Check(snapshot->menuPreferenceRefresh == expected && snapshot->rows.size() == 1
            && snapshot->rows[0].label == "Row" && snapshot->rows[0].value == "OFF" && snapshot->rows[0].enabled
            && snapshot->selectedIndex == 0 && snapshot->title == "Menu Preferences"
            && runtime::InputModeForSnapshot(*snapshot) == runtime::InputMode::MenuLocked,
            "Refresh retained caller storage or replaced ordinary menu display");
    }
}
void PreferenceRefreshRejectsEveryModalAndInvalidOrdinaryContext() {
    for (int conflict = 0; conflict < 14; ++conflict) {
        auto packet = RefreshPacket();
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
        if (conflict == 11) packet["selectedIndex"] = 0.0;
        if (conflict == 12) packet["selectedIndex"] = false;
        if (conflict == 13) packet["selectedIndex"] = -1;
        Reject([&] { runtime::ParseState(packet.dump(), GetCurrentProcessId()); });
    }
    // Typed callers also fail safe if they construct conflicting metadata outside JSON
    MenuSnapshot typed; typed.menuPreferenceRefresh = MenuPreferenceRefreshPhase::Pending;
    typed.menuCredits = MenuCredits{};
    Check(runtime::InputModeForSnapshot(typed) == runtime::InputMode::MenuLocked,
        "A typed refresh conflict escaped the paused route");
}
void PreferenceRefreshJoinsPhaseBoundariesAndConsumesEveryHeldControl() {
    auto bindings = BindingsJson(); bindings["previousChoice"] = {51}; bindings["nextChoice"] = {52};
    for (int key : {1, 2, 38, 40, 51, 52, 70, 86, 6}) {
        runtime::NativeInputRouter router(runtime::ParseBindings(bindings.dump()));
        auto packet = RefreshPacket(); packet["navigationRevision"] = 12; packet["menuRevision"] = 6;
        std::set<int> held;
        const auto poll = [&](std::uint64_t now) {
            const auto snapshot = runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot;
            return router.Poll(true, true, false, [&](int candidate) { return held.contains(candidate); }, now,
                snapshot->navigationRevision, true, false, runtime::InputModeForSnapshot(*snapshot),
                snapshot->appearance.scrollAnimationMilliseconds, snapshot->menuRevision);
        };
        poll(0); held = {key};
        Check(!poll(0) && !poll(179), "Pending refresh produced a menu command");
        packet["menuPreferenceRefresh"]["phase"] = "cooldown"; packet["menuRevision"] = 7;
        Check(!poll(180) && !poll(429), "Cooldown refresh replayed a held command");
        packet["menuPreferenceRefresh"] = nullptr;
        Check(!poll(430) && !poll(1000), "Unlock inherited a held control from refresh");
        held.clear(); Check(!poll(1001), "Release after refresh produced a deferred action");
        held = {70}; Check(poll(1001) == runtime::Input::Select, "A fresh selection stayed paused after refresh");
    }
}
void PreferenceRefreshConsumesPendingFocusLossAndRestoresNormalRouting() {
    runtime::NativeInputRouter router(runtime::ParseBindings(BindingsJson().dump()));
    auto packet = RefreshPacket(); packet["navigationRevision"] = 12; packet["menuRevision"] = 6;
    std::set<int> held; bool focused = true, pending = false;
    const auto poll = [&](std::uint64_t now) {
        const auto snapshot = runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot;
        return router.Poll(focused, true, pending, [&](int key) { return held.contains(key); }, now,
            snapshot->navigationRevision, true, false, runtime::InputModeForSnapshot(*snapshot),
            snapshot->appearance.scrollAnimationMilliseconds, snapshot->menuRevision);
    };
    poll(0); pending = true; held = {70}; Check(!poll(1), "Pending refresh queued selection");
    focused = false; Check(!poll(10), "Focus loss during refresh queued selection");
    focused = true; pending = false; Check(!poll(179), "Focus recovery replayed refresh selection");
    packet["menuPreferenceRefresh"]["phase"] = "cooldown"; packet["menuRevision"] = 7;
    Check(!poll(180), "Refresh epoch change replayed selection");
    packet["menuPreferenceRefresh"] = nullptr;
    Check(!poll(430), "Refresh unlock replayed pending selection");
    held.clear(); poll(431); held = {70}; Check(poll(431) == runtime::Input::Select,
        "Acknowledged fresh selection failed after focus recovery");
    // Unrelated ordinary contexts remain on their established mode and close gesture
    packet.erase("menuPreferenceRefresh"); packet["isRootMenu"] = true;
    const auto ordinary = runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot;
    Check(runtime::InputModeForSnapshot(*ordinary) == runtime::InputMode::Menu,
        "The local preference refresh changed unrelated normal control routing");
}
}
