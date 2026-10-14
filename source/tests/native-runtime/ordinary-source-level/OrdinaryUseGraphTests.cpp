#include "../closed-level-opening/ClosedLevelOpeningGraphSupport.h"
#include <array>
#include <iostream>

namespace iw::overlay::tests::ordinaryUseGraph {
using closedLevelGraph::Read;
using closedLevelGraph::Router;

void ActualGodmodeResultAndPaintKeepTheirSeparateDeadlines(const std::filesystem::path& directory) {
    const auto before = Read(directory / "quick-ready550.json");
    const auto pending = Read(directory / "quick-use-operation-pending550.json");
    const auto effect = Read(directory / "quick-use-effect550.json");
    const auto refresh = Read(directory / "quick-use-refresh730.json");
    const auto ready = Read(directory / "quick-use-ready980.json");
    // Session completion changes the result before the monitor redraw changes its retained paint
    Check(before.snapshot->rows[0].label == "God Mode" && before.snapshot->rows[0].value == "OFF"
        && before.snapshot->rows[0].enabled, "Actual local God Mode was not available before Select");
    Check(pending.snapshot->rows[0].value == "UNKNOWN" && !pending.snapshot->rows[0].enabled,
        "Pending session work claimed a completed God Mode result");
    Check(effect.snapshot->rows[0].value == "ON" && !effect.snapshot->rows[0].enabled,
        "Actual completed session result was absent or prematurely enabled");
    Check(effect.snapshot->menuBooleanPaint == before.snapshot->menuBooleanPaint,
        "Session completion fabricated an early BOOL paint refresh");
    Check(refresh.snapshot->menuRevision == 4 && refresh.snapshot->menuBooleanPaint->revision == 2
        && refresh.snapshot->menuBooleanPaint->rows[0].value == MenuBooleanPaintValue::On,
        "The 180 ms source BOOL redraw did not paint the completed result");
    Check(refresh.snapshot->menuOptionCallback == MenuOptionCallback{1, MenuOptionCallbackKind::Boolean,
        MenuOptionCallbackPhase::Cooldown}, "Redraw skipped the remaining BOOL caller wait");
    Check(!ready.snapshot->menuOptionCallback && ready.snapshot->rows[0].enabled
        && ready.snapshot->menuUseContinuation == MenuUseContinuation{1, 2, 10, MenuOptionCallbackKind::Boolean},
        "The 430 ms source callback did not retain its exact invocation receipt");
    // Literal membership bounds this graph separately from the older opening inventory
    const std::array names{"quick-before0.json", "quick-open-pending0.json", "quick-drawn0.json",
        "quick-wait549.json", "quick-ready550.json", "quick-use-command550.json",
        "quick-use-operation-pending550.json", "quick-use-effect550.json", "quick-use-refresh730.json",
        "quick-use-before-ready979.json", "quick-use-ready980.json", "source-level-manifest.json"};
    std::set<std::string> actual, wanted;
    const auto labels = runtime::DescribeOpeningActions(useGraph::Bindings());
    for (const auto& entry : std::filesystem::directory_iterator(directory)) {
        Check(entry.is_regular_file(), "Actual ordinary graph contains a non-file entry");
        actual.insert(entry.path().filename().string());
    }
    for (const auto* name : names) {
        wanted.insert(name);
        if (std::string_view(name) == "source-level-manifest.json") continue;
        const auto frame = Read(directory / name);
        const auto preview = ReadBackendPreview(directory / name, &labels);
        Check(frame.snapshot->menuBooleanPaint == preview.menuBooleanPaint
            && frame.snapshot->menuUseContinuation == preview.menuUseContinuation
            && frame.snapshot->localMenuEligibility == preview.localMenuEligibility,
            "Production readers lost actual result, paint or invocation scope");
    }
    Check(actual == wanted, "Actual ordinary graph contains missing or unexpected packets");
    const auto manifest = Json::parse(useGraph::Bytes(directory / "source-level-manifest.json"));
    Check(manifest.at("source").at("commit") == "307e94c1b1d6537ba7a2a71b32a22d563d3ea25c"
        && manifest.at("session").at("processMemoryWriterUsed") == false,
        "Actual graph changed its source reference or used a live writer");
    const auto& frames = manifest.at("frames");
    Check(frames.size() == 11 && frames[6].at("sessionActions") == Json::array({"TogglePersonalGodmode"})
        && frames[7].at("sessionActions") == Json::array({"TogglePersonalGodmode"}),
        "Result and pending frames were not produced by the one actual session action");
}

Router OpenQuickWithUseHeld(const std::filesystem::path& directory) {
    Router router;
    router.held = {2, 69};
    Check(router.Poll(Read(directory / "quick-before0.json"), 0) == runtime::Input::OpenQuick,
        "Actual current chord did not open Quick");
    router.pending = true;
    Check(!router.Poll(Read(directory / "quick-open-pending0.json"), 0), "Pending open duplicated input");
    // Use starts during the locked opening and is never released at readiness
    router.pending = false;
    router.held = {70};
    Check(!router.Poll(Read(directory / "quick-drawn0.json"), 0), "Quick draw admitted Use early");
    Check(!router.Poll(Read(directory / "quick-wait549.json"), 549), "Quick wait ended before 550");
    Check(router.Poll(Read(directory / "quick-ready550.json"), 550) == runtime::Input::Select,
        "Held Use was discarded at actual Quick readiness");
    return router;
}

void HeldUseJoinsTheActualLocalActionAndRepeatsOnlyAtItsOwnedReadiness(const std::filesystem::path& directory) {
    auto router = OpenQuickWithUseHeld(directory);
    const auto before = Read(directory / "quick-ready550.json");
    const auto command = Json::parse(runtime::CommandJson(42, 2, runtime::Input::Select,
        before.snapshot->navigationRevision, runtime::ObservedOpeningControlsRevision(*before.snapshot)));
    const auto manifest = Json::parse(useGraph::Bytes(directory / "source-level-manifest.json"));
    const auto& wire = manifest.at("commands")[1].at("wire");
    Check(command.at("input") == "select" && command.at("sequence") == 2
        && command.at("navigationRevision") == wire.at("navigationRevision")
        && command.at("openingControlsRevision") == wire.at("openingControlsRevision")
        && !command.contains("localAdmissionRevision") && !command.contains("menuRevision"),
        "Native Select changed the actual producer's captured command fields");
    router.input.CaptureUseDispatch(2);
    router.pending = true;
    Check(!router.Poll(Read(directory / "quick-use-command550.json"), 550), "Pending Select replayed a held level");
    router.pending = false;
    for (const auto& [name, now] : std::array{
        std::pair{"quick-use-operation-pending550.json", 550U}, std::pair{"quick-use-effect550.json", 550U},
        std::pair{"quick-use-refresh730.json", 730U}, std::pair{"quick-use-before-ready979.json", 979U}}) {
        Check(!router.Poll(Read(directory / name), now), "Held Use bypassed actual session or BOOL wait");
    }
    Check(router.Poll(Read(directory / "quick-use-ready980.json"), 980) == runtime::Input::Select,
        "Exact owned BOOL readiness did not resume its current held Use");
    Check(!router.Poll(Read(directory / "quick-use-ready980.json"), 980),
        "One readiness receipt admitted a second Select in the same monitor cycle");
}

void AnUncapturedWriteCannotBorrowTheActualReadyReceipt(const std::filesystem::path& directory) {
    auto router = OpenQuickWithUseHeld(directory);
    // The same immutable packets cannot certify a dispatch that the native writer never captured
    Check(!router.Poll(Read(directory / "quick-use-operation-pending550.json"), 550),
        "An uncaptured invocation escaped the callback lock");
    Check(!router.Poll(Read(directory / "quick-use-ready980.json"), 980),
        "An uncaptured Select borrowed another invocation's ready receipt");
}
}

int main(int argc, char** argv) {
    using namespace iw::overlay::tests::ordinaryUseGraph;
    if (argc != 2) return 2;
    int failed = 0;
    const std::filesystem::path directory(argv[1]);
    for (const auto& [name, run] : std::array{
        std::pair{"ActualGodmodeResultAndPaintKeepTheirSeparateDeadlines", ActualGodmodeResultAndPaintKeepTheirSeparateDeadlines},
        std::pair{"HeldUseJoinsTheActualLocalActionAndRepeatsOnlyAtItsOwnedReadiness", HeldUseJoinsTheActualLocalActionAndRepeatsOnlyAtItsOwnedReadiness},
        std::pair{"AnUncapturedWriteCannotBorrowTheActualReadyReceipt", AnUncapturedWriteCannotBorrowTheActualReadyReceipt}}) {
        try { run(directory); std::cout << "PASS " << name << '\n'; }
        catch (const std::exception& error) { ++failed; std::cout << "FAIL " << name << ": " << error.what() << '\n'; }
    }
    std::cout << 3 - failed << "/3 actual ordinary Use graph checks passed\n";
    return failed ? 1 : 0;
}
