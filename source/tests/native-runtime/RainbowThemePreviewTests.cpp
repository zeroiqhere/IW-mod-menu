#include "TestSupport.h"
#include "../native-layout/BackendPreview.h"
#include <array>
#include <filesystem>
#include <fstream>
#include <random>
#include <stdexcept>
#include <string>
#include <system_error>
#include <utility>

namespace iw::overlay::tests {
namespace {
class OwnedPreviewFile {
public:
    OwnedPreviewFile() {
        std::random_device random;
        // Reserve a private test directory without reusing an existing path
        for (int attempt = 0; attempt < 32; ++attempt) {
            auto candidate = std::filesystem::temp_directory_path()
                / ("iw-rainbow-preview-" + std::to_string(random()) + "-" + std::to_string(random()));
            if (std::filesystem::create_directory(candidate)) {
                directory_ = std::move(candidate);
                std::error_code error;
                std::filesystem::permissions(directory_, std::filesystem::perms::owner_all, error);
                if (error) {
                    std::error_code ignored;
                    std::filesystem::remove_all(directory_, ignored);
                    throw std::runtime_error("Could not protect the rainbow preview fixture directory");
                }
                return;
            }
        }
        throw std::runtime_error("Could not reserve the rainbow preview fixture directory");
    }

    // One object owns cleanup so copying cannot remove another fixture's active path
    OwnedPreviewFile(const OwnedPreviewFile&) = delete;
    OwnedPreviewFile& operator=(const OwnedPreviewFile&) = delete;

    ~OwnedPreviewFile() {
        // Cleanup cannot replace a regression failure with a destructor exception
        std::error_code ignored;
        std::filesystem::remove_all(directory_, ignored);
    }

    MenuSnapshot Read(const Json& packet) const {
        return ReadBytes(packet.dump());
    }

    MenuSnapshot ReadBytes(const std::string& bytes) const {
        const auto path = directory_ / "state.json";
        {
            std::ofstream output(path, std::ios::binary | std::ios::trunc);
            output << bytes;
            if (!output) throw std::runtime_error("Could not write the rainbow preview fixture");
        }
        return ReadBackendPreview(path);
    }
private:
    std::filesystem::path directory_;
};

}

void RainbowPreviewPreservesLegacyModeAndMatchesOwnedRuntimeState() {
    OwnedPreviewFile fixture;
    auto source = StateJson();
    Check(!fixture.Read(source).smoothRainbowTheme, "A legacy preview enabled rainbow mode");
    for (bool open : {false, true}) {
        for (bool enabled : {false, true}) {
            source["isOpen"] = open;
            source["smoothRainbowTheme"] = enabled;
            source["navigationRevision"] = 47;
            const auto preview = fixture.Read(source);
            const auto parsed = runtime::ParseState(source.dump(), GetCurrentProcessId()).snapshot;
            // Preview and runtime consume the actual file shape without changing feature state
            Check(preview.smoothRainbowTheme == enabled && preview.smoothRainbowTheme == parsed->smoothRainbowTheme,
                "The preview ignored the committed rainbow mode");
            Check(preview.accent == parsed->accent && preview.navigationRevision == 47
                && preview.rows[0].value == "OFF", "Preview theme mode changed unrelated state");
            source["smoothRainbowTheme"] = !enabled;
            Check(preview.smoothRainbowTheme == enabled, "The preview retained mutable theme JSON");
        }
    }
}

void RainbowPreviewRejectsMalformedModesDuplicatesAndInvalidAccent() {
    OwnedPreviewFile fixture;
    const std::array<Json, 8> invalid{nullptr, "true", "false", 0, 1, 1.0, Json::array(), Json::object()};
    for (bool open : {false, true}) {
        for (const auto& value : invalid) {
            auto source = StateJson(); source["isOpen"] = open; source["smoothRainbowTheme"] = value;
            Reject([&] { fixture.Read(source); });
        }
    }
    // Duplicate checks compare decoded names, including escaped aliases
    for (const auto* duplicate : {"smoothRainbowTheme", "smoothRainbow\\u0054heme"}) {
        std::string bytes = StateJson().dump();
        bytes.insert(1, std::string("\"smoothRainbowTheme\":true,\"") + duplicate + "\":false,");
        Reject([&] { fixture.ReadBytes(bytes); });
    }
    for (bool enabled : {false, true}) {
        auto source = StateJson(); source["smoothRainbowTheme"] = enabled;
        // Animation cannot conceal a malformed required fixed-color fallback
        for (const Json& accent : {Json(nullptr), Json(true), Json(123), Json("3998FE"),
            Json("#3998F"), Json("#3998FE00"), Json("#zzzzzz")}) {
            source["accent"] = accent;
            Reject([&] { fixture.Read(source); });
        }
        source.erase("accent");
        Reject([&] { fixture.Read(source); });
    }
}
}
