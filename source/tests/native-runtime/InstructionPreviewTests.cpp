#include "TestSupport.h"
#include "../native-layout/BackendPreview.h"
#include <array>
#include <fstream>
#include <random>
#include <limits>

namespace iw::overlay::tests {
namespace {
class OwnedPreviewFile {
public:
    OwnedPreviewFile() {
        std::random_device random;
        // Reserve a private test directory without reusing an existing path
        for (int attempt = 0; attempt < 32; ++attempt) {
            auto candidate = std::filesystem::temp_directory_path()
                / ("iw-instruction-preview-" + std::to_string(random()) + "-" + std::to_string(random()));
            if (std::filesystem::create_directory(candidate)) {
                directory_ = std::move(candidate);
                std::error_code error;
                std::filesystem::permissions(directory_, std::filesystem::perms::owner_all, error);
                if (error) {
                    std::error_code ignored;
                    std::filesystem::remove_all(directory_, ignored);
                    throw std::runtime_error("Could not protect the instruction preview fixture directory");
                }
                return;
            }
        }
        throw std::runtime_error("Could not reserve the instruction preview fixture directory");
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
            if (!output) throw std::runtime_error("Could not write the instruction preview fixture");
        }
        return ReadBackendPreview(path);
    }
private:
    std::filesystem::path directory_;
};

Json PreviewAppearance() {
    return {{"booleanDisplay", "Boxes"}, {"boolBoxLocation", "Right"},
        {"instructionsVisible", true}, {"optionCounter", false}, {"coloredCursor", true},
        {"spotlightCursor", true}, {"largeCursor", false}};
}
}

void InstructionPreviewMatchesRuntimeDefaultsAndChoices() {
    OwnedPreviewFile fixture;
    auto source = StateJson();
    const auto compare = [&] {
        const auto runtime = runtime::ParseState(source.dump(), GetCurrentProcessId());
        Check(fixture.Read(source).appearance == runtime.snapshot->appearance,
            "CPU preview preferences differ from the real runtime parser");
    };
    compare();
    source["appearance"] = nullptr;
    compare();
    source["appearance"] = PreviewAppearance();
    compare();
    for (bool alternate : {false, true}) {
        for (bool background : {false, true}) {
            for (bool adaptive : {false, true}) {
                if (alternate && adaptive) continue;
                // Real files carry all six supported instruction combinations through both readers
                source["appearance"]["alternateInstructions"] = alternate;
                source["appearance"]["instructionsBackground"] = background;
                source["appearance"]["adaptiveInstructions"] = adaptive;
                compare();
            }
        }
    }
}

void InstructionPreviewRejectsMalformedOptionsAndStyleConflict() {
    OwnedPreviewFile fixture;
    constexpr std::array fields{"alternateInstructions", "instructionsBackground", "adaptiveInstructions"};
    const std::array<Json, 6> invalid{nullptr, "true", 0, 1, Json::array(), Json::object()};
    for (const auto* field : fields) {
        for (const auto& value : invalid) {
            auto source = StateJson();
            source["appearance"] = PreviewAppearance();
            source["appearance"][field] = value;
            Reject([&] { fixture.Read(source); });
        }
    }
    // The helper cannot produce a preview that the production schema would reject
    for (bool background : {false, true}) {
        auto source = StateJson();
        source["appearance"] = PreviewAppearance();
        source["appearance"]["alternateInstructions"] = true;
        source["appearance"]["adaptiveInstructions"] = true;
        source["appearance"]["instructionsBackground"] = background;
        Reject([&] { fixture.Read(source); });
    }
}

void PositionPreviewMatchesRuntimeCoordinatesAndEditorPhases() {
    OwnedPreviewFile fixture;
    auto source = StateJson(); source["appearance"] = PreviewAppearance();
    for (const Json& position : {Json(nullptr), Json{{"x", -2147483648LL}, {"y", 2147483647}},
        Json{{"x", 263}, {"y", 480}}}) {
        source["appearance"]["instructionPosition"] = position;
        Check(fixture.Read(source).appearance == runtime::ParseState(source.dump(), GetCurrentProcessId()).snapshot->appearance,
            "CPU preview position differs from the real signed-coordinate parser");
    }
    source["isOpen"] = true; source["isRootMenu"] = false; source["rows"] = Json::array();
    for (const auto* phase : {"entering", "editing", "exiting"}) {
        source["instructionPositionEditor"] = {{"phase", phase}};
        const auto parsed = runtime::ParseState(source.dump(), GetCurrentProcessId()).snapshot;
        Check(fixture.Read(source).instructionPositionEditor == parsed->instructionPositionEditor,
            "CPU preview editor phase or generic help differs from the real parser");
    }
}

void PositionPreviewRejectsInvalidCoordinatesContextsAndDecodedDuplicates() {
    OwnedPreviewFile fixture;
    auto source = StateJson(); source["appearance"] = PreviewAppearance();
    for (const Json& position : {Json{{"x", 2147483648ULL}, {"y", 1}}, Json{{"x", -2147483649LL}, {"y", 1}},
        Json{{"x", nullptr}, {"y", 1}}, Json{{"x", "1"}, {"y", 1}}, Json{{"x", 1.0}, {"y", 1}},
        Json{{"x", 1}}, Json{{"x", 1}, {"y", 1}, {"z", 1}}, Json::array()}) {
        source["appearance"]["instructionPosition"] = position;
        Reject([&] { fixture.Read(source); });
    }
    source["appearance"]["instructionPosition"] = {{"x", 1}, {"y", 2}};
    std::string bytes = source.dump();
    const auto start = bytes.find("{\"x\":1,\"y\":2}");
    Check(start != std::string::npos, "Preview duplicate fixture lost its coordinate object");
    bytes.replace(start, 13, "{\"x\":1,\"\\u0078\":2,\"y\":2}");
    Reject([&] { fixture.ReadBytes(bytes); });
    source["isOpen"] = true; source["isRootMenu"] = false; source["rows"] = Json::array();
    source["instructionPositionEditor"] = {{"phase", "editing"}};
    for (int conflict = 0; conflict < 5; ++conflict) {
        auto malformed = source;
        if (conflict == 0) malformed["instructionPositionEditor"]["phase"] = "unknown";
        if (conflict == 1) malformed["appearance"]["instructionsVisible"] = false;
        if (conflict == 2) malformed["widthEditor"] = {{"step", 1}};
        if (conflict == 3) malformed["menuUnlock"] = "pause";
        if (conflict == 4) malformed["rows"] = StateJson().at("rows");
        Reject([&] { fixture.Read(malformed); });
    }
}

void ScrollPreviewMatchesRuntimeDurationAndMenuRevisionPresence() {
    OwnedPreviewFile fixture;
    auto source = StateJson();
    Check(!fixture.Read(source).menuRevision && !fixture.Read(source).presentationFocusRevision,
        "Legacy preview invented animation identity");
    source["appearance"] = PreviewAppearance();
    for (int duration : {100, 120, 250}) {
        source["appearance"]["scrollAnimationMilliseconds"] = duration;
        for (auto revision : {std::uint64_t{0}, std::numeric_limits<std::uint64_t>::max()}) {
            source["menuRevision"] = revision;
            source["presentationFocusRevision"] = 99;
            const auto parsed = runtime::ParseState(source.dump(), GetCurrentProcessId()).snapshot;
            const auto preview = fixture.Read(source);
            // Preview never accepts the worker's private epoch from a producer field
            Check(preview.appearance == parsed->appearance && preview.menuRevision == revision
                && !preview.presentationFocusRevision, "Preview timing or identity differs from production");
        }
    }
}
void ScrollPreviewRejectsMalformedDurationAndMenuRevision() {
    OwnedPreviewFile fixture;
    for (const Json& duration : {Json(nullptr), Json(true), Json("120"), Json(120.0), Json(-1),
        Json(99), Json(101), Json(251), Json(std::numeric_limits<std::uint64_t>::max()), Json::array(), Json::object()}) {
        auto source = StateJson(); source["appearance"] = PreviewAppearance();
        source["appearance"]["scrollAnimationMilliseconds"] = duration;
        Reject([&] { fixture.Read(source); });
    }
    for (const Json& revision : {Json(nullptr), Json(true), Json(-1), Json("1"), Json(1.0), Json::array(), Json::object()}) {
        auto source = StateJson(); source["menuRevision"] = revision;
        Reject([&] { fixture.Read(source); });
    }
    // Duplicate decoded fields cannot make the CPU preview weaker than the private parser
    std::string bytes = StateJson().dump();
    bytes.insert(1, "\"menuRevision\":0,\"menu\\u0052evision\":1,");
    Reject([&] { fixture.ReadBytes(bytes); });
}

}
