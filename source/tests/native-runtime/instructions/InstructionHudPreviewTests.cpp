#include "InstructionHudTests.h"
#include "../TestSupport.h"
#include "../../native-layout/BackendPreview.h"
#include <fstream>
#include <random>
#include <limits>
namespace iw::overlay::tests::instructions {
namespace {
class Preview final {
public:
    Preview() {
        std::random_device random;
        for (unsigned attempt = 0; attempt < 32; ++attempt) {
            auto path = std::filesystem::temp_directory_path() / ("iw-instructions-preview-" + std::to_string(random()));
            if (std::filesystem::create_directory(path)) {
                directory_ = std::move(path); std::filesystem::permissions(directory_, std::filesystem::perms::owner_all); return;
            }
        }
        throw std::runtime_error("Could not reserve instruction preview directory");
    }
    ~Preview() { std::error_code ignored; std::filesystem::remove_all(directory_, ignored); }
    MenuSnapshot Read(const std::string& bytes) const {
        const auto path = directory_ / "state.json";
        { std::ofstream stream(path, std::ios::binary); stream << bytes; if (!stream) throw std::runtime_error("Could not write instruction preview"); }
        return ReadBackendPreview(path);
    }
private:
    std::filesystem::path directory_;
};
}
void InstructionHudPreviewMatchesStrictLifetimeAndAdmission() {
    Preview file;
    auto packet = StateJson();
    Check(!file.Read(packet.dump()).instructionHud, "Legacy preview invented footer ownership");
    packet["instructionHud"] = nullptr;
    Check(!file.Read(packet.dump()).instructionHud, "Null preview invented footer ownership");
    packet["localMenuEligibility"] = {{"revision", 7}, {"life", "dead"}, {"hasMenu", true},
        {"controlsDisabled", nullptr}, {"blocked", true}};
    packet["instructionHud"] = {{"exists", true}, {"creationRevision", std::numeric_limits<std::uint64_t>::max()},
        {"admissionRevision", 7}, {"creationDesign", "AIO"}, {"creationAlternate", true}, {"creationBackground", false}};
    for (bool open : {false, true}) {
        packet["isOpen"] = open;
        const auto preview = file.Read(packet.dump());
        const auto production = runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot;
        Check(preview.instructionHud && production->instructionHud
            && preview.instructionHud->creationRevision == production->instructionHud->creationRevision
            && preview.instructionHud->creationDesign == MenuDesign::Aio
            && preview.instructionHud->creationAlternate == true && preview.instructionHud->creationBackground == false
            && preview.instructionHud->instructions == production->instructionHud->instructions
            && preview.rows[0].value == "OFF", "Preview changed copied creation or raw row data");
    }
    for (const Json& revision : {Json(nullptr), Json(0), Json(-1), Json(true), Json(1.0), Json("2")}) {
        auto bad = packet; bad["instructionHud"]["creationRevision"] = revision;
        Reject([&] { file.Read(bad.dump()); });
    }
    auto hidden = packet;
    hidden["appearance"] = {{"booleanDisplay", "Boxes"}, {"boolBoxLocation", "Right"},
        {"instructionsVisible", false}, {"optionCounter", false}, {"coloredCursor", true},
        {"spotlightCursor", true}, {"largeCursor", false}};
    auto hiddenLegacy = hidden; hiddenLegacy.erase("instructionHud");
    Check(!file.Read(hiddenLegacy.dump()).appearance.instructionsVisible,
        "Valid hidden preview appearance failed before HUD validation");
    Reject([&] { file.Read(hidden.dump()); });
    auto bad = packet; bad["localMenuEligibility"]["life"] = "unknown"; Reject([&] { file.Read(bad.dump()); });
    bad = packet; bad["localMenuEligibility"]["revision"] = 8; Reject([&] { file.Read(bad.dump()); });
    bad = packet; bad["instructionHud"]["extra"] = true; Reject([&] { file.Read(bad.dump()); });
    auto bytes = packet.dump(); auto position = bytes.find("\"exists\":true");
    Check(position != std::string::npos, "Preview duplicate fixture lost exists");
    bytes.insert(position, "\"exi\\u0073ts\":true,");
    Check(bytes.find("\\u0073") != std::string::npos, "Preview fixture lost decoded escape");
    Reject([&] { file.Read(bytes); });
    bytes = packet.dump(); position = bytes.find("18446744073709551615");
    Check(position != std::string::npos, "Preview overflow fixture lost uint64 maximum");
    bytes.replace(position, 20, "18446744073709551616");
    Reject([&] { file.Read(bytes); });
    packet["instructionHud"] = {{"exists", false}, {"creationRevision", nullptr}, {"admissionRevision", nullptr},
        {"creationDesign", nullptr}, {"creationAlternate", nullptr}, {"creationBackground", nullptr}};
    Check(file.Read(packet.dump()).instructionHud && !file.Read(packet.dump()).instructionHud->exists,
        "Owned absent preview became legacy");
    for (const auto* key : {"creationRevision", "admissionRevision", "creationDesign", "creationAlternate", "creationBackground"}) {
        bad = packet; bad["instructionHud"][key] = true; Reject([&] { file.Read(bad.dump()); });
    }
}
}
