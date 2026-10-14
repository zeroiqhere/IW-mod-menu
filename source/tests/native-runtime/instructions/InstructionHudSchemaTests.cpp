#include "InstructionHudTests.h"
#include "../TestSupport.h"
#include <limits>

namespace iw::overlay::tests::instructions {
namespace {
Json Owned(bool open = false) {
    auto packet = StateJson();
    packet["isOpen"] = open;
    packet["localMenuEligibility"] = {{"revision", 7}, {"life", "alive"},
        {"hasMenu", true}, {"controlsDisabled", false}, {"blocked", false}};
    packet["instructionHud"] = {{"exists", true}, {"creationRevision", 2},
        {"admissionRevision", 7}, {"creationDesign", "Apparition"},
        {"creationAlternate", false}, {"creationBackground", true}};
    return packet;
}
std::shared_ptr<const MenuSnapshot> Read(const Json& packet) {
    return runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot;
}
}
void InstructionHudOwnsLegacyExplicitAbsenceAndIndependentCreation() {
    auto legacy = StateJson();
    Check(!Read(legacy)->instructionHud, "Legacy invented independent footer ownership");
    legacy["instructionHud"] = nullptr;
    Check(!Read(legacy)->instructionHud, "Null legacy descriptor invented ownership");
    for (bool open : {false, true}) {
        auto packet = Owned(open);
        const auto state = Read(packet);
        Check(state->instructionHud && state->instructionHud->exists
            && state->instructionHud->creationRevision == 2 && state->instructionHud->admissionRevision == 7
            && state->isOpen == open && state->rows[0].value == "OFF" && !state->menuChromeRevision,
            "Independent footer was lost or changed menu data");
        packet["instructionHud"]["creationRevision"] = std::numeric_limits<std::uint64_t>::max();
        Check(Read(packet)->instructionHud->creationRevision == static_cast<std::uint64_t>(std::numeric_limits<std::uint64_t>::max()),
            "Positive unsigned boundary was rejected");
        packet["instructionHud"] = {{"exists", false}, {"creationRevision", nullptr}, {"admissionRevision", nullptr},
            {"creationDesign", nullptr}, {"creationAlternate", nullptr}, {"creationBackground", nullptr}};
        Check(Read(packet)->instructionHud && !Read(packet)->instructionHud->exists, "Owned absence became legacy");
        Check(state->instructionHud->creationRevision == 2, "Published HUD borrowed later JSON changes");
    }
}
void InstructionHudRejectsMalformedFieldsAndUnverifiedAdmission() {
    for (const Json& invalid : {Json(0), Json(-1), Json(true), Json(1.0), Json("2"), Json(nullptr)}) {
        for (const auto* key : {"creationRevision", "admissionRevision"}) {
            auto packet = Owned(); packet["instructionHud"][key] = invalid;
            Reject([&] { Read(packet); });
        }
    }
    for (const auto* key : {"exists", "creationAlternate", "creationBackground"}) {
        auto packet = Owned(); packet["instructionHud"][key] = "false";
        Reject([&] { Read(packet); });
    }
    for (const auto* key : {"creationRevision", "admissionRevision", "creationDesign", "creationAlternate", "creationBackground"}) {
        auto packet = Owned(); packet["instructionHud"].erase(key);
        Reject([&] { Read(packet); });
    }
    auto packet = Owned(); packet["instructionHud"]["extra"] = true; Reject([&] { Read(packet); });
    packet = Owned(); packet["instructionHud"]["creationDesign"] = "Aio"; Reject([&] { Read(packet); });
    packet = Owned(); packet["instructionHud"]["exists"] = false; Reject([&] { Read(packet); });
    for (const auto* life : {"unknown"}) {
        packet = Owned(); packet["localMenuEligibility"]["life"] = life; Reject([&] { Read(packet); });
    }
    for (const Json& grant : {Json(false), Json(nullptr)}) {
        packet = Owned(); packet["localMenuEligibility"]["hasMenu"] = grant; Reject([&] { Read(packet); });
    }
    packet = Owned(); packet.erase("localMenuEligibility"); Reject([&] { Read(packet); });
    packet = Owned(); packet["localMenuEligibility"]["revision"] = 8; Reject([&] { Read(packet); });
    packet = Owned(); packet["appearance"] = {{"booleanDisplay", "Boxes"}, {"boolBoxLocation", "Right"},
        {"instructionsVisible", false}, {"optionCounter", false}, {"coloredCursor", true},
        {"spotlightCursor", true}, {"largeCursor", false}};
    auto withoutHud = packet; withoutHud.erase("instructionHud");
    Check(!Read(withoutHud)->appearance.instructionsVisible, "Valid hidden legacy appearance was rejected before HUD scope");
    withoutHud["instructionHud"] = {{"exists", false}, {"creationRevision", nullptr}, {"admissionRevision", nullptr},
        {"creationDesign", nullptr}, {"creationAlternate", nullptr}, {"creationBackground", nullptr}};
    Check(Read(withoutHud)->instructionHud && !Read(withoutHud)->instructionHud->exists,
        "Explicit absence could not carry a valid hidden appearance");
    Reject([&] { Read(packet); });
    // The escaped name reaches the real decoded-duplicate guard
    packet = Owned(); auto bytes = packet.dump(); const auto index = bytes.find("\"exists\":true");
    Check(index != std::string::npos, "Duplicate fixture lost exists");
    bytes.insert(index, "\"exi\\u0073ts\":true,");
    Check(bytes.find("\\u0073") != std::string::npos, "Duplicate lost literal JSON escape");
    Reject([&] { runtime::ParseState(bytes, GetCurrentProcessId()); });
    bytes = packet.dump(); const auto revision = bytes.find("\"creationRevision\":2");
    Check(revision != std::string::npos, "Overflow fixture lost creation revision");
    bytes.replace(revision, std::string("\"creationRevision\":2").size(), "\"creationRevision\":18446744073709551616");
    Reject([&] { runtime::ParseState(bytes, GetCurrentProcessId()); });
}
void InstructionHudHintsUseExactSavedPoliciesAndDisabledDeadRoute() {
    auto packet = Owned();
    packet["mainOpeningControls"] = {{"revision", 1}, {"actions", {5, 4}}};
    packet["openingPolicies"] = {{"revision", 1}, {"main", packet["mainOpeningControls"]},
        {"quick", {{"revision", 1}, {"actions", {5, 10}}}}, {"quickDisabled", false}};
    runtime::OpeningActionLabels labels; labels[5] = "Mouse 4"; labels[4] = "E"; labels[10] = "G"; labels[12] = "Space";
    auto read = [&] { return runtime::ParseState(packet.dump(), GetCurrentProcessId(), nullptr, nullptr,
        nullptr, nullptr, nullptr, std::nullopt, nullptr, nullptr, &labels).snapshot; };
    auto state = read();
    Check(state->instructionHud && state->instructionHud->instructions.size() == 2
        && state->instructionHud->instructions[0] == "Mouse 4 + E: Open Main Menu"
        && state->instructionHud->instructions[1] == "Mouse 4 + G: Open Quick Menu", "Saved closed policies were not formatted exactly");
    packet["openingPolicies"]["quickDisabled"] = true;
    state = read();
    Check(state->instructionHud->instructions.size() == 1, "Living disabled Quick route was not omitted");
    packet["openingPolicies"]["quickDisabled"] = false;
    packet["localMenuEligibility"]["blocked"] = true;
    state = read();
    Check(state->instructionHud->instructions[0] == "Mouse 4 + E: Main Menu unavailable (controls paused)",
        "Blocked closed route was shown as admitted");
    packet["localMenuEligibility"]["blocked"] = false;
    packet["localMenuEligibility"]["life"] = "dead";
    packet["openingPolicies"]["quickDisabled"] = true;
    state = read();
    Check(state->instructionHud->instructions.size() == 1
        && state->instructionHud->instructions[0] == "Mouse 4 + Space: Quick Menu unavailable (disabled)",
        "Dead disabled Quick route was advertised as active");
    packet["isOpen"] = true; packet["menuSurface"] = "quick";
    labels[6] = "LMB"; labels[2] = "Left"; labels[3] = "Right"; labels[8] = "F";
    state = read();
    Check(state->instructionHud->instructions.size() == 4
        && state->instructionHud->instructions[0] == "LMB | Mouse 4: Scroll"
        && state->instructionHud->instructions[1] == "Left | Right: Previous / next choice"
        && state->instructionHud->instructions[2] == "F: Select"
        && state->instructionHud->instructions[3] == "Space: Back / close", "Dead Quick help used melee or general scroll aliases");
    packet["menuSurface"] = "main"; state = read();
    Check(state->instructionHud->instructions[3] == "Space: Exit unavailable (Main surface)", "Dead Main promised an unsupported Jump route");
    packet["isOpen"] = false;
    labels[12].clear(); state = read();
    Check(state->instructionHud->instructions[0] == "Quick Menu unavailable (saved Jump not bound)", "Missing jump invented a key");
}
}
