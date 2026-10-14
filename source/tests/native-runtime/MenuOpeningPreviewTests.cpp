#include "TestSupport.h"
#include "../native-layout/BackendPreview.h"
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
            auto candidate = std::filesystem::temp_directory_path()
                / ("iw-menu-opening-preview-" + std::to_string(random()) + "-" + std::to_string(random()));
            if (std::filesystem::create_directory(candidate)) {
                directory_ = std::move(candidate);
                std::error_code error;
                std::filesystem::permissions(directory_, std::filesystem::perms::owner_all, error);
                if (error) {
                    std::error_code ignored;
                    std::filesystem::remove_all(directory_, ignored);
                    throw std::runtime_error("Could not protect the menu opening preview fixture directory");
                }
                return;
            }
        }
        throw std::runtime_error("Could not reserve the menu opening preview fixture directory");
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
            if (!output) throw std::runtime_error("Could not write the menu opening preview fixture");
        }
        return ReadBackendPreview(path);
    }
private:
    std::filesystem::path directory_;
};

}

void MenuOpeningPreviewMatchesRuntimeLegacyBoundsAndAllowedContexts() {
    OwnedPreviewFile fixture; auto packet = StateJson();
    const auto compare = [&] {
        const auto production = runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot;
        const auto preview = fixture.Read(packet);
        Check(preview.menuOpening == production->menuOpening && preview.menuRevision == production->menuRevision
            && preview.navigationRevision == production->navigationRevision,
            "CPU preview opening differs from production state or public epochs");
    };
    // Legacy absence and null stay inactive before testing the two strict active bounds
    compare(); packet["menuOpening"] = nullptr; compare();
    packet["isOpen"] = true; packet["rows"].push_back(packet["rows"][0]);
    for (bool root : {true, false}) {
        packet["isRootMenu"] = root;
        for (auto revision : {std::uint64_t{1}, UINT64_MAX}) {
            for (int elapsed : {0, 99}) {
                packet["menuOpening"] = {{"revision", revision}, {"elapsedMilliseconds", elapsed}}; compare();
            }
        }
    }
    packet["menuDesignTransition"] = {{"phase", "cooldown"}}; compare();
}
void MenuOpeningPreviewRejectsMalformedProgressContextsAndDecodedDuplicates() {
    OwnedPreviewFile fixture; auto packet = StateJson(); packet["isOpen"] = true; packet["isRootMenu"] = false;
    packet["rows"].push_back(packet["rows"][0]);
    for (const Json& invalid : {Json(false), Json(1), Json("opening"), Json::array(), Json::object(),
        Json{{"revision", 1}}, Json{{"elapsedMilliseconds", 0}},
        Json{{"revision", 0}, {"elapsedMilliseconds", 0}}, Json{{"revision", -1}, {"elapsedMilliseconds", 0}},
        Json{{"revision", nullptr}, {"elapsedMilliseconds", 0}}, Json{{"revision", true}, {"elapsedMilliseconds", 0}},
        Json{{"revision", "1"}, {"elapsedMilliseconds", 0}}, Json{{"revision", 1.0}, {"elapsedMilliseconds", 0}},
        Json{{"revision", 1}, {"elapsedMilliseconds", nullptr}}, Json{{"revision", 1}, {"elapsedMilliseconds", false}},
        Json{{"revision", 1}, {"elapsedMilliseconds", "0"}}, Json{{"revision", 1}, {"elapsedMilliseconds", 0.0}},
        Json{{"revision", 1}, {"elapsedMilliseconds", -1}}, Json{{"revision", 1}, {"elapsedMilliseconds", 100}},
        Json{{"revision", 1}, {"elapsedMilliseconds", UINT64_MAX}}, Json{{"revision", 1}, {"elapsedMilliseconds", 0}, {"duration", 100}}}) {
        packet["menuOpening"] = invalid; Reject([&] { fixture.Read(packet); });
    }
    // Preview rejection must match production modal and row-context restrictions
    packet["menuOpening"] = {{"revision", 1}, {"elapsedMilliseconds", 0}};
    for (int conflict = 0; conflict < 16; ++conflict) {
        auto invalid = packet;
        if (conflict == 0) invalid["isOpen"] = false;
        if (conflict == 1) invalid["rows"] = Json::array();
        if (conflict == 2) invalid["rows"] = StateJson().at("rows");
        if (conflict == 3) invalid["selectedIndex"] = 2;
        if (conflict == 4) invalid["widthEditor"] = {{"step", 1}};
        if (conflict == 5) invalid["instructionPositionEditor"] = {{"phase", "editing"}};
        if (conflict == 6) invalid["menuPositionEditor"] = {{"step", 1}};
        if (conflict == 7) invalid["menuCredits"] = {{"elapsedMilliseconds", 0}};
        if (conflict == 8) invalid["menuUnlock"] = "rebuild";
        if (conflict == 9) invalid["menuUnlock"] = "pause";
        if (conflict == 10) invalid["menuPreferenceRebuild"] = {{"phase", "rebuild"}};
        if (conflict == 11) invalid["menuPreferenceRefresh"] = {{"phase", "cooldown"}};
        if (conflict == 12) invalid["menuDesignTransition"] = {{"phase", "rebuild"}};
        if (conflict == 13) invalid["selectedIndex"] = 0.0;
        if (conflict == 14) invalid["rows"] = Json::object();
        if (conflict == 15) invalid["selectedIndex"] = -1;
        Reject([&] { fixture.Read(invalid); });
    }
    // Both event fields share the bounded reader's decoded-name duplicate protection
    for (const auto* field : {"\"revision\":1", "\"elapsedMilliseconds\":0"}) {
        for (bool escaped : {false, true}) {
            auto bytes = packet.dump(); const std::string original(field);
            const auto start = bytes.find(original); Check(start != std::string::npos, "Opening preview duplicate fixture lost its field");
            const std::string duplicate = original == "\"revision\":1"
                ? escaped ? "\"\\u0072evision\":2" : "\"revision\":2"
                : escaped ? "\"\\u0065lapsedMilliseconds\":1" : "\"elapsedMilliseconds\":1";
            bytes.replace(start, original.size(), original + "," + duplicate); Reject([&] { fixture.ReadBytes(bytes); });
        }
    }
}
}
