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
                / ("iw-menu-design-preview-" + std::to_string(random()) + "-" + std::to_string(random()));
            if (std::filesystem::create_directory(candidate)) {
                directory_ = std::move(candidate);
                std::error_code error;
                std::filesystem::permissions(directory_, std::filesystem::perms::owner_all, error);
                if (error) {
                    std::error_code ignored;
                    std::filesystem::remove_all(directory_, ignored);
                    throw std::runtime_error("Could not protect the menu design preview fixture directory");
                }
                return;
            }
        }
        throw std::runtime_error("Could not reserve the menu design preview fixture directory");
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
            if (!output) throw std::runtime_error("Could not write the menu design preview fixture");
        }
        return ReadBackendPreview(path);
    }
private:
    std::filesystem::path directory_;
};

}

void MenuDesignPreviewMatchesRuntimeLegacyNamesConstraintsAndTransitions() {
    OwnedPreviewFile fixture; auto packet = StateJson();
    const auto compare = [&] {
        const auto production = runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot;
        const auto preview = fixture.Read(packet);
        Check(preview.appearance == production->appearance && preview.menuDesignTransition == production->menuDesignTransition,
            "CPU preview design state differs from production parsing");
    };
    compare(); packet["appearance"] = nullptr; compare();
    packet["appearance"] = {{"booleanDisplay", "Boxes"}, {"boolBoxLocation", "Right"}, {"instructionsVisible", true},
        {"optionCounter", false}, {"coloredCursor", false}, {"spotlightCursor", true}, {"largeCursor", false}};
    compare();
    for (const auto* name : {"Apparition", "Classic", "Native", "AIO", "Basic"}) {
        packet["appearance"]["design"] = name; compare();
    }
    packet["isOpen"] = true; packet["isRootMenu"] = false;
    packet["menuDesignTransition"] = nullptr; compare();
    for (const auto* phase : {"rebuild", "cooldown"}) {
        packet["menuDesignTransition"] = {{"phase", phase}}; compare();
    }
}
void MenuDesignPreviewRejectsMalformedNamesFlagsContextsAndDecodedDuplicates() {
    OwnedPreviewFile fixture; auto packet = StateJson();
    packet["appearance"] = {{"booleanDisplay", "Boxes"}, {"boolBoxLocation", "Right"}, {"instructionsVisible", true},
        {"optionCounter", false}, {"coloredCursor", false}, {"spotlightCursor", true}, {"largeCursor", false}};
    for (const Json& invalid : {Json(nullptr), Json(false), Json(1), Json(1.0), Json::array(), Json::object(),
        Json(""), Json("app"), Json("apparition"), Json("Aio"), Json("Classic "), Json("UNKNOWN")}) {
        packet["appearance"]["design"] = invalid; Reject([&] { fixture.Read(packet); });
    }
    // Unsupported cursor and counter combinations fail while closed as well as open
    for (const auto* name : {"Classic", "Native", "AIO", "Basic"}) {
        packet["appearance"]["design"] = name;
        const bool restrictedColor = std::string(name) == "Classic" || std::string(name) == "Native";
        packet["appearance"]["coloredCursor"] = restrictedColor;
        packet["appearance"]["optionCounter"] = !restrictedColor;
        Reject([&] { fixture.Read(packet); });
    }
    packet["appearance"]["design"] = "Apparition"; packet["appearance"]["coloredCursor"] = false;
    packet["appearance"]["optionCounter"] = false; packet["isOpen"] = true; packet["isRootMenu"] = false;
    for (const Json& invalid : {Json(false), Json(1), Json("rebuild"), Json::array(), Json::object(),
        Json{{"phase", nullptr}}, Json{{"phase", false}}, Json{{"phase", 1}}, Json{{"phase", 0.0}},
        Json{{"phase", "pending"}}, Json{{"phase", "Rebuild"}}, Json{{"phase", ""}}, Json{{"phase", "rebuild"}, {"duration", 50}}}) {
        packet["menuDesignTransition"] = invalid; Reject([&] { fixture.Read(packet); });
    }
    packet["menuDesignTransition"] = {{"phase", "rebuild"}};
    for (int conflict = 0; conflict < 16; ++conflict) {
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
        if (conflict == 11) invalid["menuPreferenceRefresh"] = {{"phase", "pending"}};
        if (conflict == 12) invalid["menuPreferenceRefresh"] = {{"phase", "cooldown"}};
        if (conflict == 13) invalid["selectedIndex"] = 0.0;
        if (conflict == 14) invalid["selectedIndex"] = false;
        if (conflict == 15) invalid["selectedIndex"] = -1;
        Reject([&] { fixture.Read(invalid); });
    }
    // Both plain and escaped duplicate names are rejected by the real bounded reader
    for (const auto* duplicate : {"\"design\":\"Apparition\",\"design\":\"Classic\"",
        "\"design\":\"Apparition\",\"\\u0064esign\":\"Classic\""}) {
        auto bytes = packet.dump(); const std::string field = "\"design\":\"Apparition\"";
        const auto start = bytes.find(field); Check(start != std::string::npos, "Preview design duplicate fixture lost its field");
        bytes.replace(start, field.size(), duplicate); Reject([&] { fixture.ReadBytes(bytes); });
    }
    for (const auto* duplicate : {"{\"phase\":\"rebuild\",\"phase\":\"cooldown\"}",
        "{\"phase\":\"rebuild\",\"\\u0070hase\":\"cooldown\"}"}) {
        auto bytes = packet.dump(); const std::string object = "{\"phase\":\"rebuild\"}";
        const auto start = bytes.find(object); Check(start != std::string::npos, "Preview transition duplicate fixture lost its object");
        bytes.replace(start, object.size(), duplicate); Reject([&] { fixture.ReadBytes(bytes); });
    }
}
}
