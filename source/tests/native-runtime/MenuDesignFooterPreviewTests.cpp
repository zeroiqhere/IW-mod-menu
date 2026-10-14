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
                / ("iw-menu-design-footer-preview-" + std::to_string(random()) + "-" + std::to_string(random()));
            if (std::filesystem::create_directory(candidate)) {
                directory_ = std::move(candidate);
                std::error_code error;
                std::filesystem::permissions(directory_, std::filesystem::perms::owner_all, error);
                if (error) {
                    std::error_code ignored;
                    std::filesystem::remove_all(directory_, ignored);
                    throw std::runtime_error("Could not protect the menu design footer preview fixture directory");
                }
                return;
            }
        }
        throw std::runtime_error("Could not reserve the menu design footer preview fixture directory");
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
            if (!output) throw std::runtime_error("Could not write the menu design footer preview fixture");
        }
        return ReadBackendPreview(path);
    }
private:
    std::filesystem::path directory_;
};

}

namespace {
Json FooterPreviewPacket() {
    auto packet = StateJson(); packet["isOpen"] = true; packet["isRootMenu"] = false;
    packet["menuDesignTransition"] = {{"phase", "rebuild"}, {"footerDesign", "AIO"}};
    packet["appearance"] = {{"booleanDisplay", "Boxes"}, {"boolBoxLocation", "Right"}, {"instructionsVisible", true},
        {"optionCounter", false}, {"coloredCursor", true}, {"spotlightCursor", true}, {"largeCursor", false}};
    return packet;
}
}
void MenuDesignFooterPreviewMatchesRuntimeLegacyAndOwnedRebuildTargets() {
    OwnedPreviewFile fixture; auto packet = FooterPreviewPacket();
    const auto compare = [&] {
        const auto production = runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot;
        const auto preview = fixture.Read(packet);
        Check(preview.menuDesignFooterDesign == production->menuDesignFooterDesign
            && preview.menuDesignTransition == production->menuDesignTransition && preview.appearance == production->appearance
            && preview.navigationRevision == production->navigationRevision && preview.menuRevision == production->menuRevision,
            "Preview footer target changed captured appearance or differs from production state");
    };
    // Missing and explicit null remain legacy descriptors in either transition phase
    packet.erase("menuDesignTransition"); compare(); packet["menuDesignTransition"] = nullptr; compare();
    for (const auto* phase : {"rebuild", "cooldown"}) {
        packet["menuDesignTransition"] = {{"phase", phase}}; compare();
        packet["menuDesignTransition"]["footerDesign"] = nullptr; compare();
    }
    for (const auto* target : {"Apparition", "Classic", "Native", "AIO", "Basic"}) {
        packet = FooterPreviewPacket(); packet["menuDesignTransition"]["footerDesign"] = target;
        packet["appearance"]["coloredCursor"] = false; compare();
    }
}

void MenuDesignFooterPreviewRejectsMalformedPhaseFlagsContextsAndDecodedDuplicates() {
    OwnedPreviewFile fixture; auto packet = FooterPreviewPacket();
    for (const Json& invalid : {Json(false), Json(1), Json(1.0), Json::array(), Json::object(),
        Json(""), Json("App"), Json("aio"), Json("Aio"), Json("Unknown")}) {
        auto supplied = packet; supplied["menuDesignTransition"]["footerDesign"] = invalid;
        Reject([&] { fixture.Read(supplied); });
    }
    // Extra members and absent phase cannot create another footer or timing policy
    auto extra = packet; extra["menuDesignTransition"]["duration"] = nullptr; Reject([&] { fixture.Read(extra); });
    extra["menuDesignTransition"].erase("footerDesign"); Reject([&] { fixture.Read(extra); });
    extra = packet; extra["menuDesignTransition"].erase("phase"); Reject([&] { fixture.Read(extra); });
    for (const auto* target : {"Apparition", "Classic", "Native", "AIO", "Basic"}) {
        auto cooldown = packet; cooldown["appearance"]["design"] = target; cooldown["appearance"]["coloredCursor"] = false;
        cooldown["menuDesignTransition"] = {{"phase", "cooldown"}, {"footerDesign", target}};
        auto legacy = cooldown; legacy["menuDesignTransition"].erase("footerDesign"); fixture.Read(legacy);
        Reject([&] { fixture.Read(cooldown); });
    }
    for (const auto* target : {"Classic", "Native", "AIO", "Basic"}) {
        auto supplied = packet; supplied["menuDesignTransition"]["footerDesign"] = target;
        const bool counter = std::string(target) == "AIO" || std::string(target) == "Basic";
        supplied["appearance"]["coloredCursor"] = !counter; supplied["appearance"]["optionCounter"] = counter;
        auto legacy = supplied; legacy["menuDesignTransition"].erase("footerDesign"); fixture.Read(legacy);
        Reject([&] { fixture.Read(supplied); });
    }
    for (int conflict = 0; conflict < 11; ++conflict) {
        auto supplied = packet;
        if (conflict == 0) supplied["isOpen"] = false;
        if (conflict == 1) supplied["isRootMenu"] = true;
        if (conflict == 2) supplied["rows"] = Json::array();
        if (conflict == 3) supplied["widthEditor"] = {{"step", 1}};
        if (conflict == 4) supplied["instructionPositionEditor"] = {{"phase", "editing"}};
        if (conflict == 5) supplied["menuPositionEditor"] = {{"step", 1}};
        if (conflict == 6) supplied["menuCredits"] = {{"elapsedMilliseconds", 0}};
        if (conflict == 7) supplied["menuUnlock"] = "pause";
        if (conflict == 8) supplied["menuPreferenceRefresh"] = {{"phase", "pending"}};
        if (conflict == 9) supplied["menuOpenTransition"] = {{"phase", "cooldown"}};
        if (conflict == 10) supplied["menuOpening"] = {{"revision", 1}, {"elapsedMilliseconds", 0}};
        // Each conflicting view is valid before the rebuild descriptor is added
        if (conflict >= 3) {
            if (conflict <= 6) supplied["rows"] = Json::array();
            if (conflict == 10) supplied["rows"].push_back(supplied["rows"][0]);
            auto standalone = supplied; standalone.erase("menuDesignTransition"); fixture.Read(standalone);
        }
        Reject([&] { fixture.Read(supplied); });
    }
    // Duplicate decoded target names cannot erase or replace a validated target
    for (const auto* duplicate : {"\"footerDesign\":\"Basic\"", "\"\\u0066ooterDesign\":\"Basic\""}) {
        auto bytes = packet.dump(); const std::string field = "\"footerDesign\":\"AIO\"";
        const auto start = bytes.find(field); Check(start != std::string::npos, "Footer preview duplicate fixture lost its target");
        bytes.replace(start, field.size(), field + "," + duplicate); Reject([&] { fixture.ReadBytes(bytes); });
    }
}
}
