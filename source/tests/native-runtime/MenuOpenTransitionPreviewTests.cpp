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
                / ("iw-menu-open-transition-preview-" + std::to_string(random()) + "-" + std::to_string(random()));
            if (std::filesystem::create_directory(candidate)) {
                directory_ = std::move(candidate);
                std::error_code error;
                std::filesystem::permissions(directory_, std::filesystem::perms::owner_all, error);
                if (error) {
                    std::error_code ignored;
                    std::filesystem::remove_all(directory_, ignored);
                    throw std::runtime_error("Could not protect the menu open transition preview fixture directory");
                }
                return;
            }
        }
        throw std::runtime_error("Could not reserve the menu open transition preview fixture directory");
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
            if (!output) throw std::runtime_error("Could not write the menu open transition preview fixture");
        }
        return ReadBackendPreview(path);
    }
private:
    std::filesystem::path directory_;
};

}

void MenuOpenTransitionPreviewMatchesRuntimeLegacyEmptyRootChildAndFades() {
    OwnedPreviewFile fixture; auto packet = StateJson();
    const auto compare = [&] {
        const auto state = runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot;
        const auto preview = fixture.Read(packet);
        Check(preview.menuOpenTransition == state->menuOpenTransition && preview.menuOpening == state->menuOpening
            && preview.rows.size() == state->rows.size() && preview.selectedIndex == state->selectedIndex
            && preview.isRootMenu == state->isRootMenu && preview.navigationRevision == state->navigationRevision
            && preview.menuRevision == state->menuRevision, "Preview normal opening differs from production state");
    };
    // Legacy absence and null preserve immediate controls for old packets
    compare(); packet["menuOpenTransition"] = nullptr; compare(); packet["isOpen"] = true;
    for (bool root : {true, false}) for (std::size_t count : {std::size_t{0}, std::size_t{1}, std::size_t{10}}) {
        packet["isRootMenu"] = root; packet["rows"] = Json::array();
        for (std::size_t row = 0; row < count; ++row) packet["rows"].push_back(StateJson()["rows"][0]);
        for (const auto* phase : {"rebuild", "cooldown"}) {
            packet["menuOpenTransition"] = {{"phase", phase}}; compare();
        }
    }
    // Only the drawn cooldown can carry a separate active fade
    packet["menuOpening"] = {{"revision", 1}, {"elapsedMilliseconds", 550}}; compare();
    packet["menuOpenTransition"] = nullptr; compare();
}

void MenuOpenTransitionPreviewRejectsMalformedConflictingAndUndrawnStates() {
    OwnedPreviewFile fixture; auto packet = StateJson(); packet["isOpen"] = true;
    for (const Json& invalid : {Json(false), Json(1), Json("rebuild"), Json::array(), Json::object(),
        Json{{"phase", nullptr}}, Json{{"phase", true}}, Json{{"phase", 1}}, Json{{"phase", 1.0}},
        Json{{"phase", "Rebuild"}}, Json{{"phase", "pause"}}, Json{{"phase", ""}},
        Json{{"phase", "rebuild"}, {"elapsedMilliseconds", 0}}}) {
        packet["menuOpenTransition"] = invalid; Reject([&] { fixture.Read(packet); });
    }
    packet["menuOpenTransition"] = {{"phase", "rebuild"}};
    // Modal exclusions and strict empty cursor rules match the production parser
    for (const auto* phase : {"rebuild", "cooldown"}) for (int conflict = 0; conflict < 16; ++conflict) {
        auto invalid = packet; invalid["isRootMenu"] = false; invalid["menuOpenTransition"]["phase"] = phase;
        if (conflict == 0) invalid["isOpen"] = false;
        if (conflict == 1) { invalid["rows"] = Json::array(); invalid["selectedIndex"] = 1; }
        if (conflict == 2) invalid["selectedIndex"] = 1;
        if (conflict == 3) invalid["selectedIndex"] = -1;
        if (conflict == 4) invalid["selectedIndex"] = 0.0;
        if (conflict == 5) invalid["rows"] = Json::object();
        if (conflict == 6) invalid["widthEditor"] = {{"step", 1}};
        if (conflict == 7) invalid["instructionPositionEditor"] = {{"phase", "editing"}};
        if (conflict == 8) invalid["menuPositionEditor"] = {{"step", 1}};
        if (conflict == 9) invalid["menuCredits"] = {{"elapsedMilliseconds", 0}};
        if (conflict == 10) invalid["menuUnlock"] = "rebuild";
        if (conflict == 11) invalid["menuUnlock"] = "pause";
        if (conflict == 12) invalid["menuPreferenceRefresh"] = {{"phase", "pending"}};
        if (conflict == 13) invalid["menuPreferenceRefresh"] = {{"phase", "cooldown"}};
        if (conflict == 14) invalid["menuDesignTransition"] = {{"phase", "rebuild"}};
        if (conflict == 15) invalid["menuDesignTransition"] = {{"phase", "cooldown"}};
        if (conflict >= 6) {
            // Each conflicting modal must remain valid on its own before adding normal opening
            if (conflict <= 9) invalid["rows"] = Json::array();
            auto standalone = invalid; standalone.erase("menuOpenTransition"); fixture.Read(standalone);
        }
        Reject([&] { fixture.Read(invalid); });
    }
    packet["rows"].push_back(packet["rows"][0]); packet["menuOpening"] = {{"revision", 1}, {"elapsedMilliseconds", 0}};
    Reject([&] { fixture.Read(packet); }); packet.erase("menuOpening");
    // Escaped property names share the same duplicate rejection as ordinary names
    for (const auto* duplicate : {"{\"phase\":\"rebuild\",\"phase\":\"cooldown\"}",
        "{\"phase\":\"rebuild\",\"\\u0070hase\":\"cooldown\"}"}) {
        auto bytes = packet.dump(); const std::string object = "{\"phase\":\"rebuild\"}";
        const auto start = bytes.find(object); Check(start != std::string::npos, "Open preview duplicate fixture lost its object");
        bytes.replace(start, object.size(), duplicate); Reject([&] { fixture.ReadBytes(bytes); });
    }
    // The preview reader also rejects an inactive duplicate of the active top-level field
    for (const auto* duplicate : {"\"menuOpenTransition\":null", "\"menu\\u004fpenTransition\":null"}) {
        auto bytes = packet.dump(); const std::string field = "\"menuOpenTransition\":{\"phase\":\"rebuild\"}";
        const auto start = bytes.find(field); Check(start != std::string::npos, "Open preview duplicate fixture lost its metadata");
        bytes.replace(start, field.size(), field + "," + duplicate); Reject([&] { fixture.ReadBytes(bytes); });
    }
}
}
