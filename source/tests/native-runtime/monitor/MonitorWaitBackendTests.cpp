#include "MonitorWaitSchemaTests.h"
#include "../TestSupport.h"
#include "../../native-layout/BackendPreview.h"
#include "ControlLabels.h"
#include <array>
#include <fstream>
#include <set>

namespace iw::overlay::tests::monitor {
namespace {
runtime::Bindings PacketBindings() {
    // The fixture action groups are explicit and do not stand in for a real saved keyboard file
    return runtime::Bindings{{2}, {86}, {2, 38}, {1, 40}, {70}, {51}, {52}, {1}, {38}, {40}, {71}, {69}, {16}, {17}, {32}};
}
std::string Bytes(const std::filesystem::path& path) {
    Check(std::filesystem::file_size(path) <= runtime::MaximumFileBytes, "An actual packet exceeds the bounded reader");
    std::ifstream stream(path);
    Check(static_cast<bool>(stream), "An actual controller packet is missing");
    return {std::istreambuf_iterator<char>(stream), std::istreambuf_iterator<char>()};
}
auto Read(const std::filesystem::path& path) {
    const auto labels = runtime::DescribeOpeningActions(PacketBindings());
    return runtime::ParseState(Bytes(path), 42, nullptr, nullptr, nullptr, nullptr, nullptr,
        std::nullopt, nullptr, nullptr, &labels).snapshot;
}
struct RouterGraph {
    runtime::NativeInputRouter router{PacketBindings()};
    std::set<int> held;
    std::optional<runtime::Input> Poll(const MenuSnapshot& frame, std::uint64_t now) {
        return router.Poll(true, frame.isOpen, false, [&](int key) { return held.contains(key); },
            now, frame.navigationRevision, frame.appearance.quickExit, frame.isRootMenu,
            runtime::InputModeForSnapshot(frame), frame.appearance.scrollAnimationMilliseconds, frame.menuRevision,
            frame.mainOpeningControls ? &*frame.mainOpeningControls : nullptr, 0, 0,
            frame.openingPolicies ? &*frame.openingPolicies : nullptr,
            frame.localMenuEligibility ? &*frame.localMenuEligibility : nullptr, frame.menuSurface,
            frame.menuMonitorWait ? &*frame.menuMonitorWait : nullptr);
    }
};
}
void ActualControllerWaitPacketsMatchBothStrictReaders(const std::filesystem::path& directory) {
    const std::array names{
        "choice-before0.json", "choice0.json", "choice179.json", "choice180.json",
        "back-parent0.json", "back-parent249.json", "back-parent250.json", "back-root-before0.json",
        "back-root-closed0.json", "back-root-closed249.json", "back-root-closed250.json",
        "dead-jump0.json", "dead-jump250.json", "hold-close0.json", "hold-close250.json",
        "focus-owner0.json", "focus-hidden0.json", "focus-return100.json", "focus-ready180.json",
        "owner-before0.json", "owner-replaced0.json", "rollback-before100.json", "rollback-recovered10.json",
        "rollback-ready190.json", "numeric-bound250.json", "numeric-wrap0.json"};
    std::size_t actual = 0;
    for (const auto& entry : std::filesystem::directory_iterator(directory)) if (entry.path().extension() == ".json") ++actual;
    Check(actual == names.size(), "The separate actual wait inventory changed silently");
    const auto labels = runtime::DescribeOpeningActions(PacketBindings());
    for (const auto* name : names) {
        const auto path = directory / name;
        const auto runtime = Read(path); const auto preview = ReadBackendPreview(path, &labels);
        Check(runtime->menuMonitorWait == preview.menuMonitorWait
            && runtime->localMenuEligibility == preview.localMenuEligibility,
            "Preview and runtime disagree about actual wait or admission ownership");
        Check(runtime->navigationRevision == preview.navigationRevision && runtime->menuRevision == preview.menuRevision
            && runtime->selectedIndex == preview.selectedIndex && runtime->isOpen == preview.isOpen
            && runtime->rows.size() == preview.rows.size(), "A reader changed actual source navigation");
        for (std::size_t row = 0; row < preview.rows.size(); ++row) {
            Check(runtime->rows[row].label == preview.rows[row].label && runtime->rows[row].value == preview.rows[row].value
                && runtime->rows[row].enabled == preview.rows[row].enabled, "A reader changed an actual UI value");
        }
        const auto raw = Json::parse(Bytes(path));
        const bool present = raw.contains("menuMonitorWait") && !raw.at("menuMonitorWait").is_null();
        Check(runtime->menuMonitorWait.has_value() == present, "A reader erased or invented an actual wait");
        if (present) {
            Check(runtime->menuMonitorWait->revision == raw.at("menuMonitorWait").at("revision").get<std::uint64_t>(),
                "A reader narrowed an actual invocation revision");
            Check(runtime::InputModeForSnapshot(*runtime) == runtime::InputMode::Menu,
                "An actual mechanical wait fabricated a modal input reseed");
        }
    }
}
void ActualControllerWaitChronologyJoinsNativeRouting(const std::filesystem::path& directory) {
    const auto beforeChoice = Read(directory / "choice-before0.json");
    const auto choice = Read(directory / "choice0.json"), choice179 = Read(directory / "choice179.json"), choice180 = Read(directory / "choice180.json");
    Check(!beforeChoice->menuMonitorWait && choice->menuMonitorWait && choice179->menuMonitorWait && !choice180->menuMonitorWait,
        "The source choice graph does not retire at180ms");
    RouterGraph slider; slider.Poll(*beforeChoice, 0); slider.held = {52};
    Check(slider.Poll(*beforeChoice, 0) == runtime::Input::NextChoice, "Actual pre-admission frame could not choose its draft");
    Check(!slider.Poll(*choice, 0) && !slider.Poll(*choice179, 179), "Actual choice wait admitted early input");
    Check(slider.Poll(*choice180, 180) == runtime::Input::NextChoice && !slider.Poll(*choice180, 180),
        "Actual controller wait lost its held repeat or caused a burst");

    const auto beforeBack = Read(directory / "back-root-before0.json"), closed = Read(directory / "back-root-closed0.json"),
        closed249 = Read(directory / "back-root-closed249.json"), closed250 = Read(directory / "back-root-closed250.json");
    Check(beforeBack->isOpen && !beforeBack->menuMonitorWait && !closed->isOpen && closed->menuMonitorWait
        && closed249->menuMonitorWait && !closed250->menuMonitorWait, "The root Back graph lost its remaining250ms");
    RouterGraph back; back.Poll(*beforeBack, 0); back.held = {86};
    Check(back.Poll(*beforeBack, 0) == runtime::Input::Back, "Actual root frame could not admit its fresh Back");
    Check(!back.Poll(*closed, 0), "The closed result replayed a knife action");
    back.held = {2, 86}; Check(!back.Poll(*closed249, 249), "An opening chord bypassed actual closed-root wait");
    Check(!back.Poll(*closed250, 250), "An opening chord observed during Back replayed at retirement");
    back.held.clear(); back.Poll(*closed250, 251); back.held = {2, 86};
    Check(back.Poll(*closed250, 252) == runtime::Input::OpenMain,
        "A fresh scoped opening failed after the actual root wait finished");
}
}
