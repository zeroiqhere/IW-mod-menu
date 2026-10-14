#include "../TestSupport.h"
#include <limits>

namespace iw::overlay::tests {
namespace {
Json Eligibility() {
    return {{"revision", 1u}, {"life", "unknown"}, {"hasMenu", nullptr},
        {"controlsDisabled", nullptr}, {"blocked", false}};
}
auto ParseEligibility(const Json& packet) {
    return runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot;
}
}

void LocalEligibilityPreservesLegacyAndOwnsUnknownNullableFacts() {
    auto packet = StateJson();
    Check(!ParseEligibility(packet)->localMenuEligibility
        && ParseEligibility(packet)->menuSurface == MenuSurface::Main, "Legacy admission changed");
    packet["localMenuEligibility"] = Eligibility();
    const auto snapshot = ParseEligibility(packet);
    Check(snapshot->localMenuEligibility && snapshot->localMenuEligibility->revision == 1
        && snapshot->localMenuEligibility->life == PlayerLifeState::Unknown
        && !snapshot->localMenuEligibility->hasMenu && !snapshot->localMenuEligibility->controlsDisabled
        && !snapshot->localMenuEligibility->blocked, "Unknown facts were lost or converted into permission");
    // The copied descriptor owns its data independently from later packet edits
    packet["localMenuEligibility"]["hasMenu"] = true;
    Check(!snapshot->localMenuEligibility->hasMenu, "Published eligibility retained mutable JSON");
}

void LocalEligibilityOwnsAllLifeFactsAcrossOpenClosedAndUnsignedBounds() {
    auto packet = StateJson(); packet["localMenuEligibility"] = Eligibility();
    for (const bool open : {false, true}) for (const auto* life : {"unknown", "alive", "dead"})
        for (const Json& fact : {Json(nullptr), Json(false), Json(true)}) {
            packet["isOpen"] = open; packet["localMenuEligibility"]["life"] = life;
            packet["localMenuEligibility"]["hasMenu"] = fact;
            packet["localMenuEligibility"]["controlsDisabled"] = fact;
            packet["localMenuEligibility"]["blocked"] = true;
            packet["localMenuEligibility"]["revision"] = std::numeric_limits<std::uint64_t>::max();
            const auto snapshot = ParseEligibility(packet);
            Check(snapshot->localMenuEligibility.has_value(), "Supplied eligibility disappeared");
            const auto& actual = *snapshot->localMenuEligibility;
            Check(actual.revision == std::numeric_limits<std::uint64_t>::max() && actual.blocked
                && actual.hasMenu == (fact.is_null() ? std::nullopt : std::optional<bool>(fact.get<bool>()))
                && actual.controlsDisabled == actual.hasMenu,
                "Eligibility bounds or optional facts changed during publication");
            Check(actual.life == (std::string(life) == "alive" ? PlayerLifeState::Alive
                : std::string(life) == "dead" ? PlayerLifeState::Dead : PlayerLifeState::Unknown), "Life enum changed");
        }
}

void LocalEligibilityRejectsMalformedExactFieldsBeforeClosedPublication() {
    const auto valid = Eligibility();
    for (const Json& wrong : {Json(nullptr), Json(false), Json(1), Json("alive"), Json::array()}) {
        auto packet = StateJson(); packet["localMenuEligibility"] = wrong;
        Reject([&] { ParseEligibility(packet); });
    }
    // Required nullable fields may be null, but cannot be omitted or mistyped
    for (const auto* field : {"revision", "life", "hasMenu", "controlsDisabled", "blocked"}) {
        auto packet = StateJson(); packet["localMenuEligibility"] = valid;
        packet["localMenuEligibility"].erase(field); Reject([&] { ParseEligibility(packet); });
    }
    for (const auto* field : {"hasMenu", "controlsDisabled", "blocked"})
        for (const Json& wrong : {Json(0), Json(1), Json("false"), Json::array(), Json::object()}) {
            auto packet = StateJson(); packet["localMenuEligibility"] = valid;
            packet["localMenuEligibility"][field] = wrong; Reject([&] { ParseEligibility(packet); });
        }
    auto packet = StateJson(); packet["localMenuEligibility"] = valid;
    packet["localMenuEligibility"]["blocked"] = nullptr; Reject([&] { ParseEligibility(packet); });
    packet["localMenuEligibility"] = valid; packet["localMenuEligibility"]["account"] = 1;
    Reject([&] { ParseEligibility(packet); });
    for (const Json& wrong : {Json(nullptr), Json(false), Json(0), Json(-1), Json(1.5), Json("1")}) {
        packet["localMenuEligibility"] = valid; packet["localMenuEligibility"]["revision"] = wrong;
        Reject([&] { ParseEligibility(packet); });
    }
    for (const Json& wrong : {Json(nullptr), Json(true), Json(0), Json("Alive"), Json("spectator"), Json("")}) {
        packet["localMenuEligibility"] = valid; packet["localMenuEligibility"]["life"] = wrong;
        Reject([&] { ParseEligibility(packet); });
    }
}

void LocalEligibilityRejectsDecodedDuplicatesAndOverflowWithoutBorrowingContext() {
    auto packet = StateJson(); packet["context"] = "ALIVE AUTHORIZED";
    packet["localMenuEligibility"] = Eligibility();
    const auto inject = [&](const std::string& addition) {
        auto bytes = packet.dump(); const auto marker = bytes.find("\"revision\":1");
        Check(marker != std::string::npos, "Duplicate fixture has no revision");
        bytes.insert(marker, addition);
        Reject([&] { runtime::ParseState(bytes, GetCurrentProcessId()); });
    };
    inject("\"revision\":2,"); inject("\"revi\\u0073ion\":2,");
    auto bytes = packet.dump(); const auto marker = bytes.find("\"revision\":1");
    bytes.replace(marker, std::string("\"revision\":1").size(), "\"revision\":18446744073709551616");
    Reject([&] { runtime::ParseState(bytes, GetCurrentProcessId()); });
    const auto snapshot = ParseEligibility(packet);
    Check(snapshot->localMenuEligibility && snapshot->localMenuEligibility->life == PlayerLifeState::Unknown,
        "Context text fabricated a living actor");
}

void MenuSurfacePreservesLegacyAndOwnsBothFamiliesWithoutTitleInference() {
    auto packet = StateJson(); packet["title"] = "Quick Menu";
    Check(ParseEligibility(packet)->menuSurface == MenuSurface::Main, "Title changed legacy family");
    for (const bool open : {false, true}) for (const auto* surface : {"main", "quick"}) {
        packet["isOpen"] = open; packet["menuSurface"] = surface;
        Check(ParseEligibility(packet)->menuSurface == (std::string(surface) == "main" ? MenuSurface::Main : MenuSurface::Quick),
            "Explicit family identity changed");
    }
}

void MenuSurfaceRejectsWrongTypesNamesAndDecodedDuplicates() {
    auto packet = StateJson();
    for (const Json& wrong : {Json(nullptr), Json(false), Json(0), Json("Main"), Json("unknown"), Json::array()}) {
        packet["menuSurface"] = wrong; Reject([&] { ParseEligibility(packet); });
    }
    packet["menuSurface"] = "main";
    auto bytes = packet.dump(); bytes.insert(1, "\"menu\\u0053urface\":\"quick\",");
    Reject([&] { runtime::ParseState(bytes, GetCurrentProcessId()); });
}
} // namespace iw::overlay::tests
