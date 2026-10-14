#include "../closed-level-opening/ClosedLevelOpeningGraphSupport.h"
#include <array>
#include <iostream>
#include <string_view>

namespace iw::overlay::tests::rejectedPreferencesGraph {
using closedLevelGraph::Read;
using closedLevelGraph::Router;

struct RejectionCase {
    const char* prefix;
    const char* row;
    MenuDesign design;
    bool stealth;
};

constexpr std::array Cases{
    RejectionCase{"stealth-counter", "Option Counter", MenuDesign::Apparition, true},
    RejectionCase{"aio-counter", "Option Counter", MenuDesign::Aio, false},
    RejectionCase{"basic-counter", "Option Counter", MenuDesign::Basic, false},
    RejectionCase{"classic-colored", "Colored Cursor", MenuDesign::Classic, false},
    RejectionCase{"native-colored", "Colored Cursor", MenuDesign::Native, false}};
constexpr std::array RejectionStages{"before", "select0", "wait179", "redraw180", "wait429", "ready430"};
constexpr std::array StealthRepeatStages{"second-select430", "second-wait609", "second-redraw610",
    "second-wait859", "second-ready860"};
constexpr std::array RebuildStages{"before", "select0", "wait49", "redraw50", "wait249", "ready250"};

std::string Name(const char* prefix, const char* stage) {
    return std::string(prefix) + "-" + stage + ".json";
}

auto Frame(const std::filesystem::path& directory, const char* prefix, const char* stage) {
    return Read(directory / Name(prefix, stage));
}

const MenuRow& Row(const MenuSnapshot& state, std::string_view label) {
    const auto found = std::find_if(state.rows.begin(), state.rows.end(),
        [&](const MenuRow& row) { return row.label == label; });
    if (found == state.rows.end()) throw std::runtime_error("Actual rejected preference row is missing");
    return *found;
}

const Json& Command(const Json& manifest, std::string_view file) {
    for (const auto& command : manifest.at("commands")) {
        if (command.at("file") == file) return command;
    }
    throw std::runtime_error("Actual rejected preference command capture is missing");
}

void CheckCapturedSelect(const std::filesystem::path& directory, const Json& command) {
    const auto& capture = command.at("capture");
    const auto observed = Read(directory / capture.at("file").get<std::string>());
    const auto& frame = *observed.snapshot;
    const auto sequence = command.at("sequence").get<std::uint64_t>();
    const auto openingRevision = runtime::ObservedOpeningControlsRevision(frame);
    Check(command.at("input") == "select" && sequence == observed.lastSequence + 1
        && capture.at("lastSequence") == observed.lastSequence,
        "Actual rejected preference Select does not follow its captured acknowledgment");
    Check(openingRevision && capture.at("navigationRevision") == frame.navigationRevision
        && capture.at("openingControlsRevision") == *openingRevision
        && capture.at("selectedSourceIndex") == frame.selectedIndex,
        "Actual Select manifest does not describe its captured source frame");
    if (capture.contains("selectedSourceLabel")) {
        Check(capture.at("selectedSourceLabel") == frame.rows.at(frame.selectedIndex).label,
            "Actual Select capture changed the selected source row");
    }

    // Select contains only the command scopes read from its original controller frame
    const auto native = Json::parse(runtime::CommandJson(42, sequence, runtime::Input::Select,
        frame.navigationRevision, openingRevision));
    const auto& wire = command.at("wire");
    const auto emitted = Read(directory / command.at("file").get<std::string>());
    Check(native.size() == 6 && native.at("sequence") == sequence && native.at("input") == "select"
        && native.at("navigationRevision") == wire.at("navigationRevision")
        && native.at("openingControlsRevision") == wire.at("openingControlsRevision")
        && wire.at("localAdmissionRevision").is_null() && wire.at("menuRevision").is_null()
        && !native.contains("localAdmissionRevision") && !native.contains("menuRevision")
        && emitted.lastSequence == sequence,
        "Native Select borrowed a row-local or presentation scope from its source callback");
}

void CheckBooleanRejectedInvocation(const runtime::ParsedState& parsed, std::uint64_t sequence,
    MenuOptionCallbackPhase phase, bool ready = false) {
    const auto& frame = *parsed.snapshot;
    Check(frame.menuUseContinuation.has_value(), "Rejected source BOOL lost its written Use receipt");
    const auto& receipt = *frame.menuUseContinuation;
    Check(receipt.sequence == sequence && receipt.kind == MenuOptionCallbackKind::Boolean
        && frame.localMenuEligibility && receipt.admissionRevision == frame.localMenuEligibility->revision,
        "Rejected source BOOL receipt lost its actual Select or actor owner");
    Check(!frame.menuPreferenceRefresh && !frame.menuPreferenceRebuild,
        "Rejected typed BOOL acquired a competing preference timer");
    if (ready) {
        Check(!frame.menuOptionCallback, "Rejected source BOOL did not become ready at430ms");
    } else {
        Check(frame.menuOptionCallback && frame.menuOptionCallback->revision == receipt.revision
            && frame.menuOptionCallback->kind == MenuOptionCallbackKind::Boolean
            && frame.menuOptionCallback->phase == phase,
            "Rejected BOOL callback phase lost its captured Select owner");
    }
}

void ActualRejectedRowsRemainSelectableAndBothReadersPreserveTheirSourceCallback(
    const std::filesystem::path& directory) {
    const auto labels = runtime::DescribeOpeningActions(useGraph::Bindings());
    std::set<std::string> expected{"rejected-preferences-manifest.json"}, actual;
    for (const auto& profile : Cases) {
        for (const auto* stage : RejectionStages) expected.insert(Name(profile.prefix, stage));
    }
    for (const auto* stage : StealthRepeatStages) expected.insert(Name("stealth-counter", stage));
    for (const auto* stage : RebuildStages) expected.insert(Name("counter-rebuild", stage));
    for (const auto& entry : std::filesystem::directory_iterator(directory)) {
        Check(entry.is_regular_file(), "Rejected preference graph contains a non-file entry");
        actual.insert(entry.path().filename().string());
    }
    Check(actual == expected, "Rejected preference graph has missing or unexpected packet files");

    // The two production readers must retain identical row, actor and callback facts
    for (const auto& file : expected) {
        if (file == "rejected-preferences-manifest.json") continue;
        const auto parsed = Read(directory / file);
        const auto preview = ReadBackendPreview(directory / file, &labels);
        const auto& frame = *parsed.snapshot;
        Check(frame.rows.size() == preview.rows.size()
            && frame.localMenuEligibility == preview.localMenuEligibility
            && frame.menuUseContinuation == preview.menuUseContinuation
            && frame.menuOptionCallback == preview.menuOptionCallback
            && frame.menuPreferenceRebuild == preview.menuPreferenceRebuild
            && frame.menuPreferenceRefresh == preview.menuPreferenceRefresh
            && frame.openingPolicies == preview.openingPolicies
            && frame.mainOpeningControls == preview.mainOpeningControls
            && frame.menuSurface == preview.menuSurface,
            "Actual rejected callback ownership differs between production readers");
        for (std::size_t row = 0; row < frame.rows.size(); ++row) {
            Check(frame.rows[row].label == preview.rows[row].label
                && frame.rows[row].value == preview.rows[row].value
                && frame.rows[row].enabled == preview.rows[row].enabled,
                "A production reader changed an actual rejected preference row");
        }
    }

    const auto manifest = Json::parse(useGraph::Bytes(directory / "rejected-preferences-manifest.json"));
    Check(manifest.at("graph") == "rejected-preferences"
        && manifest.at("session").at("processMemoryWriterUsed") == false
        && manifest.at("session").at("actionCount") == 0
        && manifest.at("frames").size() == 41 && manifest.at("commands").size() == 7,
        "Actual rejected preference graph changed its inert session or packet inventory");
    for (const auto& command : manifest.at("commands")) CheckCapturedSelect(directory, command);

    for (const auto& profile : Cases) {
        const auto before = Frame(directory, profile.prefix, "before");
        const auto select = Frame(directory, profile.prefix, "select0");
        const auto early = Frame(directory, profile.prefix, "wait179");
        const auto drawn = Frame(directory, profile.prefix, "redraw180");
        const auto wait = Frame(directory, profile.prefix, "wait429");
        const auto ready = Frame(directory, profile.prefix, "ready430");
        const auto& sourceRow = Row(*before.snapshot, profile.row);
        Check(before.snapshot->selectedIndex < before.snapshot->rows.size()
            && before.snapshot->rows[before.snapshot->selectedIndex].label == profile.row
            && sourceRow.enabled && (sourceRow.value == "ON" || sourceRow.value == "OFF"),
            "A source-rejected preference row was not selectable before its callback");
        Check(before.snapshot->appearance.design == profile.design
            && before.snapshot->appearance.stealthUI == profile.stealth,
            "Actual rejected profile did not preserve its selected design or Stealth state");

        // Rejection leaves the value and applied appearance intact while the real BOOL wait runs
        for (const auto& state : {select, early, drawn, wait, ready}) {
            Check(state.snapshot->appearance == before.snapshot->appearance
                && Row(*state.snapshot, profile.row).value == sourceRow.value
                && Row(*state.snapshot, profile.row).enabled
                && state.snapshot->navigationRevision == before.snapshot->navigationRevision
                && state.snapshot->selectedIndex == before.snapshot->selectedIndex,
                "Rejected preference changed its committed value or source row scope");
        }
        const auto& selectedCommand = Command(manifest, Name(profile.prefix, "select0"));
        const auto sequence = selectedCommand.at("sequence").get<std::uint64_t>();
        CheckBooleanRejectedInvocation(select, sequence, MenuOptionCallbackPhase::Pending);
        CheckBooleanRejectedInvocation(early, sequence, MenuOptionCallbackPhase::Pending);
        CheckBooleanRejectedInvocation(drawn, sequence, MenuOptionCallbackPhase::Cooldown);
        CheckBooleanRejectedInvocation(wait, sequence, MenuOptionCallbackPhase::Cooldown);
        CheckBooleanRejectedInvocation(ready, sequence, MenuOptionCallbackPhase::Cooldown, true);
        Check(early.snapshot->menuRevision == before.snapshot->menuRevision
            && drawn.snapshot->menuRevision == *before.snapshot->menuRevision + 1,
            "Rejected BOOL redraw changed presentation at the wrong source time");
    }

    const auto beforeRebuild = Frame(directory, "counter-rebuild", "before");
    const auto rebuild = Frame(directory, "counter-rebuild", "select0");
    const auto wait49 = Frame(directory, "counter-rebuild", "wait49");
    const auto redraw50 = Frame(directory, "counter-rebuild", "redraw50");
    const auto wait249 = Frame(directory, "counter-rebuild", "wait249");
    const auto ready250 = Frame(directory, "counter-rebuild", "ready250");
    Check(Row(*beforeRebuild.snapshot, "Option Counter").enabled
        && beforeRebuild.snapshot->selectedIndex < beforeRebuild.snapshot->rows.size()
        && beforeRebuild.snapshot->rows[beforeRebuild.snapshot->selectedIndex].label == "Option Counter"
        && !rebuild.snapshot->menuOptionCallback && !rebuild.snapshot->menuUseContinuation
        && wait49.snapshot->menuPreferenceRebuild == MenuPreferenceRebuildPhase::Rebuild
        && redraw50.snapshot->menuPreferenceRebuild == MenuPreferenceRebuildPhase::Cooldown
        && wait249.snapshot->menuPreferenceRebuild == MenuPreferenceRebuildPhase::Cooldown
        && !ready250.snapshot->menuPreferenceRebuild
        && redraw50.snapshot->menuRevision == *beforeRebuild.snapshot->menuRevision + 1,
        "Supported counter change did not retain its ordinary 50+200 ms rebuild");
}

void HeldRejectedUseRepeatsOnlyAfterItsCapturedBooleanReceiptIsReady(const std::filesystem::path& directory) {
    const auto manifest = Json::parse(useGraph::Bytes(directory / "rejected-preferences-manifest.json"));
    Router router;
    router.held = {70};
    Check(router.Poll(Frame(directory, "stealth-counter", "before"), 0) == runtime::Input::Select,
        "Held Use did not dispatch the actual selectable Stealth counter row");
    const auto& first = Command(manifest, Name("stealth-counter", "select0"));
    CheckCapturedSelect(directory, first);
    router.input.CaptureUseDispatch(first.at("sequence").get<std::uint64_t>());
    for (const auto& [stage, now] : std::array{
        std::pair{"select0", 0U}, std::pair{"wait179", 179U}, std::pair{"redraw180", 180U},
        std::pair{"wait429", 429U}}) {
        Check(!router.Poll(Frame(directory, "stealth-counter", stage), now),
            "Held rejected Use skipped its 430 ms source callback");
    }
    Check(router.Poll(Frame(directory, "stealth-counter", "ready430"), 430) == runtime::Input::Select,
        "Held rejected Use did not resume when its actual callback ended");

    const auto& second = Command(manifest, Name("stealth-counter", "second-select430"));
    CheckCapturedSelect(directory, second);
    router.input.CaptureUseDispatch(second.at("sequence").get<std::uint64_t>());
    for (const auto& [stage, now] : std::array{
        std::pair{"second-select430", 430U}, std::pair{"second-wait609", 609U},
        std::pair{"second-redraw610", 610U}, std::pair{"second-wait859", 859U}}) {
        Check(!router.Poll(Frame(directory, "stealth-counter", stage), now),
            "The repeated rejected Use skipped its own 430 ms callback");
    }
    Check(router.Poll(Frame(directory, "stealth-counter", "second-ready860"), 860) == runtime::Input::Select,
        "The repeated rejected Use did not resume at its own ready receipt");
}

void UncapturedRejectedSelectCannotBorrowItsReadyReceipt(const std::filesystem::path& directory) {
    Router router;
    router.held = {70};
    Check(router.Poll(Frame(directory, "stealth-counter", "before"), 0) == runtime::Input::Select,
        "Uncaptured graph did not begin with an actual Select");
    for (const auto& [stage, now] : std::array{
        std::pair{"select0", 0U}, std::pair{"wait179", 179U}, std::pair{"redraw180", 180U},
        std::pair{"wait429", 429U}, std::pair{"ready430", 430U}}) {
        Check(!router.Poll(Frame(directory, "stealth-counter", stage), now),
            "Acknowledged source work borrowed a Select receipt the native writer never captured");
    }
}
}

int main(int argc, char** argv) {
    using namespace iw::overlay::tests::rejectedPreferencesGraph;
    if (argc != 2) return 2;
    int failed = 0;
    const std::filesystem::path directory(argv[1]);
    for (const auto& [name, run] : std::array{
        std::pair{"ActualRejectedRowsRemainSelectableAndBothReadersPreserveTheirSourceCallback",
            ActualRejectedRowsRemainSelectableAndBothReadersPreserveTheirSourceCallback},
        std::pair{"HeldRejectedUseRepeatsOnlyAfterItsCapturedBooleanReceiptIsReady",
            HeldRejectedUseRepeatsOnlyAfterItsCapturedBooleanReceiptIsReady},
        std::pair{"UncapturedRejectedSelectCannotBorrowItsReadyReceipt",
            UncapturedRejectedSelectCannotBorrowItsReadyReceipt}}) {
        try { run(directory); std::cout << "PASS " << name << '\n'; }
        catch (const std::exception& error) { ++failed; std::cout << "FAIL " << name << ": " << error.what() << '\n'; }
    }
    std::cout << 3 - failed << "/3 actual rejected preference graph checks passed\n";
    return failed ? 1 : 0;
}
