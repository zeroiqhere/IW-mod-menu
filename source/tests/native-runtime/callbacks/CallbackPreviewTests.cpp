#include "CallbackTestModels.h"
#include "../../native-layout/BackendPreview.h"
#include <fstream>
#include <random>

namespace iw::overlay::tests::callback {
namespace {
class PreviewFile {
public:
    PreviewFile() {
        std::random_device random;
        for (int attempt = 0; attempt < 32; ++attempt) {
            auto candidate = std::filesystem::temp_directory_path() / ("iw-callback-preview-" + std::to_string(random()));
            if (std::filesystem::create_directory(candidate)) {
                directory_ = std::move(candidate);
                std::filesystem::permissions(directory_, std::filesystem::perms::owner_all);
                return;
            }
        }
        throw std::runtime_error("Could not reserve callback preview directory");
    }
    PreviewFile(const PreviewFile&) = delete;
    PreviewFile& operator=(const PreviewFile&) = delete;
    ~PreviewFile() {
        // Cleanup cannot replace the original regression failure
        std::error_code ignored;
        std::filesystem::remove_all(directory_, ignored);
    }
    MenuSnapshot Read(const std::string& bytes) const {
        const auto path = directory_ / "state.json";
        { std::ofstream output(path, std::ios::binary); output << bytes;
          if (!output) throw std::runtime_error("Could not write callback preview fixture"); }
        return ReadBackendPreview(path);
    }
private:
    std::filesystem::path directory_;
};
}
void PreviewOwnsExactCallbacksAndLegacyWithoutChangingRows() {
    PreviewFile file;
    for (int phase = 0; phase < 4; ++phase) {
        auto packet = Packet(phase == 3 ? "plain" : "boolean", phase >= 2 ? "cooldown" : "pending");
        if (phase == 0) packet.erase("menuOptionCallback");
        packet["menuSurface"] = phase % 2 ? "quick" : "main";
        const auto parsed = runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot;
        const auto preview = file.Read(packet.dump());
        Check(preview.menuOptionCallback == parsed->menuOptionCallback
            && preview.menuOptionCallback.has_value() == (phase != 0)
            && preview.menuSurface == parsed->menuSurface && preview.navigationRevision == 12
            && preview.menuRevision == 6 && preview.rows[0].value == "OFF" && preview.rows[1].value == "UNKNOWN",
            "Preview discarded callback ownership or fabricated a game result");
    }
}
void PreviewRejectsMalformedCallbacksContextsAndOpeningConflicts() {
    PreviewFile file;
    for (int change = 0; change < 8; ++change) {
        auto packet = Packet();
        if (change == 0) packet["menuOptionCallback"] = nullptr;
        if (change == 1) packet["menuOptionCallback"]["revision"] = 0;
        if (change == 2) packet["menuOptionCallback"]["kind"] = "Boolean";
        if (change == 3) packet["menuOptionCallback"]["extra"] = true;
        if (change == 4) packet["menuOptionCallback"].erase("phase");
        if (change == 5) packet["isOpen"] = false;
        if (change == 6) packet["menuPreferenceRefresh"] = {{"phase", "pending"}};
        if (change == 7) { packet["menuOptionCallback"]["phase"] = "cooldown";
            packet["menuOpening"] = {{"revision", 2}, {"elapsedMilliseconds", 99}}; }
        Reject([&] { file.Read(packet.dump()); });
    }
    auto bytes = Packet().dump(); const auto offset = bytes.find("\"revision\":7");
    bytes.insert(offset, "\"\\u0072evision\":8,");
    Reject([&] { file.Read(bytes); });
    for (const auto* kind : {"boolean", "plain"}) {
        auto packet = Packet(kind, std::string(kind) == "boolean" ? "pending" : "cooldown");
        packet["menuOpening"] = {{"revision", 2}, {"elapsedMilliseconds", 99}};
        Check(file.Read(packet.dump()).menuOpening == MenuOpening{2, 99}, "Preview dropped a permitted existing opening");
    }
}
}
