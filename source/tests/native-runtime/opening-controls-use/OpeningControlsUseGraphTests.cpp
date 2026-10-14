#include "../closed-level-opening/ClosedLevelOpeningGraphSupport.h"
#include <array>
#include <iostream>

namespace iw::overlay::tests::openingControlsUseGraph {
using closedLevelGraph::Read;
using closedLevelGraph::Router;

constexpr std::array PacketNames{
    "binding-before0.json", "binding-select0.json", "binding-wait179.json", "binding-redraw180.json",
    "binding-wait429.json", "binding-ready430.json", "binding-second-select430.json",
    "binding-second-redraw610.json", "binding-second-ready860.json", "binding-third-select860.json",
    "duplicate-melee-before0.json", "duplicate-melee-select0.json", "duplicate-melee-wait179.json",
    "duplicate-melee-redraw180.json", "duplicate-melee-wait429.json", "duplicate-melee-ready430.json",
    "disable-quick-before0.json", "disable-quick-select0.json", "disable-quick-wait179.json",
    "disable-quick-redraw180.json", "disable-quick-wait429.json", "disable-quick-ready430.json",
    "menu-selector-before0.json", "menu-selector-select0.json", "menu-selector-wait249.json",
    "menu-selector-ready250.json", "slot-selector-before0.json", "slot-selector-select0.json",
    "slot-selector-wait249.json", "slot-selector-ready250.json", "changed-slot-before0.json",
    "changed-slot-next-choice0.json", "changed-slot-choice-ready180.json", "changed-slot-select180.json",
    "changed-slot-wait249.json", "changed-slot-ready250.json"};

auto Frame(const std::filesystem::path& directory, const char* name) {
    return Read(directory / name);
}

const MenuRow& Row(const MenuSnapshot& state, std::string_view label) {
    const auto& rows = state.rows;
    const auto found = std::find_if(rows.begin(), rows.end(), [&](const MenuRow& row) { return row.label == label; });
    if (found == rows.end()) {
        std::string actual;
        for (const auto& row : rows) actual += row.label + ",";
        throw std::runtime_error("Actual source row is missing: " + std::string(label) + " in [" + actual + "]");
    }
    return *found;
}

const MenuRow& OpeningRow(const MenuSnapshot& state, OpeningAction action) {
    const auto& rows = state.rows;
    const auto found = std::find_if(rows.begin(), rows.end(),
        [&](const MenuRow& row) { return row.openingAction == action; });
    if (found == rows.end()) throw std::runtime_error("Actual opening action row is missing");
    return *found;
}

const Json& Command(const Json& manifest, std::string_view file) {
    for (const auto& command : manifest.at("commands")) {
        if (command.at("file") == file) return command;
    }
    throw std::runtime_error("Actual command capture is missing");
}

void CheckCapture(const std::filesystem::path& directory, const Json& command) {
    // The capture names the pre-command frame; the command file holds the written acknowledgment
    const auto& capture = command.at("capture");
    const auto observed = Read(directory / capture.at("file").get<std::string>());
    const auto& frame = *observed.snapshot;
    const auto sequence = command.at("sequence").get<std::uint64_t>();
    Check(sequence == observed.lastSequence + 1 && capture.at("lastSequence") == observed.lastSequence,
        "Actual command sequence does not follow its captured acknowledgment");
    const auto openingRevision = runtime::ObservedOpeningControlsRevision(frame);
    Check(openingRevision.has_value(), "Captured source command lacks its opening policy revision");
    Check(capture.at("navigationRevision") == frame.navigationRevision
        && capture.at("openingControlsRevision") == *openingRevision
        && capture.at("selectedSourceIndex") == frame.selectedIndex,
        "Manifest capture does not describe its original controller frame");

    const auto inputName = command.at("input").get<std::string>();
    const auto input = inputName == "select" ? runtime::Input::Select
        : inputName == "nextChoice" ? runtime::Input::NextChoice
        : throw std::runtime_error("Unexpected Open Controls source command");
    std::optional<std::uint64_t> admission;
    std::optional<std::uint64_t> menu;
    if (input == runtime::Input::NextChoice) {
        Check(frame.localMenuEligibility.has_value() && frame.menuRevision.has_value(),
            "Mechanical selector command lacks its captured local row scope");
        admission = frame.localMenuEligibility->revision;
        menu = frame.menuRevision;
    }
    const auto native = Json::parse(runtime::CommandJson(42, sequence, input, frame.navigationRevision,
        runtime::ObservedOpeningControlsRevision(frame), admission, menu));
    const auto& wire = command.at("wire");
    const auto emitted = Read(directory / command.at("file").get<std::string>());
    Check(emitted.lastSequence == sequence && native.at("input") == command.at("input")
        && native.at("sequence") == sequence
        && native.at("navigationRevision") == wire.at("navigationRevision")
        && native.at("openingControlsRevision") == wire.at("openingControlsRevision"),
        "Native command changed the producer's captured Select fields");
    if (input == runtime::Input::Select) {
        Check(wire.at("localAdmissionRevision").is_null() && wire.at("menuRevision").is_null(),
            "Native Select added a local admission or presentation scope");
    } else {
        Check(wire.at("localAdmissionRevision").get<std::uint64_t>() == *admission
            && wire.at("menuRevision").get<std::uint64_t>() == *menu,
            "Native mechanical input dropped its source-captured row scope");
    }
    Check(!native.contains("localAdmissionRevision") == (input == runtime::Input::Select)
        && !native.contains("menuRevision") == (input == runtime::Input::Select),
        "Select serialized a presentation or admission capture");
}

void CheckBooleanInvocation(const runtime::ParsedState& state, std::uint64_t sequence,
    MenuOptionCallbackPhase phase, bool ready = false) {
    const auto& frame = *state.snapshot;
    Check(frame.menuUseContinuation.has_value(), "Actual BOOL callback lost its written Use receipt");
    const auto& receipt = *frame.menuUseContinuation;
    Check(receipt.sequence == sequence && receipt.kind == MenuOptionCallbackKind::Boolean
        && frame.localMenuEligibility && receipt.admissionRevision == frame.localMenuEligibility->revision,
        "BOOL receipt does not retain its captured command and actor");
    if (ready) {
        Check(!frame.menuOptionCallback, "BOOL callback did not retire at its source deadline");
    } else {
        Check(frame.menuOptionCallback && frame.menuOptionCallback->revision == receipt.revision
            && frame.menuOptionCallback->kind == MenuOptionCallbackKind::Boolean
            && frame.menuOptionCallback->phase == phase,
            "BOOL callback phase does not match its actual source interval");
    }
}

void CheckPlainInvocation(const runtime::ParsedState& state, std::uint64_t sequence, bool ready = false) {
    const auto& frame = *state.snapshot;
    Check(frame.menuUseContinuation && frame.menuUseContinuation->sequence == sequence
        && frame.menuUseContinuation->kind == MenuOptionCallbackKind::Plain,
        "Plain callback lost its actual Select invocation");
    if (ready) {
        Check(!frame.menuOptionCallback, "Plain callback did not retire at250ms");
    } else {
        Check(frame.menuOptionCallback && frame.menuOptionCallback->kind == MenuOptionCallbackKind::Plain
            && frame.menuOptionCallback->phase == MenuOptionCallbackPhase::Cooldown
            && frame.menuOptionCallback->revision == frame.menuUseContinuation->revision,
            "Plain callback does not retain its source-owned wait");
    }
}

void ActualPacketsKeepSourceEffectsAndCapturedScopesInBothReaders(const std::filesystem::path& directory) {
    const auto labels = runtime::DescribeOpeningActions(useGraph::Bindings());
    std::set<std::string> expected{"opening-controls-use-manifest.json"}, actual;
    for (const auto* name : PacketNames) expected.insert(name);
    for (const auto& entry : std::filesystem::directory_iterator(directory)) {
        Check(entry.is_regular_file(), "Open Controls graph contains a non-file entry");
        actual.insert(entry.path().filename().string());
    }
    Check(actual == expected, "Open Controls graph has missing or unexpected packet files");

    for (const auto* name : PacketNames) {
        const auto parsed = Read(directory / name);
        const auto preview = ReadBackendPreview(directory / name, &labels);
        const auto& frame = *parsed.snapshot;
        // Both production readers must copy the same owner, callback, policy and surface facts
        Check(frame.localMenuEligibility == preview.localMenuEligibility
            && frame.menuUseContinuation == preview.menuUseContinuation
            && frame.menuOptionCallback == preview.menuOptionCallback
            && frame.mainOpeningControls == preview.mainOpeningControls
            && frame.openingPolicies == preview.openingPolicies
            && frame.menuSurface == preview.menuSurface,
            "Actual callback, policy or owner fields disagree between both readers");
    }

    const auto manifest = Json::parse(useGraph::Bytes(directory / "opening-controls-use-manifest.json"));
    Check(manifest.at("source").at("commit") == "307e94c1b1d6537ba7a2a71b32a22d563d3ea25c"
        && manifest.at("source").at("baseSha256") == "5c92cf211e3d772e196cbc856fb3e0cb9ab46e620ab8b67d1f63ef13c2276d11"
        && manifest.at("source").at("menuCustomizationSha256") == "29767ad03de621b1c1491c46ec769dd352b05654f73452e8034cf320c497f7da"
        && manifest.at("session").at("processMemoryWriterUsed") == false
        && manifest.at("session").at("actionCount") == 0
        && manifest.at("commands").size() == 9 && manifest.at("frames").size() == 36,
        "Open Controls graph changed its pinned source or inert-session inventory");
    for (const auto& command : manifest.at("commands")) CheckCapture(directory, command);

    const auto first = Frame(directory, "binding-select0.json");
    const auto before = Frame(directory, "binding-before0.json");
    const auto seq1 = Command(manifest, "binding-select0.json").at("sequence").get<std::uint64_t>();
    Check(std::find(first.snapshot->mainOpeningControls->actions.begin(),
        first.snapshot->mainOpeningControls->actions.end(), OpeningAction::Use)
        != first.snapshot->mainOpeningControls->actions.end(), "Successful binding did not commit its source policy");
    // The policy commits at Select while its displayed BOOL remains old until the source redraw
    Check(OpeningRow(*before.snapshot, OpeningAction::Use).value == "OFF"
        && OpeningRow(*first.snapshot, OpeningAction::Use).value == "OFF"
        && OpeningRow(*Frame(directory, "binding-wait179.json").snapshot, OpeningAction::Use).value == "OFF"
        && OpeningRow(*Frame(directory, "binding-redraw180.json").snapshot, OpeningAction::Use).value == "ON",
        "Binding effect painted before its actual BOOL redraw");
    CheckBooleanInvocation(first, seq1, MenuOptionCallbackPhase::Pending);
    CheckBooleanInvocation(Frame(directory, "binding-redraw180.json"), seq1, MenuOptionCallbackPhase::Cooldown);
    CheckBooleanInvocation(Frame(directory, "binding-ready430.json"), seq1, MenuOptionCallbackPhase::Cooldown, true);

    const auto duplicate = Frame(directory, "binding-second-select430.json");
    const auto seq2 = Command(manifest, "binding-second-select430.json").at("sequence").get<std::uint64_t>();
    Check(seq2 > seq1 && duplicate.snapshot->notice.find("already assigned") != std::string::npos,
        "Duplicate Use did not preserve its actual rejection and new write");
    CheckBooleanInvocation(duplicate, seq2, MenuOptionCallbackPhase::Pending);
    CheckBooleanInvocation(Frame(directory, "binding-second-redraw610.json"), seq2, MenuOptionCallbackPhase::Cooldown);
    CheckBooleanInvocation(Frame(directory, "binding-second-ready860.json"), seq2, MenuOptionCallbackPhase::Cooldown, true);
    Check(OpeningRow(*Frame(directory, "binding-second-redraw610.json").snapshot,
        OpeningAction::Use).value == "ON",
        "Duplicate binding changed the already-published source row");

    const auto denied = Frame(directory, "duplicate-melee-select0.json");
    const auto meleeSequence = Command(manifest, "duplicate-melee-select0.json").at("sequence").get<std::uint64_t>();
    Check(denied.snapshot->notice.find("already assigned") != std::string::npos,
        "Duplicate Melee did not preserve its source rejection");
    CheckBooleanInvocation(denied, meleeSequence, MenuOptionCallbackPhase::Pending);
    CheckBooleanInvocation(Frame(directory, "duplicate-melee-redraw180.json"), meleeSequence,
        MenuOptionCallbackPhase::Cooldown);
    CheckBooleanInvocation(Frame(directory, "duplicate-melee-ready430.json"), meleeSequence,
        MenuOptionCallbackPhase::Cooldown, true);

    const auto quickBefore = Frame(directory, "disable-quick-before0.json");
    const auto quick = Frame(directory, "disable-quick-select0.json");
    const auto quickSequence = Command(manifest, "disable-quick-select0.json").at("sequence").get<std::uint64_t>();
    Check(!quickBefore.snapshot->openingPolicies->quickMenuDisabled && quick.snapshot->openingPolicies->quickMenuDisabled
        && Row(*quick.snapshot, "Disable Quick Menu").value == "OFF"
        && Row(*Frame(directory, "disable-quick-redraw180.json").snapshot, "Disable Quick Menu").value == "ON",
        "Quick policy did not commit before its source BOOL redraw");
    CheckBooleanInvocation(quick, quickSequence, MenuOptionCallbackPhase::Pending);
    CheckBooleanInvocation(Frame(directory, "disable-quick-redraw180.json"), quickSequence,
        MenuOptionCallbackPhase::Cooldown);
    CheckBooleanInvocation(Frame(directory, "disable-quick-ready430.json"), quickSequence,
        MenuOptionCallbackPhase::Cooldown, true);

    const auto menuBefore = Frame(directory, "menu-selector-before0.json");
    const auto menu = Frame(directory, "menu-selector-select0.json");
    const auto menuSequence = Command(manifest, "menu-selector-select0.json").at("sequence").get<std::uint64_t>();
    Check(menu.snapshot->menuRevision == menuBefore.snapshot->menuRevision
        && Row(*menu.snapshot, "Menu").value == Row(*menuBefore.snapshot, "Menu").value,
        "Selecting the current Main Menu target invented a redraw");
    CheckPlainInvocation(menu, menuSequence);
    CheckPlainInvocation(Frame(directory, "menu-selector-wait249.json"), menuSequence);
    CheckPlainInvocation(Frame(directory, "menu-selector-ready250.json"), menuSequence, true);

    const auto slotBefore = Frame(directory, "slot-selector-before0.json");
    const auto slot = Frame(directory, "slot-selector-select0.json");
    const auto slotSequence = Command(manifest, "slot-selector-select0.json").at("sequence").get<std::uint64_t>();
    // Bind Slot 1 publishes its source redraw during Select while the page remains in place
    Check(Row(*slotBefore.snapshot, "Bind Slot").value == "1"
        && Row(*slot.snapshot, "Bind Slot").value == "1"
        && slot.snapshot->menuRevision == *slotBefore.snapshot->menuRevision + 1,
        "Selecting Bind Slot 1 did not keep its immediate source redraw");
    CheckPlainInvocation(slot, slotSequence);
    CheckPlainInvocation(Frame(directory, "slot-selector-wait249.json"), slotSequence);
    CheckPlainInvocation(Frame(directory, "slot-selector-ready250.json"), slotSequence, true);

    const auto changedChoice = Frame(directory, "changed-slot-choice-ready180.json");
    const auto changed = Frame(directory, "changed-slot-select180.json");
    Check(Row(*changedChoice.snapshot, "Bind Slot").value == "2"
        && Row(*changed.snapshot, "Bind Slot").value == "2"
        && changed.snapshot->navigationRevision > changedChoice.snapshot->navigationRevision
        && Row(*changed.snapshot, "None").label == "None",
        "Changed Bind Slot did not publish its source navigation and row topology");
    // Changing the slot replaces the row graph and retires the receipt from the old slot
    Check(!changed.snapshot->menuUseContinuation,
        "A navigation-changing selector must cancel the prior held Use receipt");
    const auto changedWait = Frame(directory, "changed-slot-wait249.json");
    const auto changedReady = Frame(directory, "changed-slot-ready250.json");
    Check(changed.snapshot->menuOptionCallback && changed.snapshot->menuOptionCallback->kind == MenuOptionCallbackKind::Plain
        && changed.snapshot->menuOptionCallback->phase == MenuOptionCallbackPhase::Cooldown
        && changedWait.snapshot->menuOptionCallback
        && changedWait.snapshot->menuOptionCallback->kind == MenuOptionCallbackKind::Plain
        && changedWait.snapshot->menuOptionCallback->phase == MenuOptionCallbackPhase::Cooldown
        && !changedReady.snapshot->menuOptionCallback,
        "Changed selector did not keep its source plain250 callback phase");
    Check(!changedWait.snapshot->menuUseContinuation && !changedReady.snapshot->menuUseContinuation,
        "A changed selector transferred the canceled invocation into its remaining plain wait");
}

void HeldUseRepeatsOnlyAtTheCapturedBindingInvocationReadiness(const std::filesystem::path& directory) {
    const auto manifest = Json::parse(useGraph::Bytes(directory / "opening-controls-use-manifest.json"));
    Router router;
    router.held = {70};
    Check(router.Poll(Frame(directory, "binding-before0.json"), 0) == runtime::Input::Select,
        "Current held Use did not emit the first source Select");
    CheckCapture(directory, Command(manifest, "binding-select0.json"));
    const auto sequence = Command(manifest, "binding-select0.json").at("sequence").get<std::uint64_t>();
    router.input.CaptureUseDispatch(sequence);
    for (const auto& [name, now] : std::array{
        std::pair{"binding-select0.json", 0U}, std::pair{"binding-wait179.json", 179U},
        std::pair{"binding-redraw180.json", 180U}, std::pair{"binding-wait429.json", 429U}}) {
        Check(!router.Poll(Frame(directory, name), now), "Held Use bypassed its own BOOL callback wait");
    }
    Check(router.Poll(Frame(directory, "binding-ready430.json"), 430) == runtime::Input::Select,
        "Held Use did not resume at its captured BOOL invocation deadline");
    CheckCapture(directory, Command(manifest, "binding-second-select430.json"));
    const auto secondSequence = Command(manifest, "binding-second-select430.json").at("sequence").get<std::uint64_t>();
    router.input.CaptureUseDispatch(secondSequence);
    Check(!router.Poll(Frame(directory, "binding-second-select430.json"), 430),
        "The second Select replayed before its own callback was ready");
    // The repeated Select owns a fresh receipt and completes its own 430 ms callback
    Check(!router.Poll(Frame(directory, "binding-second-redraw610.json"), 610),
        "The second Boolean redraw granted early held input");
    Check(router.Poll(Frame(directory, "binding-second-ready860.json"), 860) == runtime::Input::Select,
        "Held Use did not resume after the second actual 430 ms callback");
    CheckCapture(directory, Command(manifest, "binding-third-select860.json"));
}

void AnUncapturedWriteCannotBorrowAReadyReceipt(const std::filesystem::path& directory) {
    Router router;
    router.held = {70};
    Check(router.Poll(Frame(directory, "binding-before0.json"), 0) == runtime::Input::Select,
        "Uncaptured test did not begin with its actual held Select");
    // Backend acknowledgment alone cannot prove the native writer captured the command
    for (const auto& [name, now] : std::array{
        std::pair{"binding-select0.json", 0U}, std::pair{"binding-wait179.json", 179U},
        std::pair{"binding-redraw180.json", 180U}, std::pair{"binding-wait429.json", 429U},
        std::pair{"binding-ready430.json", 430U}}) {
        Check(!router.Poll(Frame(directory, name), now), "An unrecorded native write borrowed the ready receipt");
    }
}

void ChangedNavigationCancelsTheCapturedUseReceipt(const std::filesystem::path& directory) {
    const auto manifest = Json::parse(useGraph::Bytes(directory / "opening-controls-use-manifest.json"));
    Router router;
    router.held = {70};
    Check(router.Poll(Frame(directory, "changed-slot-choice-ready180.json"), 180) == runtime::Input::Select,
        "Held Use did not select the actual changed-slot target");
    CheckCapture(directory, Command(manifest, "changed-slot-select180.json"));
    const auto command = Command(manifest, "changed-slot-select180.json");
    router.input.CaptureUseDispatch(command.at("sequence").get<std::uint64_t>());
    for (const auto& [name, now] : std::array{
        std::pair{"changed-slot-select180.json", 180U}, std::pair{"changed-slot-wait249.json", 249U},
        std::pair{"changed-slot-ready250.json", 250U}}) {
        Check(!router.Poll(Frame(directory, name), now),
            "Changed navigation reused the Select receipt for a later held Use");
    }
}
}

int main(int argc, char** argv) {
    using namespace iw::overlay::tests::openingControlsUseGraph;
    if (argc != 2) return 2;
    int failed = 0;
    const std::filesystem::path directory(argv[1]);
    for (const auto& [name, run] : std::array{
        std::pair{"ActualPacketsKeepSourceEffectsAndCapturedScopesInBothReaders", ActualPacketsKeepSourceEffectsAndCapturedScopesInBothReaders},
        std::pair{"HeldUseRepeatsOnlyAtTheCapturedBindingInvocationReadiness", HeldUseRepeatsOnlyAtTheCapturedBindingInvocationReadiness},
        std::pair{"AnUncapturedWriteCannotBorrowAReadyReceipt", AnUncapturedWriteCannotBorrowAReadyReceipt},
        std::pair{"ChangedNavigationCancelsTheCapturedUseReceipt", ChangedNavigationCancelsTheCapturedUseReceipt}}) {
        try { run(directory); std::cout << "PASS " << name << '\n'; }
        catch (const std::exception& error) { ++failed; std::cout << "FAIL " << name << ": " << error.what() << '\n'; }
    }
    std::cout << 4 - failed << "/4 actual Open Controls Use graph checks passed\n";
    return failed ? 1 : 0;
}
