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
                / ("iw-menu-position-preview-" + std::to_string(random()) + "-" + std::to_string(random()));
            if (std::filesystem::create_directory(candidate)) {
                directory_ = std::move(candidate);
                std::error_code error;
                std::filesystem::permissions(directory_, std::filesystem::perms::owner_all, error);
                if (error) {
                    std::error_code ignored;
                    std::filesystem::remove_all(directory_, ignored);
                    throw std::runtime_error("Could not protect the menu position preview fixture directory");
                }
                return;
            }
        }
        throw std::runtime_error("Could not reserve the menu position preview fixture directory");
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
            if (!output) throw std::runtime_error("Could not write the menu position preview fixture");
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


void MenuPositionPreviewMatchesRuntimeDefaultsSignedBoundsAndEditorSteps() {
    OwnedPreviewFile fixture;
    auto source = StateJson();
    const auto compare = [&] {
        const auto snapshot = runtime::ParseState(source.dump(), GetCurrentProcessId()).snapshot;
        const auto preview = fixture.Read(source);
        Check(preview.appearance == snapshot->appearance && preview.menuPositionEditor == snapshot->menuPositionEditor,
            "CPU preview panel position or editor differs from production parser");
    };
    compare(); source["appearance"] = nullptr; compare();
    source["appearance"] = PreviewAppearance(); source["appearance"]["menuPosition"] = nullptr; compare();
    for (auto coordinate : {std::numeric_limits<std::int32_t>::min(), -1, 0, std::numeric_limits<std::int32_t>::max()}) {
        source["appearance"]["menuPosition"] = {{"x", coordinate}, {"y", coordinate}}; compare();
    }
    source["isOpen"] = true; source["isRootMenu"] = false; source["rows"] = Json::array();
    for (int step : {1, 10}) { source["menuPositionEditor"] = {{"step", step}}; compare(); }
    source["menuPositionEditor"] = nullptr; compare();
}
void MenuPositionPreviewRejectsMalformedCoordinatesEditorContextsAndDuplicates() {
    OwnedPreviewFile fixture;
    auto source = StateJson(); source["appearance"] = PreviewAppearance();
    for (const Json& invalid : {Json(false), Json(1), Json::array(), Json::object(), Json{{"x", 1}},
        Json{{"x", 0}, {"y", 85}, {"z", 1}}, Json{{"x", 2147483648ULL}, {"y", 85}},
        Json{{"x", 0}, {"y", -2147483649LL}}, Json{{"x", true}, {"y", 85}}, Json{{"x", 0}, {"y", 85.0}}}) {
        source["appearance"]["menuPosition"] = invalid;
        Reject([&] { fixture.Read(source); });
    }
    source["appearance"]["menuPosition"] = nullptr;
    source["isOpen"] = true; source["isRootMenu"] = false; source["rows"] = Json::array();
    for (const Json& invalid : {Json(false), Json(1), Json::array(), Json::object(), Json{{"step", nullptr}},
        Json{{"step", true}}, Json{{"step", "1"}}, Json{{"step", 1.0}}, Json{{"step", -1}}, Json{{"step", 0}},
        Json{{"step", 11}}, Json{{"step", std::numeric_limits<std::uint64_t>::max()}}, Json{{"step", 1}, {"phase", "editing"}}}) {
        source["menuPositionEditor"] = invalid; Reject([&] { fixture.Read(source); });
    }
    source["menuPositionEditor"] = {{"step", 1}};
    for (int conflict = 0; conflict < 7; ++conflict) {
        auto invalid = source;
        if (conflict == 0) invalid["isOpen"] = false;
        if (conflict == 1) invalid["isRootMenu"] = true;
        if (conflict == 2) invalid["rows"] = StateJson().at("rows");
        if (conflict == 3) invalid["selectedIndex"] = 1;
        if (conflict == 4) invalid["widthEditor"] = {{"step", 1}};
        if (conflict == 5) invalid["instructionPositionEditor"] = {{"phase", "editing"}};
        if (conflict == 6) invalid["menuUnlock"] = "rebuild";
        Reject([&] { fixture.Read(invalid); });
    }
    auto bytes = source.dump(); const auto position = bytes.find("{\"step\":1}");
    bytes.replace(position, 10, "{\"step\":1,\"\\u0073tep\":2}");
    Reject([&] { fixture.ReadBytes(bytes); });
}
}
