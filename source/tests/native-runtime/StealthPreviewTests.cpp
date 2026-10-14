#include "../native-layout/BackendPreview.h"
#include "TestSupport.h"
#include <fstream>
#include <random>

namespace iw::overlay::tests {
namespace {
class OwnedPreviewFile {
  public:
    OwnedPreviewFile() {
        std::random_device random;
        // Reserve a private test directory without reusing an existing path
        for (int attempt = 0; attempt < 32; ++attempt) {
            auto candidate =
                std::filesystem::temp_directory_path() /
                ("iw-stealth-preview-" + std::to_string(random()) + "-" + std::to_string(random()));
            if (std::filesystem::create_directory(candidate)) {
                directory_ = std::move(candidate);
                std::error_code error;
                std::filesystem::permissions(directory_, std::filesystem::perms::owner_all, error);
                if (error) {
                    std::error_code ignored;
                    std::filesystem::remove_all(directory_, ignored);
                    throw std::runtime_error("Could not protect the Stealth preview fixture directory");
                }
                return;
            }
        }
        throw std::runtime_error("Could not reserve the Stealth preview fixture directory");
    }

    // One object owns cleanup so copying cannot remove another fixture's active path
    OwnedPreviewFile(const OwnedPreviewFile &) = delete;
    OwnedPreviewFile &operator=(const OwnedPreviewFile &) = delete;

    ~OwnedPreviewFile() {
        // Cleanup cannot replace a regression failure with a destructor exception
        std::error_code ignored;
        std::filesystem::remove_all(directory_, ignored);
    }

    MenuSnapshot Read(const Json &packet) const {
        return ReadBytes(packet.dump());
    }

    MenuSnapshot ReadBytes(const std::string &bytes) const {
        const auto path = directory_ / "state.json";
        {
            std::ofstream output(path, std::ios::binary | std::ios::trunc);
            output << bytes;
            if (!output)
                throw std::runtime_error("Could not write the Stealth preview fixture");
        }
        return ReadBackendPreview(path);
    }

  private:
    std::filesystem::path directory_;
};

Json Packet() {
    auto packet = StateJson();
    packet["isOpen"] = true;
    packet["isRootMenu"] = false;
    packet["appearance"] = {{"booleanDisplay", "Text"},    {"boolBoxLocation", "Right"},
                            {"instructionsVisible", true}, {"optionCounter", false},
                            {"coloredCursor", true},       {"spotlightCursor", true},
                            {"largeCursor", false},        {"stealthUI", true}};
    return packet;
}
} // namespace
void StealthPreviewOwnsLegacyAppearanceAndBothRebuildPhases() {
    OwnedPreviewFile fixture;
    for (int phase = 0; phase < 3; ++phase) {
        auto packet = Packet();
        if (phase)
            packet["menuPreferenceRebuild"] = {{"phase", phase == 1 ? "rebuild" : "cooldown"}};
        const auto production = runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot;
        const auto preview = fixture.Read(packet);
        Check(preview.appearance == production->appearance &&
                  preview.menuPreferenceRebuild == production->menuPreferenceRebuild &&
                  preview.rows.size() == production->rows.size(),
              "Preview lost an owned Stealth layout or phase");
    }
    auto packet = Packet();
    packet["appearance"].erase("stealthUI");
    Check(!fixture.Read(packet).appearance.stealthUI, "Legacy preview enabled Stealth");
}
void StealthPreviewRejectsMalformedFlagsPhasesAndInvalidContexts() {
    OwnedPreviewFile fixture;
    for (int invalid = 0; invalid < 12; ++invalid) {
        auto packet = Packet();
        if (invalid == 0)
            packet["appearance"]["stealthUI"] = nullptr;
        if (invalid == 1)
            packet["appearance"]["stealthUI"] = "true";
        if (invalid == 2)
            packet["appearance"]["stealthUI"] = 1;
        if (invalid == 3)
            packet["appearance"]["booleanDisplay"] = "Boxes";
        if (invalid == 4)
            packet["appearance"]["optionCounter"] = true;
        if (invalid == 5)
            packet["menuPreferenceRebuild"] = nullptr;
        if (invalid == 6)
            packet["menuPreferenceRebuild"] = {{"phase", "pending"}};
        if (invalid >= 7)
            packet["menuPreferenceRebuild"] = {{"phase", "rebuild"}};
        if (invalid == 7)
            packet["isRootMenu"] = true;
        if (invalid == 8)
            packet["rows"] = Json::array();
        if (invalid == 9)
            packet["menuPreferenceRefresh"] = {{"phase", "pending"}};
        if (invalid == 10)
            packet["menuOpenTransition"] = {{"phase", "cooldown"}};
        if (invalid == 11)
            packet["menuOpening"] = {{"revision", 1}, {"elapsedMilliseconds", 0}};
        Reject([&] { fixture.Read(packet); });
        Reject([&] { runtime::ParseState(packet.dump(), GetCurrentProcessId()); });
    }
}
void StealthPreviewRejectsDecodedDuplicatesAndBoundsCooldownOpening() {
    OwnedPreviewFile fixture;
    auto packet = Packet();
    packet["menuPreferenceRebuild"] = {{"phase", "cooldown"}};
    // The compact window owns a shorter fade even when the logical menu retains twelve rows
    packet["rows"] = Json::array();
    for (int row = 0; row < 12; ++row)
        packet["rows"].push_back({{"label", "Row"}, {"value", "OFF"}, {"enabled", true}});
    packet["menuOpening"] = {{"revision", 2}, {"elapsedMilliseconds", 399}};
    Check(fixture.Read(packet).menuOpening == MenuOpening{2, 399}, "Preview dropped a bounded redraw fade");
    packet["menuOpening"]["elapsedMilliseconds"] = 400;
    Reject([&] { fixture.Read(packet); });
    packet.erase("menuOpening");
    for (const auto *duplicate : {"\"phase\":\"rebuild\"", "\"\\u0070hase\":\"rebuild\""}) {
        auto bytes = packet.dump();
        const std::string field = "\"phase\":\"cooldown\"";
        bytes.replace(bytes.find(field), field.size(), field + "," + duplicate);
        Reject([&] { fixture.ReadBytes(bytes); });
    }
}
} // namespace iw::overlay::tests
