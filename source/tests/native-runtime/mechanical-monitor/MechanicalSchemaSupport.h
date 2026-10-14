#pragma once
#include "../TestSupport.h"
#include "../../native-layout/BackendPreview.h"
#include <chrono>
#include <fstream>

namespace iw::overlay::tests::mechanical {
inline Json Packet(const char* kind = "choice") {
    auto value = StateJson();
    value["isOpen"] = true;
    value["isRootMenu"] = false;
    value["lastSequence"] = 10;
    value["menuSurface"] = "main";
    value["navigationRevision"] = 17;
    value["menuRevision"] = 21;
    value["localMenuEligibility"] = {{"revision", 4}, {"life", "alive"}, {"hasMenu", true},
        {"controlsDisabled", false}, {"blocked", false}};
    value["menuMonitorContinuation"] = {{"revision", 1}, {"sequence", 10}, {"admissionRevision", 4}, {"kind", kind}};
    return value;
}

// Each preview consumes the exact same serialized packet as the production reader
class PreviewFile {
public:
    explicit PreviewFile(const std::string& bytes) : path(std::filesystem::temp_directory_path() /
        ("iw-mechanical-schema-" + std::to_string(GetCurrentProcessId()) + "-"
            + std::to_string(std::chrono::steady_clock::now().time_since_epoch().count()) + ".json")) {
        std::ofstream output(path);
        output << bytes;
        Check(static_cast<bool>(output), "Mechanical preview fixture could not be written");
    }
    ~PreviewFile() { std::error_code error; std::filesystem::remove(path, error); }
    std::filesystem::path path;
};
inline auto Both(const Json& value) {
    const auto bytes = value.dump();
    auto parsed = runtime::ParseState(bytes, GetCurrentProcessId());
    Check(parsed.snapshot != nullptr, "Production mechanical reader returned no snapshot");
    PreviewFile file(bytes);
    auto preview = ReadBackendPreview(file.path);
    Check(parsed.snapshot->menuMonitorContinuation == preview.menuMonitorContinuation,
        "Production and preview disagree about the mechanical receipt");
    Check(parsed.snapshot->rows.size() == value.at("rows").size() && preview.rows.size() == value.at("rows").size(),
        "Receipt parsing changed the raw row inventory");
    return parsed.snapshot;
}
inline void RejectBoth(const Json& value) {
    const auto bytes = value.dump();
    Reject([&] { runtime::ParseState(bytes, GetCurrentProcessId()); });
    PreviewFile file(bytes);
    Reject([&] { ReadBackendPreview(file.path); });
}
inline void RejectBytes(const std::string& bytes) {
    Reject([&] { runtime::ParseState(bytes, GetCurrentProcessId()); });
    PreviewFile file(bytes);
    Reject([&] { ReadBackendPreview(file.path); });
}
void LegacyAbsenceKeepsTheCurrentReceiptFreeSnapshot();
void AllMechanicalKindsKeepWaitingReadyAndUnsignedBounds();
void ClosedBackKeepsRetainedRootAndChildWithoutGrantingOpeningAuthority();
void ExactReceiptShapesRejectWrongTypesMissingAndExtraFields();
void ReceiptRequiresCurrentKnownAdmissionAndOrdinaryChoice();
void WaitingReceiptRequiresItsExactWaitAndAcknowledgment();
void ValidCompetingPhasesCannotShareMechanicalContinuation();
void DecodedDuplicatesAndUnsignedOverflowRejectInBothReaders();
void LegacyCommandsRemainByteExactWhenMenuRevisionIsOmitted();
void MechanicalCommandsSerializeAllThreeCapturedEpochs();
void SuppliedMenuRevisionRequiresNavigationAndPositiveAdmission();
void MenuRevisionCannotBeAttachedToUnrelatedCommands();
}
