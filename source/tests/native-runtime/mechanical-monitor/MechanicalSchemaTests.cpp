#include "MechanicalSchemaSupport.h"
#include <limits>

namespace iw::overlay::tests::mechanical {
void LegacyAbsenceKeepsTheCurrentReceiptFreeSnapshot() {
    auto value = Packet();
    value.erase("menuMonitorContinuation");
    Check(!Both(value)->menuMonitorContinuation, "Legacy omission invented a mechanical receipt");
    value["menuMonitorContinuation"] = nullptr;
    Check(!Both(value)->menuMonitorContinuation, "Legacy null invented a mechanical receipt");
}
void AllMechanicalKindsKeepWaitingReadyAndUnsignedBounds() {
    for (const auto& [name, kind] : {std::pair{"choice", MenuMonitorWaitKind::Choice},
        std::pair{"choiceNoop", MenuMonitorWaitKind::ChoiceNoop}, std::pair{"back", MenuMonitorWaitKind::Back}}) {
        for (auto revision : {std::uint64_t{1}, std::numeric_limits<std::uint64_t>::max()}) {
            auto value = Packet(name);
            value["lastSequence"] = revision;
            value["localMenuEligibility"]["revision"] = revision;
            value["menuMonitorContinuation"] = {{"revision", revision}, {"sequence", revision},
                {"admissionRevision", revision}, {"kind", name}};
            const MenuMonitorContinuation expected{revision, revision, revision, kind};
            Check(Both(value)->menuMonitorContinuation == expected, "Ready receipt was ignored or narrowed");
            value["menuMonitorWait"] = {{"revision", revision}, {"kind", name}};
            Check(Both(value)->menuMonitorContinuation == expected, "Waiting receipt lost its matching invocation");
            Check(Both(value)->localMenuEligibility->controlsDisabled == false,
                "Mechanical receipt changed the observed control permission");
            // Dead Quick receipts remain causal facts even when the action gate is currently blocked
            value["menuSurface"] = "quick"; value["localMenuEligibility"]["life"] = "dead";
            value["localMenuEligibility"]["controlsDisabled"] = true; value["localMenuEligibility"]["blocked"] = true;
            Check(Both(value)->menuMonitorContinuation == expected, "Dead blocked Quick receipt lost its scope");
        }
    }
}
void ClosedBackKeepsRetainedRootAndChildWithoutGrantingOpeningAuthority() {
    auto value = Packet("back");
    value["isOpen"] = false;
    value["isRootMenu"] = true;
    value["rows"] = Json::array();
    auto closed = Both(value);
    Check(closed->menuMonitorContinuation && !closed->isOpen && closed->rows.empty(),
        "Closed root lost its retained Back receipt");
    Check(runtime::InputModeForSnapshot(*closed) == runtime::InputMode::Menu,
        "Ready closed receipt invented a modal or opening route");
    value["menuMonitorWait"] = {{"revision", 1}, {"kind", "back"}};
    Check(Both(value)->menuMonitorContinuation.has_value(), "Closed waiting root lost its Back identity");
    value.erase("menuMonitorWait");
    value["isRootMenu"] = false;
    Check(Both(value)->menuMonitorContinuation.has_value(), "Closed child lost the actual Close receipt");
    value["rows"] = Packet()["rows"];
    Check(Both(value)->menuMonitorContinuation.has_value(), "Closed child lost its retained ordinary rows");
}
void ExactReceiptShapesRejectWrongTypesMissingAndExtraFields() {
    for (const auto& shape : {Json(false), Json(1), Json("choice"), Json::array(), Json::object()}) {
        auto value = Packet(); value["menuMonitorContinuation"] = shape; RejectBoth(value);
    }
    for (const auto* field : {"revision", "sequence", "admissionRevision", "kind"}) {
        auto value = Packet(); value["menuMonitorContinuation"].erase(field); RejectBoth(value);
    }
    for (const auto* field : {"revision", "sequence", "admissionRevision"}) {
        for (const auto& invalid : {Json(0), Json(-1), Json(1.5), Json(true), Json("1"), Json(nullptr)}) {
            auto value = Packet(); value["menuMonitorContinuation"][field] = invalid; RejectBoth(value);
        }
    }
    for (const auto& invalid : {Json("Choice"), Json("plain"), Json("boolean"), Json(1), Json(nullptr)}) {
        auto value = Packet(); value["menuMonitorContinuation"]["kind"] = invalid; RejectBoth(value);
    }
    auto extra = Packet(); extra["menuMonitorContinuation"]["permission"] = true; RejectBoth(extra);
}
void ReceiptRequiresCurrentKnownAdmissionAndOrdinaryChoice() {
    for (int change : {0, 1, 2, 3, 4}) {
        auto value = Packet();
        if (change == 0) value.erase("localMenuEligibility");
        else if (change == 1) value["localMenuEligibility"]["hasMenu"] = nullptr;
        else if (change == 2) value["localMenuEligibility"]["life"] = "unknown";
        else if (change == 3) value["isOpen"] = false;
        else if (change == 4) value["rows"] = Json::array();
        // Each control remains valid without the receipt, so the rejection reaches its own scope gate
        auto control = value; control.erase("menuMonitorContinuation"); Both(control);
        RejectBoth(value);
    }
    // Cursor and spacer failures also remain rejected at the existing row boundary
    auto cursor = Packet(); cursor["selectedIndex"] = 1; RejectBoth(cursor);
    auto spacer = Packet(); spacer["rows"][0] = {{"label", ""}, {"value", ""}, {"enabled", false}, {"isSpacer", true}};
    RejectBoth(spacer);
}
void WaitingReceiptRequiresItsExactWaitAndAcknowledgment() {
    for (int change : {0, 1, 2, 3}) {
        auto value = Packet();
        if (change == 0) value["menuMonitorContinuation"]["sequence"] = 11;
        else if (change == 1) value["menuMonitorContinuation"]["admissionRevision"] = 5;
        else value["menuMonitorWait"] = {{"revision", change == 2 ? 2 : 1},
            {"kind", change == 2 ? "choice" : "choiceNoop"}};
        auto control = value; control.erase("menuMonitorContinuation"); Both(control);
        RejectBoth(value);
    }
}
void ValidCompetingPhasesCannotShareMechanicalContinuation() {
    for (const auto& [field, descriptor] : {
        std::pair{"menuUnlock", Json("rebuild")},
        std::pair{"menuPreferenceRefresh", Json{{"phase", "pending"}}},
        std::pair{"menuPreferenceRebuild", Json{{"phase", "rebuild"}}},
        std::pair{"menuDesignTransition", Json{{"phase", "cooldown"}}},
        std::pair{"menuOpenTransition", Json{{"phase", "cooldown"}}},
        std::pair{"menuOptionCallback", Json{{"revision", 1}, {"kind", "plain"}, {"phase", "cooldown"}}},
        std::pair{"menuUseContinuation", Json{{"revision", 1}, {"sequence", 10}, {"admissionRevision", 4}, {"kind", "plain"}}}}) {
        auto value = Packet(); value.erase("menuMonitorContinuation"); value[field] = descriptor;
        Both(value);
        value["menuMonitorContinuation"] = Packet()["menuMonitorContinuation"]; RejectBoth(value);
    }
    for (const auto& [field, descriptor] : {
        std::pair{"widthEditor", Json{{"step", 1}}},
        std::pair{"instructionPositionEditor", Json{{"phase", "editing"}}},
        std::pair{"menuPositionEditor", Json{{"step", 1}}},
        std::pair{"menuCredits", Json{{"elapsedMilliseconds", 0}}}}) {
        auto value = Packet("back"); value.erase("menuMonitorContinuation");
        value["title"] = "Menu Customization"; value["rows"] = Json::array(); value[field] = descriptor;
        // Empty Back results are otherwise supported, so this reaches the competing editor gate
        Both(value);
        value["menuMonitorContinuation"] = Packet("back")["menuMonitorContinuation"]; RejectBoth(value);
    }
}
void DecodedDuplicatesAndUnsignedOverflowRejectInBothReaders() {
    auto bytes = Packet().dump();
    auto offset = bytes.find("\"menuMonitorContinuation\":");
    Check(offset != std::string::npos, "Duplicate mechanical fixture lost its descriptor");
    bytes.insert(offset, "\"menuMonitorContinuation\":null,"); RejectBytes(bytes);
    bytes = Packet().dump(); offset = bytes.find("\"kind\":\"choice\"");
    Check(offset != std::string::npos, "Duplicate mechanical fixture lost its kind");
    const std::string escaped = "\"\\u006bind\":\"back\",";
    Check(escaped.find("\\u006b") != std::string::npos, "Duplicate fixture lost the literal JSON escape");
    bytes.insert(offset, escaped); RejectBytes(bytes);
    bytes = Packet().dump(); offset = bytes.find("\"sequence\":10");
    Check(offset != std::string::npos, "Overflow mechanical fixture lost its sequence");
    bytes.replace(offset, std::string("\"sequence\":10").size(), "\"sequence\":18446744073709551616");
    RejectBytes(bytes);
}
}
