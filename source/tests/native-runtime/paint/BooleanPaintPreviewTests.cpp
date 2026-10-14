#include "BooleanPaintTests.h"
#include "../../native-layout/BackendPreview.h"
#include "../../../native/shared/paint/RowDisplayValue.h"
#include <fstream>
#include <random>

namespace iw::overlay::tests::paint {
namespace {
class PreviewFile final {
public:
    PreviewFile() {
        std::random_device random;
        for (int attempt = 0; attempt < 32; ++attempt) {
            auto candidate = std::filesystem::temp_directory_path() / ("iw-paint-preview-" + std::to_string(random()));
            if (std::filesystem::create_directory(candidate)) {
                directory_ = std::move(candidate);
                std::filesystem::permissions(directory_, std::filesystem::perms::owner_all); return;
            }
        }
        throw std::runtime_error("Could not reserve paint preview directory");
    }
    ~PreviewFile() { std::error_code ignored; std::filesystem::remove_all(directory_, ignored); }
    MenuSnapshot Read(const std::string& bytes) const {
        const auto path = directory_ / "state.json";
        { std::ofstream output(path, std::ios::binary); output << bytes;
          if (!output) throw std::runtime_error("Could not write paint preview fixture"); }
        return ReadBackendPreview(path);
    }
private:
    std::filesystem::path directory_;
};
}
void PreviewMatchesOwnedPaintAndRejectsMalformedSuppliedMetadata() {
    PreviewFile file;
    for (bool legacy : {false, true}) {
        auto packet = Packet(); if (legacy) packet.erase("menuBooleanPaint");
        const auto parsed = runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot;
        const auto preview = file.Read(packet.dump());
        Check(preview.menuBooleanPaint == parsed->menuBooleanPaint && preview.rows[0].value == "OFF"
            && ResolveRowDisplayValue(preview, 0) == (legacy ? "OFF" : "ON")
            && ResolveRowDisplayValue(preview, 1) == "UNKNOWN", "Preview changed current values or lost display-only paint");
    }
    for (int change = 0; change < 9; ++change) {
        auto packet = Packet();
        if (change == 0) packet["menuBooleanPaint"] = nullptr;
        if (change == 1) packet["menuBooleanPaint"]["extra"] = true;
        if (change == 2) packet["menuBooleanPaint"]["rows"][0]["index"] = 128;
        if (change == 3) packet["menuBooleanPaint"]["rows"][0]["value"] = "on";
        if (change == 4) packet["menuBooleanPaint"]["rows"][1]["index"] = 0;
        if (change == 5) packet.erase("menuRevision");
        if (change == 6) packet["rows"][0]["value"] = "UNAVAILABLE";
        if (change == 7) packet["isOpen"] = false;
        if (change == 8) packet["menuOpenTransition"] = {{"phase", "rebuild"}};
        Reject([&] { file.Read(packet.dump()); });
        Reject([&] { runtime::ParseState(packet.dump(), GetCurrentProcessId()); });
    }
    auto bytes = Packet().dump(); const auto offset = bytes.find("\"revision\":7");
    bytes.insert(offset, "\"rev\\u0069sion\":7,");
    Reject([&] { file.Read(bytes); }); Reject([&] { runtime::ParseState(bytes, GetCurrentProcessId()); });
}
}
