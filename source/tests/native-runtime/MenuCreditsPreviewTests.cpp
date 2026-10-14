#include "TestSupport.h"
#include "../native-layout/BackendPreview.h"
#include <array>
#include <fstream>
#include <random>
#include <limits>

namespace iw::overlay::tests {
namespace {
class OwnedPreviewFile {
public:
    OwnedPreviewFile() {
        std::random_device random;
        // Reserve a private test directory without reusing an existing path
        for (int attempt = 0; attempt < 32; ++attempt) {
            auto candidate = std::filesystem::temp_directory_path()
                / ("iw-menu-credits-preview-" + std::to_string(random()) + "-" + std::to_string(random()));
            if (std::filesystem::create_directory(candidate)) {
                directory_ = std::move(candidate);
                std::error_code error;
                std::filesystem::permissions(directory_, std::filesystem::perms::owner_all, error);
                if (error) {
                    std::error_code ignored;
                    std::filesystem::remove_all(directory_, ignored);
                    throw std::runtime_error("Could not protect the menu credits preview fixture directory");
                }
                return;
            }
        }
        throw std::runtime_error("Could not reserve the menu credits preview fixture directory");
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
            if (!output) throw std::runtime_error("Could not write the menu credits preview fixture");
        }
        return ReadBackendPreview(path);
    }
private:
    std::filesystem::path directory_;
};

Json PreviewAppearance() {
    return {{"booleanDisplay", "Boxes"}, {"boolBoxLocation", "Right"},
        {"instructionsVisible", true}, {"optionCounter", false}, {"coloredCursor", true},
        {"spotlightCursor", true}, {"largeCursor", false}};
}
}



void MenuCreditsPreviewMatchesRuntimeLegacyAndExactElapsedBoundaries() {
    OwnedPreviewFile fixture;
    auto source = StateJson();
    const auto compare = [&] {
        Check(fixture.Read(source).menuCredits == runtime::ParseState(source.dump(), GetCurrentProcessId()).snapshot->menuCredits,
            "CPU preview credits differ from the production parser");
    };
    compare(); source["menuCredits"] = nullptr; compare();
    source["isOpen"] = true; source["isRootMenu"] = false; source["rows"] = Json::array();
    for (int elapsed : {0, 500, 1000, 39999}) {
        source["menuCredits"] = {{"elapsedMilliseconds", elapsed}}; compare();
    }
    // Credits remain available when ordinary instruction decoration is hidden
    source["appearance"] = PreviewAppearance(); source["appearance"]["instructionsVisible"] = false; compare();
}
void MenuCreditsPreviewRejectsInvalidProgressDuplicatesAndConflictingContexts() {
    OwnedPreviewFile fixture;
    auto source = StateJson(); source["isOpen"] = true; source["isRootMenu"] = false; source["rows"] = Json::array();
    for (const Json& invalid : {Json(false), Json(1), Json("credits"), Json::array(), Json::object(),
        Json{{"elapsedMilliseconds", nullptr}}, Json{{"elapsedMilliseconds", true}}, Json{{"elapsedMilliseconds", "0"}},
        Json{{"elapsedMilliseconds", 0.0}}, Json{{"elapsedMilliseconds", -1}}, Json{{"elapsedMilliseconds", 40000}},
        Json{{"elapsedMilliseconds", std::numeric_limits<std::uint64_t>::max()}},
        Json{{"elapsedMilliseconds", 0}, {"instructions", Json::array({"injected"})}}}) {
        source["menuCredits"] = invalid; Reject([&] { fixture.Read(source); });
    }
    source["menuCredits"] = {{"elapsedMilliseconds", 0}};
    for (int conflict = 0; conflict < 8; ++conflict) {
        auto invalid = source;
        if (conflict == 0) invalid["isOpen"] = false;
        if (conflict == 1) invalid["isRootMenu"] = true;
        if (conflict == 2) invalid["rows"] = StateJson().at("rows");
        if (conflict == 3) invalid["selectedIndex"] = 1;
        if (conflict == 4) invalid["widthEditor"] = {{"step", 1}};
        if (conflict == 5) invalid["instructionPositionEditor"] = {{"phase", "editing"}};
        if (conflict == 6) invalid["menuPositionEditor"] = {{"step", 1}};
        if (conflict == 7) invalid["menuUnlock"] = "rebuild";
        Reject([&] { fixture.Read(invalid); });
    }
    for (const auto* replacement : {"{\"elapsedMilliseconds\":0,\"elapsedMilliseconds\":1}",
        "{\"elapsedMilliseconds\":0,\"\\u0065lapsedMilliseconds\":1}"}) {
        auto bytes = source.dump(); const auto object = std::string("{\"elapsedMilliseconds\":0}");
        const auto start = bytes.find(object); Check(start != std::string::npos, "Credits preview duplicate fixture lost its object");
        bytes.replace(start, object.size(), replacement); Reject([&] { fixture.ReadBytes(bytes); });
    }
}
}
