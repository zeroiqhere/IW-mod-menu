#include "../TestSupport.h"
#include "../../native-layout/BackendPreview.h"
#include <fstream>
#include <random>

namespace iw::overlay::tests {
namespace {
class EligibilityPreview {
public:
    EligibilityPreview() {
        std::random_device random;
        // One fresh owned directory isolates the read-only preview input
        for (int attempt = 0; attempt < 32; ++attempt) {
            directory_ = std::filesystem::temp_directory_path() / ("iw-eligibility-preview-"
                + std::to_string(random()) + "-" + std::to_string(random()));
            if (std::filesystem::create_directory(directory_)) return;
        }
        throw std::runtime_error("Could not reserve the eligibility preview fixture");
    }
    EligibilityPreview(const EligibilityPreview&) = delete;
    EligibilityPreview& operator=(const EligibilityPreview&) = delete;
    ~EligibilityPreview() {
        std::error_code ignored;
        std::filesystem::remove_all(directory_, ignored);
    }
    MenuSnapshot Read(const std::string& bytes) const {
        const auto path = directory_ / "state.json";
        {
            std::ofstream file(path, std::ios::binary | std::ios::trunc);
            file << bytes;
            if (!file) throw std::runtime_error("Could not write the eligibility preview fixture");
        }
        return ReadBackendPreview(path);
    }
private:
    std::filesystem::path directory_;
};
}

void LocalEligibilityPreviewMatchesOwnedNullableFactsAndBothSurfaces() {
    EligibilityPreview fixture;
    auto packet = StateJson();
    Check(fixture.Read(packet.dump()).menuSurface == MenuSurface::Main
        && !fixture.Read(packet.dump()).localMenuEligibility, "Preview legacy admission changed");
    for (const auto* surface : {"main", "quick"}) for (const auto* life : {"unknown", "alive", "dead"}) {
        packet["menuSurface"] = surface;
        packet["localMenuEligibility"] = {{"revision", 19u}, {"life", life}, {"hasMenu", nullptr},
            {"controlsDisabled", false}, {"blocked", true}};
        const auto actual = runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot;
        const auto preview = fixture.Read(packet.dump());
        Check(preview.localMenuEligibility.has_value() && preview.localMenuEligibility == actual->localMenuEligibility
            && preview.menuSurface == actual->menuSurface, "Preview discarded or changed actual admission facts");
    }
}

void LocalEligibilityPreviewRejectsMalformedFactsSurfaceAndDecodedDuplicates() {
    EligibilityPreview fixture;
    auto packet = StateJson();
    for (const Json& bad : {Json(nullptr), Json::array(), Json::object(), Json(true)}) {
        packet["localMenuEligibility"] = bad;
        Reject([&] { fixture.Read(packet.dump()); });
    }
    packet["localMenuEligibility"] = {{"revision", 1u}, {"life", "alive"}, {"hasMenu", true},
        {"controlsDisabled", nullptr}, {"blocked", false}};
    for (const auto* field : {"revision", "life", "hasMenu", "controlsDisabled", "blocked"}) {
        auto bad = packet; bad["localMenuEligibility"].erase(field);
        Reject([&] { fixture.Read(bad.dump()); });
    }
    auto duplicate = packet.dump(); duplicate.insert(1, "\"localMenuEligibilit\\u0079\":null,");
    Reject([&] { fixture.Read(duplicate); });
    for (const Json& bad : {Json(nullptr), Json(0), Json("Main")}) {
        packet["menuSurface"] = bad;
        Reject([&] { fixture.Read(packet.dump()); });
    }
}
} // namespace iw::overlay::tests
