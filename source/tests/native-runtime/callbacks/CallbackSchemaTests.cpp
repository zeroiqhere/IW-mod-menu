#include "CallbackTestModels.h"
#include "../scroll/SpacerScrollTestModels.h"
#include <limits>
#include <set>

namespace iw::overlay::tests::callback {
void OwnsLegacyAndExactCallbackPhases() {
    auto legacy = Packet(); legacy.erase("menuOptionCallback");
    Check(!runtime::ParseState(legacy.dump(), GetCurrentProcessId()).snapshot->menuOptionCallback,
        "Legacy packet invented callback ownership");
    for (const auto kind : {MenuOptionCallbackKind::Boolean, MenuOptionCallbackKind::Plain}) {
        for (const auto phase : {MenuOptionCallbackPhase::Pending, MenuOptionCallbackPhase::Cooldown}) {
            if (kind == MenuOptionCallbackKind::Plain && phase == MenuOptionCallbackPhase::Pending) continue;
            for (bool root : {false, true}) for (const char* surface : {"main", "quick"}) {
                auto packet = Packet(kind == MenuOptionCallbackKind::Boolean ? "boolean" : "plain",
                    phase == MenuOptionCallbackPhase::Pending ? "pending" : "cooldown");
                packet["isRootMenu"] = root; packet["menuSurface"] = surface;
                packet["menuOptionCallback"]["revision"] = std::numeric_limits<std::uint64_t>::max();
                const auto snapshot = runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot;
                packet["menuOptionCallback"]["revision"] = 1;
                Check(snapshot->menuOptionCallback == MenuOptionCallback{std::numeric_limits<std::uint64_t>::max(), kind, phase}
                    && snapshot->rows[0].value == "OFF" && snapshot->rows[1].value == "UNKNOWN"
                    && snapshot->navigationRevision == 12 && snapshot->menuRevision == 6
                    && runtime::InputModeForSnapshot(*snapshot) == runtime::InputMode::MenuLocked,
                    "Callback ownership, lock or untouched game values were lost");
            }
        }
    }
}
void RejectsMalformedCallbackFieldsAndDecodedDuplicates() {
    const auto valid = Packet().at("menuOptionCallback");
    for (const Json& bad : {Json(nullptr), Json(false), Json(1), Json("pending"), Json::array(), Json::object()}) {
        auto packet = Packet(); packet["menuOptionCallback"] = bad;
        Reject([&] { runtime::ParseState(packet.dump(), GetCurrentProcessId()); });
    }
    for (const char* field : {"revision", "kind", "phase"}) {
        auto packet = Packet(); packet["menuOptionCallback"].erase(field);
        Reject([&] { runtime::ParseState(packet.dump(), GetCurrentProcessId()); });
        for (const Json& bad : {Json(nullptr), Json(false), Json::array(), Json::object()}) {
            packet = Packet(); packet["menuOptionCallback"][field] = bad;
            Reject([&] { runtime::ParseState(packet.dump(), GetCurrentProcessId()); });
        }
    }
    for (const Json& bad : {Json(0), Json(-1), Json(1.0), Json("7"), Json("18446744073709551616")}) {
        auto packet = Packet(); packet["menuOptionCallback"]["revision"] = bad;
        Reject([&] { runtime::ParseState(packet.dump(), GetCurrentProcessId()); });
    }
    for (const char* field : {"kind", "phase"}) for (const Json& bad : {Json(1), Json(1.0), Json("Boolean"), Json("waiting"), Json("")}) {
        auto packet = Packet(); packet["menuOptionCallback"][field] = bad;
        Reject([&] { runtime::ParseState(packet.dump(), GetCurrentProcessId()); });
    }
    auto packet = Packet("plain", "pending");
    Reject([&] { runtime::ParseState(packet.dump(), GetCurrentProcessId()); });
    packet = Packet(); packet["menuOptionCallback"]["extra"] = true;
    Reject([&] { runtime::ParseState(packet.dump(), GetCurrentProcessId()); });
    for (const char* field : {"revision", "kind", "phase", "\\u0072evision"}) {
        auto bytes = Packet().dump(); const auto original = valid.dump();
        const auto offset = bytes.find(original); Check(offset != std::string::npos, "Callback duplicate fixture lost its object");
        auto duplicate = original; duplicate.pop_back(); duplicate += std::string(",\"") + field + "\":7}";
        bytes.replace(offset, original.size(), duplicate);
        Reject([&] { runtime::ParseState(bytes, GetCurrentProcessId()); });
    }
    auto bytes = Packet().dump(); const auto start = bytes.find("\"revision\":7");
    bytes.replace(start, 12, "\"revision\":18446744073709551616");
    Reject([&] { runtime::ParseState(bytes, GetCurrentProcessId()); });
}
void RejectsInvalidCallbackContextsAndEveryCompetingWait() {
    for (int conflict = 0; conflict < 13; ++conflict) {
        auto packet = Packet();
        if (conflict == 0) packet["isOpen"] = false;
        if (conflict == 1) packet["rows"] = Json::array();
        if (conflict == 2) packet["selectedIndex"] = 2;
        if (conflict == 3) packet["rows"][0] = {{"label", ""}, {"value", ""}, {"enabled", false}, {"isSpacer", true}};
        if (conflict == 4) packet["widthEditor"] = {{"step", 1}};
        if (conflict == 5) packet["instructionPositionEditor"] = {{"phase", "editing"}};
        if (conflict == 6) packet["menuPositionEditor"] = {{"step", 1}};
        if (conflict == 7) packet["menuCredits"] = {{"elapsedMilliseconds", 0}};
        if (conflict == 8) packet["menuUnlock"] = "rebuild";
        if (conflict == 9) packet["menuPreferenceRefresh"] = {{"phase", "pending"}};
        if (conflict == 10) packet["menuDesignTransition"] = {{"phase", "cooldown"}};
        if (conflict == 11) packet["menuOpenTransition"] = {{"phase", "cooldown"}};
        if (conflict == 12) packet["menuPreferenceRebuild"] = {{"phase", "cooldown"}};
        Reject([&] { runtime::ParseState(packet.dump(), GetCurrentProcessId()); });
    }
}
void CallbackOpeningCompatibilityDoesNotInventRedrawsOrValues() {
    for (const char* kind : {"boolean", "plain"}) for (const char* phase : {"pending", "cooldown"}) {
        auto packet = Packet(kind, phase); packet["menuOpening"] = {{"revision", 2}, {"elapsedMilliseconds", 99}};
        if ((std::string(kind) == "plain" && std::string(phase) == "pending")
            || (std::string(kind) == "boolean" && std::string(phase) == "cooldown")) {
            Reject([&] { runtime::ParseState(packet.dump(), GetCurrentProcessId()); });
        } else {
            const auto snapshot = runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot;
            Check(snapshot->menuOpening == MenuOpening{2, 99} && snapshot->rows[1].value == "UNKNOWN"
                && snapshot->menuRevision == 6 && snapshot->navigationRevision == 12,
                "A callback descriptor fabricated game success or another redraw epoch");
        }
    }
}
void CallbackModesConsumeHeldPendingAndFocusInputsUntilFreshRelease() {
    for (bool plain : {false, true}) for (int key : {1, 2, 38, 40, 70, 86, 6}) {
        runtime::NativeInputRouter router(runtime::ParseBindings(BindingsJson().dump()));
        auto packet = Packet(plain ? "plain" : "boolean", plain ? "cooldown" : "pending");
        std::set<int> held; bool focus = true, pending = false;
        const auto poll = [&](std::uint64_t now) {
            const auto snapshot = runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot;
            return router.Poll(focus, true, pending, [&](int candidate) { return held.contains(candidate); }, now,
                snapshot->navigationRevision, false, snapshot->isRootMenu, runtime::InputModeForSnapshot(*snapshot),
                snapshot->appearance.scrollAnimationMilliseconds, snapshot->menuRevision);
        };
        poll(0); held = {key}; pending = true;
        Check(!poll(1), "Callback pending ACK dispatched an ordinary key");
        focus = false; Check(!poll(100), "Callback focus loss dispatched a key");
        focus = true; pending = false; Check(!poll(179), "Callback recovery replayed an old key");
        if (!plain) { packet["menuOptionCallback"]["phase"] = "cooldown"; packet["menuRevision"] = 7; }
        Check(!poll(180) && !poll(plain ? 249 : 429), "Callback cadence dispatched an ordinary key");
        packet.erase("menuOptionCallback");
        Check(!poll(plain ? 250 : 430) && !poll(1000), "Callback completion replayed a held key");
        held.clear(); Check(!poll(1001), "Callback release produced deferred input");
        held = {70}; Check(poll(1002) == runtime::Input::Select, "Fresh selection stayed paused after callback");
    }
}
void CallbackWaitRetiresPrivateScrollReceipts() {
    const auto source = spacer::Source();
    for (const auto kind : {MenuOptionCallbackKind::Boolean, MenuOptionCallbackKind::Plain}) {
        runtime::scroll::SpacerScrollReceiptTracker tracker;
        Check(tracker.Capture(source, SpacerScrollDirection::Down, 11, 100), "Ordinary private capture failed");
        auto target = spacer::Target(source, 3);
        target.menuOptionCallback = MenuOptionCallback{7, kind,
            kind == MenuOptionCallbackKind::Boolean ? MenuOptionCallbackPhase::Pending : MenuOptionCallbackPhase::Cooldown};
        Check(!tracker.Observe(target, 11, 120), "Callback borrowed a pre-wait private receipt");
        Check(!tracker.Capture(target, SpacerScrollDirection::Down, 12, 200), "Callback admitted a new private scroll");
    }
}
}
