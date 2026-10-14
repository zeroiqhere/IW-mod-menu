#include "MenuChromeTests.h"
#include "../TestSupport.h"
#include "../../native-layout/BackendPreview.h"
#include <fstream>
#include <random>
#include <limits>
namespace iw::overlay::tests::chrome {
namespace {
class Preview final {
public:
    Preview() {
        std::random_device random;
        for (unsigned attempt = 0; attempt < 32; ++attempt) {
            auto path = std::filesystem::temp_directory_path() / ("iw-chrome-preview-" + std::to_string(random()));
            if (std::filesystem::create_directory(path)) {
                directory_ = std::move(path); std::filesystem::permissions(directory_, std::filesystem::perms::owner_all); return;
            }
        }
        throw std::runtime_error("Could not reserve chrome preview directory");
    }
    ~Preview() { std::error_code ignored; std::filesystem::remove_all(directory_, ignored); }
    MenuSnapshot Read(const std::string& bytes) const {
        const auto path = directory_ / "state.json";
        { std::ofstream stream(path, std::ios::binary); stream << bytes; if (!stream) throw std::runtime_error("Could not write chrome preview"); }
        return ReadBackendPreview(path);
    }
private:
    std::filesystem::path directory_;
};
}
void PreviewMatchesStrictGlobalChromeAndLegacyDefaults() {
    Preview file;
    for (bool open : {false, true}) {
        auto packet = StateJson(); packet["isOpen"] = open;
        Check(!file.Read(packet.dump()).menuChromeRevision, "Legacy preview invented chrome");
        packet["menuChromeRevision"] = nullptr;
        Check(!file.Read(packet.dump()).menuChromeRevision, "Null legacy preview invented chrome");
        for (std::uint64_t revision : {std::uint64_t{1}, std::numeric_limits<std::uint64_t>::max()}) {
            packet["menuChromeRevision"] = revision;
            const auto owned = runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot;
            const auto preview = file.Read(packet.dump());
            Check(preview.menuChromeRevision == revision && preview.menuChromeRevision == owned->menuChromeRevision
                && preview.rows[0].value == owned->rows[0].value, "Preview changed chrome ownership or current row data");
        }
    }
    // Supplied metadata has the same fail-closed boundary in both readers
    for (const Json& value : {Json(0), Json(-1), Json(true), Json(1.0), Json("1"), Json::array(), Json::object()}) {
        auto packet = StateJson(); packet["menuChromeRevision"] = value;
        Reject([&] { file.Read(packet.dump()); });
    }
    auto packet = StateJson(); packet["menuChromeRevision"] = 1; auto bytes = packet.dump();
    const std::string field = "\"menuChromeRevision\":1";
    auto offset = bytes.find(field); bytes.insert(offset, "\"menuChrome\\u0052evision\":2,");
    Check(bytes.find("\\u0052") != std::string::npos, "Preview duplicate fixture must retain its JSON escape");
    Reject([&] { file.Read(bytes); });
    bytes = packet.dump(); offset = bytes.find(field); bytes.replace(offset, field.size(), "\"menuChromeRevision\":18446744073709551616");
    Reject([&] { file.Read(bytes); });
}
}
