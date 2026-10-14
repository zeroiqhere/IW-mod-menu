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
                / ("iw-preference-refresh-preview-" + std::to_string(random()) + "-" + std::to_string(random()));
            if (std::filesystem::create_directory(candidate)) {
                directory_ = std::move(candidate);
                std::error_code error;
                std::filesystem::permissions(directory_, std::filesystem::perms::owner_all, error);
                if (error) {
                    std::error_code ignored;
                    std::filesystem::remove_all(directory_, ignored);
                    throw std::runtime_error("Could not protect the preference refresh preview fixture directory");
                }
                return;
            }
        }
        throw std::runtime_error("Could not reserve the preference refresh preview fixture directory");
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
            if (!output) throw std::runtime_error("Could not write the preference refresh preview fixture");
        }
        return ReadBackendPreview(path);
    }
private:
    std::filesystem::path directory_;
};

}
void PreferenceRefreshPreviewMatchesRuntimeLegacyAndBothPhases() {
    OwnedPreviewFile fixture;
    auto packet = StateJson(); packet["isOpen"] = true; packet["isRootMenu"] = false;
    const auto compare = [&] {
        const auto production = runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot;
        const auto preview = fixture.Read(packet);
        Check(preview.menuPreferenceRefresh == production->menuPreferenceRefresh
            && preview.rows.size() == production->rows.size() && preview.selectedIndex == production->selectedIndex,
            "CPU preview preference refresh differs from production parsing");
    };
    compare(); packet["menuPreferenceRefresh"] = nullptr; compare();
    for (const auto* phase : {"pending", "cooldown"}) {
        packet["menuPreferenceRefresh"] = {{"phase", phase}}; compare();
    }
}
void PreferenceRefreshPreviewRejectsMalformedPhasesContextsAndDuplicates() {
    OwnedPreviewFile fixture;
    auto packet = StateJson(); packet["isOpen"] = true; packet["isRootMenu"] = false;
    for (const Json& invalid : {Json(false), Json(1), Json("pending"), Json::array(), Json::object(),
        Json{{"phase", nullptr}}, Json{{"phase", true}}, Json{{"phase", 1}}, Json{{"phase", 1.0}},
        Json{{"phase", "Pending"}}, Json{{"phase", "pause"}}, Json{{"phase", ""}},
        Json{{"phase", "pending"}, {"instructions", Json::array()}}}) {
        packet["menuPreferenceRefresh"] = invalid; Reject([&] { fixture.Read(packet); });
    }
    packet["menuPreferenceRefresh"] = {{"phase", "pending"}};
    for (int conflict = 0; conflict < 14; ++conflict) {
        auto invalid = packet;
        if (conflict == 0) invalid["isOpen"] = false;
        if (conflict == 1) invalid["isRootMenu"] = true;
        if (conflict == 2) invalid["rows"] = Json::array();
        if (conflict == 3) invalid["selectedIndex"] = 1;
        if (conflict == 4) invalid["rows"] = Json::object();
        if (conflict == 5) invalid["widthEditor"] = {{"step", 1}};
        if (conflict == 6) invalid["instructionPositionEditor"] = {{"phase", "editing"}};
        if (conflict == 7) invalid["menuPositionEditor"] = {{"step", 1}};
        if (conflict == 8) invalid["menuCredits"] = {{"elapsedMilliseconds", 0}};
        if (conflict == 9) invalid["menuUnlock"] = "rebuild";
        if (conflict == 10) invalid["menuUnlock"] = "pause";
        if (conflict == 11) invalid["selectedIndex"] = 0.0;
        if (conflict == 12) invalid["selectedIndex"] = false;
        if (conflict == 13) invalid["selectedIndex"] = -1;
        Reject([&] { fixture.Read(invalid); });
    }
    // The bounded reader rejects duplicate decoded phase names before either parser uses them
    for (const auto* duplicate : {"{\"phase\":\"pending\",\"phase\":\"cooldown\"}",
        "{\"phase\":\"pending\",\"\\u0070hase\":\"cooldown\"}"}) {
        auto bytes = packet.dump(); const std::string object = "{\"phase\":\"pending\"}";
        const auto start = bytes.find(object); Check(start != std::string::npos, "Refresh preview duplicate fixture lost its object");
        bytes.replace(start, object.size(), duplicate); Reject([&] { fixture.ReadBytes(bytes); });
    }
}
}
