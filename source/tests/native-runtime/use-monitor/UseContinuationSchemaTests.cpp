#include "../TestSupport.h"
#include "../../native-layout/BackendPreview.h"
#include <array>
#include <chrono>
#include <fstream>
#include <iostream>
#include <limits>

namespace iw::overlay::tests::useMonitor {
namespace {
Json Packet(const char* kind = "plain") {
    auto value = StateJson(); value["isOpen"] = true; value["lastSequence"] = 10; value["menuSurface"] = "main";
    value["navigationRevision"] = 17; value["menuRevision"] = 21;
    value["localMenuEligibility"] = {{"revision", 4}, {"life", "alive"}, {"hasMenu", true},
        {"controlsDisabled", false}, {"blocked", false}};
    value["menuUseContinuation"] = {{"revision", 1}, {"sequence", 10}, {"admissionRevision", 4}, {"kind", kind}};
    return value;
}
struct PreviewFile {
    std::filesystem::path path = std::filesystem::temp_directory_path() /
        ("iw-use-continuation-" + std::to_string(GetCurrentProcessId()) + "-"
            + std::to_string(std::chrono::steady_clock::now().time_since_epoch().count()) + ".json");
    explicit PreviewFile(const std::string& bytes) {
        std::ofstream stream(path); stream << bytes;
        Check(static_cast<bool>(stream), "Use preview fixture could not be written");
    }
    ~PreviewFile() { std::error_code error; std::filesystem::remove(path, error); }
};
auto Both(const Json& value) {
    auto runtime = runtime::ParseState(value.dump(), GetCurrentProcessId()).snapshot;
    PreviewFile file{value.dump()}; auto preview = ReadBackendPreview(file.path);
    Check(runtime->menuUseContinuation == preview.menuUseContinuation, "Both readers disagree about causal Use ownership");
    return runtime;
}
void RejectBoth(const Json& value) {
    Reject([&] { runtime::ParseState(value.dump(), GetCurrentProcessId()); });
    PreviewFile file{value.dump()}; Reject([&] { ReadBackendPreview(file.path); });
}
void LegacyOmissionAndNullDoNotCreateRepeatAuthority() {
    auto value = Packet(); value.erase("menuUseContinuation");
    Check(!Both(value)->menuUseContinuation, "Legacy omission invented a causal Use receipt");
    value["menuUseContinuation"] = nullptr;
    Check(!Both(value)->menuUseContinuation, "Legacy null invented a causal Use receipt");
}
void ExactReceiptKeepsAllUnsignedBoundsAndPrivateFieldsAbsent() {
    for (auto maximum : {std::uint64_t{1}, std::numeric_limits<std::uint64_t>::max()}) {
        auto value = Packet(); value["lastSequence"] = maximum; value["localMenuEligibility"]["revision"] = maximum;
        value["menuUseContinuation"] = {{"revision", maximum}, {"sequence", maximum}, {"admissionRevision", maximum}, {"kind", "plain"}};
        Check(Both(value)->menuUseContinuation == MenuUseContinuation{maximum, maximum, maximum, MenuOptionCallbackKind::Plain},
            "Causal receipt was lost or narrowed before native input");
    }
}
void ReadyReceiptDoesNotInventControlsOrAModalInputLock() {
    auto value = Packet("boolean"); value["localMenuEligibility"]["controlsDisabled"] = true;
    value["menuOptionCallback"] = {{"revision", 1}, {"kind", "boolean"}, {"phase", "pending"}};
    Check(runtime::InputModeForSnapshot(*Both(value)) == runtime::InputMode::MenuLocked, "Actual callback failed to own its pause");
    value.erase("menuOptionCallback"); value["localMenuEligibility"]["controlsDisabled"] = false;
    auto ready = Both(value);
    Check(ready->menuUseContinuation && ready->localMenuEligibility->controlsDisabled == false
        && runtime::InputModeForSnapshot(*ready) == runtime::InputMode::Menu, "A ready receipt invented disabled controls or another wait");
}
void InvalidReceiptShapesCannotSupplyAnInvocation() {
    for (const auto& invalid : {Json(false), Json(1), Json("plain"), Json::array(), Json::object(),
        Json{{"revision", 0}, {"sequence", 10}, {"admissionRevision", 4}, {"kind", "plain"}},
        Json{{"revision", 1}, {"sequence", -1}, {"admissionRevision", 4}, {"kind", "plain"}},
        Json{{"revision", 1}, {"sequence", "10"}, {"admissionRevision", 4}, {"kind", "plain"}},
        Json{{"revision", 1}, {"sequence", 10}, {"admissionRevision", 4.5}, {"kind", "plain"}},
        Json{{"revision", 1}, {"sequence", 10}, {"admissionRevision", 4}, {"kind", "Plain"}},
        Json{{"revision", 1}, {"sequence", 10}, {"admissionRevision", 4}, {"kind", "plain"}, {"permission", true}}}) {
        auto value = Packet(); value["menuUseContinuation"] = invalid; RejectBoth(value);
    }
}
void ReceiptRequiresKnownOriginalAdmissionAndAnOrdinaryRow() {
    for (int change : {0, 1, 2, 3, 4, 5, 6}) {
        auto value = Packet();
        if (change == 0) value.erase("localMenuEligibility");
        else if (change == 1) value["localMenuEligibility"]["hasMenu"] = nullptr;
        else if (change == 2) value["localMenuEligibility"]["life"] = "unknown";
        else if (change == 3) value["isOpen"] = false;
        else if (change == 4) value["rows"] = Json::array();
        else if (change == 5) value["selectedIndex"] = 1;
        else { value["rows"][0]["label"] = ""; value["rows"][0]["value"] = ""; value["rows"][0]["enabled"] = false; value["rows"][0]["isSpacer"] = true; }
        RejectBoth(value);
    }
}
void ReceiptCannotBorrowAForeignAckAdmissionOrCallback() {
    for (int change : {0, 1, 2, 3}) {
        auto value = Packet();
        if (change == 0) value["menuUseContinuation"]["sequence"] = 11;
        else if (change == 1) value["menuUseContinuation"]["admissionRevision"] = 5;
        else value["menuOptionCallback"] = {{"revision", change == 2 ? 2 : 1},
            {"kind", change == 2 ? "plain" : "boolean"}, {"phase", "cooldown"}};
        RejectBoth(value);
    }
}
void ChangedDesignUseRequiresItsExactPlainCooldownInvocation() {
    for (const char* transitionPhase : {"rebuild", "cooldown"}) {
        auto value = Packet(); value["isRootMenu"] = false;
        value["menuDesignTransition"] = {{"phase", transitionPhase}};
        value["menuOptionCallback"] = {{"revision", 1}, {"kind", "plain"}, {"phase", "cooldown"}};
        const auto accepted = Both(value);
        Check(accepted->menuUseContinuation == MenuUseContinuation{1, 10, 4, MenuOptionCallbackKind::Plain}
            && runtime::InputModeForSnapshot(*accepted) == runtime::InputMode::MenuLocked,
            "A changed-design plain callback lost its exact Use receipt or transition lock");

        auto missingReceipt = value; missingReceipt.erase("menuUseContinuation"); RejectBoth(missingReceipt);
        auto missingCallback = value; missingCallback.erase("menuOptionCallback"); RejectBoth(missingCallback);

        auto mismatchedRevision = value; mismatchedRevision["menuOptionCallback"]["revision"] = 2; RejectBoth(mismatchedRevision);
        auto booleanPair = value;
        booleanPair["menuOptionCallback"] = {{"revision", 1}, {"kind", "boolean"}, {"phase", "cooldown"}};
        booleanPair["menuUseContinuation"]["kind"] = "boolean"; RejectBoth(booleanPair);
        auto booleanCallback = value;
        booleanCallback["menuOptionCallback"] = {{"revision", 1}, {"kind", "boolean"}, {"phase", "cooldown"}};
        RejectBoth(booleanCallback);

        auto wrongAdmission = value; wrongAdmission["menuUseContinuation"]["admissionRevision"] = 5; RejectBoth(wrongAdmission);
        auto missingAdmission = value; missingAdmission["localMenuEligibility"] = nullptr; RejectBoth(missingAdmission);
        auto futureReceipt = value; futureReceipt["menuUseContinuation"]["sequence"] = 11; RejectBoth(futureReceipt);
    }
}
void CompetingSupportedPhasesCannotKeepAnOldUseReceipt() {
    for (const auto& [field, descriptor] : {
        std::pair{"menuUnlock", Json("rebuild")},
        std::pair{"menuPreferenceRefresh", Json{{"phase", "pending"}}},
        std::pair{"menuPreferenceRebuild", Json{{"phase", "rebuild"}}},
        std::pair{"menuOpenTransition", Json{{"phase", "cooldown"}}},
        std::pair{"menuMonitorWait", Json{{"revision", 1}, {"kind", "choice"}}}}) {
        auto value = Packet(); value["isRootMenu"] = false; value.erase("menuUseContinuation"); value[field] = descriptor;
        Both(value);
        value["menuUseContinuation"] = Packet()["menuUseContinuation"]; RejectBoth(value);
    }
    auto design = Packet(); design["isRootMenu"] = false; design.erase("menuUseContinuation");
    design["menuDesignTransition"] = {{"phase", "cooldown"}};
    Both(design);
    design["menuUseContinuation"] = Packet()["menuUseContinuation"]; RejectBoth(design);
}
void DuplicateDecodedReceiptFieldsAreRejectedByBothReaders() {
    auto bytes = Packet().dump(); auto offset = bytes.find("\"menuUseContinuation\":");
    Check(offset != std::string::npos, "Duplicate Use fixture lost its descriptor");
    bytes.insert(offset, "\"menuUseContinuation\":null,");
    Reject([&] { runtime::ParseState(bytes, GetCurrentProcessId()); });
    PreviewFile first{bytes}; Reject([&] { ReadBackendPreview(first.path); });
    bytes = Packet().dump(); offset = bytes.find("\"kind\":\"plain\"");
    Check(offset != std::string::npos, "Duplicate Use fixture lost its kind");
    bytes.insert(offset, "\"\\u006bind\":\"boolean\",");
    Reject([&] { runtime::ParseState(bytes, GetCurrentProcessId()); });
    PreviewFile second{bytes}; Reject([&] { ReadBackendPreview(second.path); });
}
}
}
int main() {
    using namespace iw::overlay::tests::useMonitor;
    struct Test { const char* name; void (*run)(); };
    const std::array tests{
        Test{"LegacyOmissionAndNullDoNotCreateRepeatAuthority", LegacyOmissionAndNullDoNotCreateRepeatAuthority},
        Test{"ExactReceiptKeepsAllUnsignedBoundsAndPrivateFieldsAbsent", ExactReceiptKeepsAllUnsignedBoundsAndPrivateFieldsAbsent},
        Test{"ReadyReceiptDoesNotInventControlsOrAModalInputLock", ReadyReceiptDoesNotInventControlsOrAModalInputLock},
        Test{"InvalidReceiptShapesCannotSupplyAnInvocation", InvalidReceiptShapesCannotSupplyAnInvocation},
        Test{"ReceiptRequiresKnownOriginalAdmissionAndAnOrdinaryRow", ReceiptRequiresKnownOriginalAdmissionAndAnOrdinaryRow},
        Test{"ReceiptCannotBorrowAForeignAckAdmissionOrCallback", ReceiptCannotBorrowAForeignAckAdmissionOrCallback},
        Test{"ChangedDesignUseRequiresItsExactPlainCooldownInvocation", ChangedDesignUseRequiresItsExactPlainCooldownInvocation},
        Test{"CompetingSupportedPhasesCannotKeepAnOldUseReceipt", CompetingSupportedPhasesCannotKeepAnOldUseReceipt},
        Test{"DuplicateDecodedReceiptFieldsAreRejectedByBothReaders", DuplicateDecodedReceiptFieldsAreRejectedByBothReaders}};
    int failures = 0;
    for (const auto& test : tests) {
        try { test.run(); std::cout << "PASS " << test.name << '\n'; }
        catch (const std::exception& error) { ++failures; std::cout << "FAIL " << test.name << ": " << error.what() << '\n'; }
    }
    std::cout << tests.size() - failures << '/' << tests.size() << " Use continuation schema checks passed\n";
    return failures ? 1 : 0;
}
