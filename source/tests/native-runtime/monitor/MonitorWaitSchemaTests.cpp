#include "MonitorWaitSchemaTests.h"
#include "../TestSupport.h"
#include "../../native-layout/BackendPreview.h"
#include <chrono>
#include <fstream>
#include <limits>

namespace iw::overlay::tests::monitor {
namespace {
Json Packet(const char* kind = "choice", bool open = true) {
    auto packet = StateJson(); packet["isOpen"] = open; packet["isRootMenu"] = !open;
    packet["menuSurface"] = "main";
    packet["navigationRevision"] = 17; packet["menuRevision"] = 21;
    packet["localMenuEligibility"] = {{"revision", 4}, {"life", "alive"}, {"hasMenu", true},
        {"controlsDisabled", false}, {"blocked", false}};
    packet["menuMonitorWait"] = {{"revision", 1}, {"kind", kind}};
    return packet;
}
auto Parse(const Json& packet) { return runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot; }
struct PreviewFile {
    std::filesystem::path path = std::filesystem::temp_directory_path() /
        ("iw-monitor-wait-" + std::to_string(GetCurrentProcessId()) + "-"
            + std::to_string(std::chrono::steady_clock::now().time_since_epoch().count()) + ".json");
    explicit PreviewFile(const Json& packet) {
        std::ofstream stream(path); stream << packet.dump();
        Check(static_cast<bool>(stream), "Preview fixture could not be written");
    }
    ~PreviewFile() { std::error_code error; std::filesystem::remove(path, error); }
};
}
void LegacyPacketsKeepOrdinaryInputWithoutAWait() {
    auto packet = Packet(); packet.erase("menuMonitorWait");
    Check(!Parse(packet)->menuMonitorWait, "An absent legacy field invented a monitor wait");
    packet["menuMonitorWait"] = nullptr;
    Check(!Parse(packet)->menuMonitorWait, "A null legacy field invented a monitor wait");
}
void ChoiceWaitCopiesPositiveBoundsWithoutInventingAModalOrEpoch() {
    for (auto revision : {std::uint64_t{1}, std::numeric_limits<std::uint64_t>::max()}) {
        auto packet = Packet(); packet["menuMonitorWait"]["revision"] = revision;
        const auto snapshot = Parse(packet);
        Check(snapshot->menuMonitorWait && snapshot->menuMonitorWait->revision == revision
            && snapshot->menuMonitorWait->kind == MenuMonitorWaitKind::Choice, "Choice ownership was lost or narrowed");
        Check(runtime::InputModeForSnapshot(*snapshot) == runtime::InputMode::Menu,
            "Mechanical choice wait invented a modal reseed that would lose the held repeat");
        Check(snapshot->navigationRevision == 17 && snapshot->menuRevision == 21
            && snapshot->rows[0].value == "OFF", "A wait invented navigation or feature state");
    }
}
void BackWaitOwnsOpenParentsAndClosedRootsInBothFamilies() {
    for (const char* surface : {"main", "quick"}) for (bool open : {false, true}) {
        auto packet = Packet("back", open); packet["menuSurface"] = surface;
        if (!open) packet["rows"] = Json::array();
        const auto snapshot = Parse(packet);
        Check(snapshot->menuMonitorWait && snapshot->menuMonitorWait->kind == MenuMonitorWaitKind::Back,
            "Back pause was discarded by its own parent or closed-root result");
        Check(snapshot->isOpen == open, "Back wait reopened a closed source menu");
    }
}
void InvalidMechanicalWaitShapesAreRejected() {
    for (const auto& invalid : {Json(false), Json(1), Json("choice"), Json::array(), Json::object(),
        Json{{"revision", 0}, {"kind", "choice"}}, Json{{"revision", -1}, {"kind", "back"}},
        Json{{"revision", 1.5}, {"kind", "choice"}}, Json{{"revision", "1"}, {"kind", "choice"}},
        Json{{"revision", 1}, {"kind", "Choice"}}, Json{{"revision", 1}, {"kind", true}},
        Json{{"revision", 1}, {"kind", "choice"}, {"elapsedMilliseconds", 180}}}) {
        auto packet = Packet(); packet["menuMonitorWait"] = invalid;
        Reject([&] { Parse(packet); });
    }
    auto closed = Packet("choice", false); Reject([&] { Parse(closed); });
    auto empty = Packet(); empty["rows"] = Json::array(); Reject([&] { Parse(empty); });
}
void MechanicalWaitCannotBorrowUnknownLocalAdmission() {
    for (const char* field : {"hasMenu", "life"}) {
        auto packet = Packet(); packet["localMenuEligibility"][field] = field == std::string("life") ? Json("unknown") : Json(nullptr);
        Reject([&] { Parse(packet); });
    }
    auto absent = Packet(); absent.erase("localMenuEligibility"); Reject([&] { Parse(absent); });
}
void MechanicalWaitRejectsEveryCompetingCallbackAndModal() {
    // Each conflict is otherwise a real supported packet, so rejection must come from its wait
    for (const auto& [field, descriptor] : {
        std::pair{"menuUnlock", Json("rebuild")},
        std::pair{"menuOptionCallback", Json{{"revision", 1}, {"kind", "boolean"}, {"phase", "pending"}}},
        std::pair{"menuPreferenceRefresh", Json{{"phase", "pending"}}},
        std::pair{"menuPreferenceRebuild", Json{{"phase", "rebuild"}}},
        std::pair{"menuDesignTransition", Json{{"phase", "cooldown"}}},
        std::pair{"menuOpenTransition", Json{{"phase", "cooldown"}}}}) {
        auto packet = Packet(); packet["isRootMenu"] = false; packet.erase("menuMonitorWait"); packet[field] = descriptor;
        Parse(packet);
        packet["menuMonitorWait"] = {{"revision", 1}, {"kind", "choice"}};
        Reject([&] { Parse(packet); });
    }
}
void DecodedDuplicateWaitFieldsCannotReplaceTheirOwner() {
    auto packet = Packet(); std::string bytes = packet.dump();
    const auto marker = bytes.find("\"menuMonitorWait\":"); Check(marker != std::string::npos, "Wait duplicate fixture lost its field");
    bytes.insert(marker, "\"menuMonitorWait\":null,");
    Reject([&] { runtime::ParseState(bytes, GetCurrentProcessId()); });
    bytes = packet.dump(); const auto kind = bytes.find("\"kind\":\"choice\"");
    Check(kind != std::string::npos, "Wait duplicate fixture lost its kind");
    bytes.insert(kind, "\"\\u006bind\":\"back\",");
    Reject([&] { runtime::ParseState(bytes, GetCurrentProcessId()); });
}
void PreviewAndRuntimeShareStrictWaitOwnership() {
    auto packet = Packet(); PreviewFile valid(packet);
    const auto runtime = Parse(packet); const auto preview = ReadBackendPreview(valid.path);
    Check(runtime->menuMonitorWait && preview.menuMonitorWait == runtime->menuMonitorWait,
        "Preview ignored or changed the mechanical wait owned by runtime");
    packet["menuMonitorWait"]["kind"] = "invalid"; PreviewFile invalid(packet);
    Reject([&] { ReadBackendPreview(invalid.path); });
}
}
