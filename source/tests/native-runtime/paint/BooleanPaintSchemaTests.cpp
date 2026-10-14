#include "BooleanPaintTests.h"
#include "../../../native/shared/paint/RowDisplayValue.h"
#include <limits>

namespace iw::overlay::tests::paint {
void OwnsBooleanPaintWithoutChangingCurrentRowsOrInputMode() {
    auto legacy = Packet(); legacy.erase("menuBooleanPaint");
    Check(!runtime::ParseState(legacy.dump(), GetCurrentProcessId()).snapshot->menuBooleanPaint, "Legacy packet invented cached paint");
    for (bool root : {false, true}) for (const char* surface : {"main", "quick"}) {
        auto packet = Packet(); packet["isRootMenu"] = root; packet["menuSurface"] = surface;
        packet["menuBooleanPaint"]["revision"] = std::numeric_limits<std::uint64_t>::max();
        const auto state = runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot;
        Check(state->menuBooleanPaint && state->menuBooleanPaint->revision == std::numeric_limits<std::uint64_t>::max()
            && state->menuBooleanPaint->count == 2 && state->rows[0].value == "OFF" && state->rows[0].enabled
            && state->rows[1].value == "UNKNOWN" && !state->rows[1].enabled
            && ResolveRowDisplayValue(*state, 0) == "ON" && ResolveRowDisplayValue(*state, 1) == "UNKNOWN"
            && runtime::InputModeForSnapshot(*state) == runtime::InputMode::Menu,
            "Owned paint was discarded or changed current feature admission");
    }
    for (const char* phase : {"pending", "cooldown"}) {
        auto packet = Packet(); packet["menuOptionCallback"] = {{"revision", 3}, {"kind", "boolean"}, {"phase", phase}};
        Check(runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot->menuBooleanPaint.has_value(), "Callback lost cached checkbox paint");
    }
    auto packet = Packet(); packet["menuOpening"] = {{"revision", 2}, {"elapsedMilliseconds", 50}};
    packet["menuOpenTransition"] = {{"phase", "cooldown"}};
    Check(runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot->menuBooleanPaint.has_value(), "Drawn normal opening lost cached paint");
}
void RejectsMalformedPaintFieldsBoundsAndDecodedDuplicates() {
    const auto parse = [](const Json& packet) { runtime::ParseState(packet.dump(), GetCurrentProcessId()); };
    for (const Json& value : {Json(nullptr), Json(false), Json(1), Json("ON"), Json::array(), Json::object()}) {
        auto packet = Packet(); packet["menuBooleanPaint"] = value; Reject([&] { parse(packet); });
    }
    for (const char* field : {"revision", "rows"}) {
        auto packet = Packet(); packet["menuBooleanPaint"].erase(field); Reject([&] { parse(packet); });
    }
    for (const Json& value : {Json(nullptr), Json(false), Json(0), Json(-1), Json(1.0), Json("7")}) {
        auto packet = Packet(); packet["menuBooleanPaint"]["revision"] = value; Reject([&] { parse(packet); });
    }
    for (const Json& value : {Json(nullptr), Json(false), Json(1), Json("ON"), Json::object(), Json::array()}) {
        auto packet = Packet(); packet["menuBooleanPaint"]["rows"] = value; Reject([&] { parse(packet); });
    }
    for (const char* field : {"index", "value"}) {
        auto packet = Packet(); packet["menuBooleanPaint"]["rows"][0].erase(field); Reject([&] { parse(packet); });
        for (const Json& value : {Json(nullptr), Json(false), Json::object(), Json::array()}) {
            packet = Packet(); packet["menuBooleanPaint"]["rows"][0][field] = value; Reject([&] { parse(packet); });
        }
    }
    for (const Json& value : {Json(-1), Json(128), Json(1.0), Json("0")}) {
        auto packet = Packet(); packet["menuBooleanPaint"]["rows"][0]["index"] = value; Reject([&] { parse(packet); });
    }
    for (const Json& value : {Json(1), Json("on"), Json("OFF "), Json(""), Json("UNAVAILABLE")}) {
        auto packet = Packet(); packet["menuBooleanPaint"]["rows"][0]["value"] = value; Reject([&] { parse(packet); });
    }
    for (int change = 0; change < 4; ++change) {
        auto packet = Packet(); auto& cache = packet["menuBooleanPaint"];
        if (change == 0) cache["extra"] = true;
        if (change == 1) cache["rows"][0]["extra"] = true;
        if (change == 2) cache["rows"][1]["index"] = 0;
        if (change == 3) std::swap(cache["rows"][0], cache["rows"][1]);
        Reject([&] { parse(packet); });
    }
    for (const std::string fragment : {"\"revision\":7", "\"index\":0", "\"value\":\"ON\""}) {
        auto bytes = Packet().dump(); const auto offset = bytes.find(fragment); Check(offset != std::string::npos, "Duplicate fixture is incomplete");
        bytes.insert(offset, fragment + ","); Reject([&] { runtime::ParseState(bytes, GetCurrentProcessId()); });
    }
    auto bytes = Packet().dump(); const auto offset = bytes.find("\"revision\":7");
    bytes.replace(offset, 12, "\"revision\":18446744073709551616");
    Reject([&] { runtime::ParseState(bytes, GetCurrentProcessId()); });
}
void RejectsInvalidPaintTargetsAndCompetingModalContexts() {
    const auto parse = [](const Json& packet) { runtime::ParseState(packet.dump(), GetCurrentProcessId()); };
    for (int change = 0; change < 7; ++change) {
        auto packet = Packet();
        if (change == 0) packet["isOpen"] = false;
        if (change == 1) packet.erase("menuRevision");
        if (change == 2) packet["rows"] = Json::array();
        if (change == 3) packet["menuBooleanPaint"]["rows"][1]["index"] = 2;
        if (change == 4) packet["rows"][0]["value"] = "UNAVAILABLE";
        if (change == 5) packet["rows"][1] = {{"label", ""}, {"value", ""}, {"enabled", false}, {"isSpacer", true}};
        if (change == 6) packet["selectedIndex"] = 2;
        Reject([&] { parse(packet); });
    }
    for (const char* field : {"menuUnlock", "menuPreferenceRefresh", "menuDesignTransition", "menuPreferenceRebuild", "menuOpenTransition"}) {
        auto packet = Packet(); packet[field] = {{"phase", "rebuild"}};
        if (std::string(field) == "menuPreferenceRefresh") packet[field]["phase"] = "pending";
        Reject([&] { parse(packet); });
    }
}
void ResolvesUnknownAndLegacyValuesWithoutGrantingActions() {
    auto state = *runtime::ParseState(Packet().dump(), GetCurrentProcessId()).snapshot;
    Check(ResolveRowDisplayValue(state, 0) == "ON" && ResolveRowDisplayValue(state, 1) == "UNKNOWN", "Current UNKNOWN did not override cached paint");
    state.rows[0].enabled = false;
    Check(ResolveRowDisplayValue(state, 0) == "ON" && !state.rows[0].enabled && state.rows[0].value == "OFF", "Cached paint modified action capability");
    state.menuBooleanPaint->rows[0].value = MenuBooleanPaintValue::Unknown;
    Check(ResolveRowDisplayValue(state, 0) == "UNKNOWN", "Cached unknown was fabricated as a confirmed current value");
    state.menuBooleanPaint.reset(); state.rows[0].value = "UNAVAILABLE";
    Check(ResolveRowDisplayValue(state, 0) == "UNAVAILABLE" && ResolveRowDisplayValue(state, 128).empty(), "Legacy or out-of-range resolution changed");
}
}
