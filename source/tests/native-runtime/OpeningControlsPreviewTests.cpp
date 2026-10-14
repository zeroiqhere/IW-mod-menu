#include "../native-layout/BackendPreview.h"
#include "ControlLabels.h"
#include "TestSupport.h"
#include <fstream>
#include <random>

namespace iw::overlay::tests {
namespace {
class OpeningPreviewFile final {
  public:
    OpeningPreviewFile() {
        std::random_device random;
        // Each reader test reserves its own directory without reusing another file
        for (int attempt = 0; attempt < 32; ++attempt) {
            directory_ = std::filesystem::temp_directory_path() /
                         ("iw-opening-reader-" + std::to_string(random()) + "-" + std::to_string(random()));
            if (std::filesystem::create_directory(directory_)) {
                std::error_code error;
                std::filesystem::permissions(directory_, std::filesystem::perms::owner_all, error);
                if (error) {
                    std::error_code ignored;
                    std::filesystem::remove_all(directory_, ignored);
                    throw std::runtime_error("Could not protect an opening reader fixture");
                }
                return;
            }
        }
        throw std::runtime_error("Could not reserve an opening reader fixture");
    }
    OpeningPreviewFile(const OpeningPreviewFile &) = delete;
    OpeningPreviewFile &operator=(const OpeningPreviewFile &) = delete;
    ~OpeningPreviewFile() {
        std::error_code ignored;
        std::filesystem::remove_all(directory_, ignored);
    }
    MenuSnapshot Read(const std::string &bytes, const runtime::OpeningActionLabels *labels = nullptr) const {
        const auto file = directory_ / "state.json";
        // Only this owned fixture is replaced during malformed-packet checks
        {
            std::ofstream output(file, std::ios::binary | std::ios::trunc);
            output << bytes;
            if (!output) {
                throw std::runtime_error("Could not write an opening reader fixture");
            }
        }
        return ReadBackendPreview(file, labels);
    }

  private:
    std::filesystem::path directory_;
};
} // namespace
void OpeningPreviewMatchesStrictDescriptorSpacerAndPrivateLabels() {
    OpeningPreviewFile file;
    runtime::OpeningActionLabels labels{};
    labels[4] = "V / Mouse X1";
    auto packet = StateJson();
    packet["mainOpeningControls"] = {{"revision", 9}, {"actions", {4}}};
    packet["rows"] = {{{"label", "Melee"}, {"value", "ON"}, {"enabled", true}, {"openingAction", 4}},
                      {{"label", ""}, {"value", ""}, {"enabled", false}, {"isSpacer", true}}};
    const auto preview = file.Read(packet.dump(), &labels);
    Check(preview.mainOpeningControls == MainOpeningControls{9, {OpeningAction::Melee}} &&
              preview.rows[0].label == labels[4] && preview.rows[1].isSpacer,
          "Preview did not own the exact policy, private caption and spacer");
    // Both readers must reject malformed supplied fields even while closed
    for (int malformed = 0; malformed < 6; ++malformed) {
        auto bad = packet;
        if (malformed == 0) {
            bad["mainOpeningControls"] = nullptr;
        }
        if (malformed == 1) {
            bad["mainOpeningControls"]["actions"] = {7};
        }
        if (malformed == 2) {
            bad["rows"][0]["openingAction"] = "4";
        }
        if (malformed == 3) {
            bad["rows"][1]["isSpacer"] = nullptr;
        }
        if (malformed == 4) {
            bad["selectedIndex"] = 1;
        }
        if (malformed == 5) {
            bad["rows"][1]["openingAction"] = 4;
        }
        bad["isOpen"] = false;
        Reject([&] { file.Read(bad.dump(), &labels); });
        Reject([&] {
            runtime::ParseState(bad.dump(), GetCurrentProcessId(), nullptr, nullptr, nullptr, nullptr,
                                nullptr, std::nullopt, nullptr, nullptr, &labels);
        });
    }
    auto bytes = packet.dump();
    const std::string action = "\"openingAction\":4";
    bytes.replace(bytes.find(action), action.size(), action + ",\"\\u006fpeningAction\":5");
    Reject([&] { file.Read(bytes, &labels); });
    packet.erase("mainOpeningControls");
    packet["rows"] = StateJson().at("rows");
    Check(!file.Read(packet.dump()).mainOpeningControls, "Legacy preview invented a policy");
}
void OpeningPoliciesPreviewOwnsBothVerifiedTuplesAndSavedCaptions() {
    OpeningPreviewFile file;
    runtime::OpeningActionLabels labels{}; labels[4] = "V"; labels[5] = "Mouse Right";
    labels[8] = "F"; labels[10] = "E";
    auto packet = StateJson();
    packet["mainOpeningControls"] = {{"revision", 1u}, {"actions", {5, 4}}};
    packet["openingPolicies"] = {{"revision", 2u}, {"main", packet["mainOpeningControls"]},
        {"quick", {{"revision", 2u}, {"actions", {5, 8}}}}, {"quickDisabled", false}};
    packet["rows"] = {{{"label", "Use"}, {"value", "ON"}, {"enabled", true}, {"openingAction", 8}}};
    const auto preview = file.Read(packet.dump(), &labels);
    Check(preview.openingPolicies && preview.openingPolicies->revision == 2 && preview.rows[0].label == "F",
          "Dual reader lost its owned policy or exact saved-key caption");
    // Binding proof and mirror checks apply even with a closed HUD
    packet["isOpen"] = false;
    for (int invalid = 0; invalid < 4; ++invalid) {
        auto bad = packet;
        if (invalid == 0) bad["openingPolicies"] = nullptr;
        if (invalid == 1) bad["openingPolicies"]["main"]["revision"] = 3;
        if (invalid == 2) bad["openingPolicies"]["quick"]["actions"] = {12};
        if (invalid == 3) bad["openingPolicies"]["quickDisabled"] = 1;
        Reject([&] { file.Read(bad.dump(), &labels); });
    }
}
void SelectorPreviewPreservesOwnedOpeningAndRejectsOrdinaryCooldown() {
    OpeningPreviewFile file; auto packet = StateJson();
    packet["isOpen"] = true; packet["isRootMenu"] = false;
    packet["mainOpeningControls"] = {{"revision", 1u}, {"actions", {5, 4}}};
    packet["rows"] = {{{"label", "Menu"}, {"value", "Main Menu"}, {"enabled", true}},
        {{"label", "Bind Slot"}, {"value", "1"}, {"enabled", true}},
        {{"label", ""}, {"value", ""}, {"enabled", false}, {"isSpacer", true}},
        {{"label", "Aim"}, {"value", "ON"}, {"enabled", true}}};
    // The complete source catalog carries typed actions even with remapped captions
    packet["rows"].erase(packet["rows"].begin() + 3, packet["rows"].end());
    for (unsigned id = 0; id < OpeningActionCount; ++id)
        packet["rows"].push_back({{"label", OpeningActionCaption(static_cast<OpeningAction>(id))},
            {"value", "OFF"}, {"enabled", true}, {"openingAction", id}});
    packet["menuPreferenceRefresh"] = {{"phase", "selector"}};
    packet["menuOpening"] = {{"revision", 1u}, {"elapsedMilliseconds", 100}};
    Check(file.Read(packet.dump()).menuOpening.has_value(), "Selector preview discarded the owned fade");
    for (int invalid = 0; invalid < 4; ++invalid) {
        auto bad = packet;
        if (invalid == 0) bad["menuPreferenceRefresh"]["phase"] = "cooldown";
        if (invalid == 1) bad["selectedIndex"] = 1;
        if (invalid == 2) bad["rows"][1]["label"] = "Other";
        if (invalid == 3) bad.erase("mainOpeningControls");
        Reject([&] { file.Read(bad.dump()); });
    }
}
} // namespace iw::overlay::tests
