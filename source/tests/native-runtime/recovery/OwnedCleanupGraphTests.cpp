#include "../use-monitor/UseContinuationGraphSupport.h"
#include "OpeningPolicyScope.h"
#include <array>
#include <fstream>
#include <iostream>
#include <map>

namespace iw::overlay::tests::cleanup {
struct Router {
    runtime::NativeInputRouter input{useGraph::Bindings()};
    std::set<int> held;
    bool foreground = true;
    bool pending = false;

    std::optional<runtime::Input> Poll(const runtime::ParsedState& state, std::uint64_t now) {
        const auto& frame = *state.snapshot;
        runtime::UseMonitorObservation use{frame.menuUseContinuation ? &*frame.menuUseContinuation : nullptr,
            frame.menuOptionCallback ? &*frame.menuOptionCallback : nullptr, state.lastSequence};
        runtime::MonitorContinuationObservation mechanical{
            frame.menuMonitorContinuation ? &*frame.menuMonitorContinuation : nullptr, state.lastSequence};
        // Match the runtime's sampler choice using the presence of actual actor proof
        return input.Poll(foreground, frame.isOpen, pending,
            [&](int key) { return held.contains(key); }, now, frame.navigationRevision,
            frame.appearance.quickExit, frame.isRootMenu, runtime::InputModeForSnapshot(frame),
            frame.appearance.scrollAnimationMilliseconds, frame.menuRevision,
            frame.mainOpeningControls ? &*frame.mainOpeningControls : nullptr, 0, 0,
            frame.openingPolicies ? &*frame.openingPolicies : nullptr,
            frame.localMenuEligibility ? &*frame.localMenuEligibility : nullptr,
            frame.menuSurface, frame.menuMonitorWait ? &*frame.menuMonitorWait : nullptr, &use, &mechanical,
            frame.localMenuEligibility ? ClosedOpeningPolling::SourceLevels : ClosedOpeningPolling::FreshEdges);
    }
};

void ActualPacketsKeepAdministrativeRowsSeparateFromActorProof(const std::filesystem::path& directory,
    std::map<std::string, runtime::ParsedState>& states) {
    // Account preference admission and withdrawal each reserve their own policy revision
    const std::array profiles{std::pair{"healthy-initial-closed.json", 2U}, std::pair{"healthy-open.json", 2U},
        std::pair{"lost-closed.json", 3U}, std::pair{"cleanup-open.json", 3U}, std::pair{"cleanup-closed.json", 3U},
        std::pair{"cleanup-reopened.json", 3U}, std::pair{"healthy-closed.json", 4U}, std::pair{"healthy-reopened.json", 4U}};
    for (const auto& [name, revision] : profiles) {
        const auto bytes = useGraph::Bytes(directory / name);
        const auto json = Json::parse(bytes);
        auto state = runtime::ParseState(bytes, 4242);
        const auto& frame = *state.snapshot;
        Check(frame.mainOpeningControls == MainOpeningControls{revision, {OpeningAction::Aim, OpeningAction::Melee}},
            "Actual cleanup packets lost the configured Main opening chord");
        Check(!frame.menuOpenTransition && !frame.menuOpening && !frame.menuOptionCallback
            && !frame.menuUseContinuation && !frame.menuMonitorWait && !frame.menuMonitorContinuation,
            "A ready cleanup or ordinary packet retained a sleeping gameplay invocation");
        if (std::string{name}.starts_with("healthy")) {
            Check(frame.localMenuEligibility && frame.localMenuEligibility->hasMenu == true
                && frame.rows.size() == 8, "Healthy packets lost normal actor permission or root navigation");
        } else {
            Check(!frame.localMenuEligibility && !json.contains("localMenuEligibility"),
                "Missing player proof was serialized as a typed or fabricated admission");
        }
        if (std::string{name}.starts_with("cleanup") && frame.isOpen) {
            Check(frame.isRootMenu && frame.rows.size() == 1 && frame.selectedIndex == 0
                && frame.rows.front().label == "Exit" && frame.rows.front().enabled,
                "Actual administrative packets exposed more than the existing Exit action");
        }
        states.emplace(name, std::move(state));
    }
    Check(!states.at("lost-closed.json").snapshot->isOpen && states.at("cleanup-open.json").snapshot->isOpen
        && !states.at("cleanup-closed.json").snapshot->isOpen && states.at("cleanup-reopened.json").snapshot->isOpen
        && !states.at("healthy-closed.json").snapshot->isOpen && states.at("healthy-reopened.json").snapshot->isOpen,
        "Actual loss, explicit Back, reopening, or healthy withdrawal changed visibility");
}

std::string HeldOpeningCannotSurviveActorLossAndFreshInputsReachExit(
    const std::map<std::string, runtime::ParsedState>& states) {
    Router router;
    const auto& healthyClosed = states.at("healthy-initial-closed.json");
    const auto& lost = states.at("lost-closed.json");
    const auto& cleanup = states.at("cleanup-open.json");
    Check(!router.Poll(healthyClosed, 0), "Neutral healthy input opened a menu");
    router.held = {2, 86};
    Check(router.Poll(healthyClosed, 50) == runtime::Input::OpenMain, "Healthy source opening was unavailable");
    // Source-level melee may return from Main while held, but cannot become Use on that row
    Check(router.Poll(states.at("healthy-open.json"), 100) != runtime::Input::Select,
        "An inherited opening key selected a normal row");
    Check(!router.Poll(lost, 150) && !router.Poll(lost, 200), "A held chord reopened after actor proof disappeared");
    router.held.clear();
    Check(!router.Poll(lost, 250), "Neutral actorless release opened cleanup");
    router.held = {2, 86};
    Check(router.Poll(lost, 300) == runtime::Input::Toggle, "A fresh configured chord did not request actorless opening");
    const auto opening = Json::parse(runtime::CommandJson(4242, lost.lastSequence + 1,
        runtime::Input::Toggle, lost.snapshot->navigationRevision,
        runtime::ObservedOpeningControlsRevision(*lost.snapshot)));
    Check(opening.contains("navigationRevision") && opening.at("navigationRevision") == lost.snapshot->navigationRevision
        && opening.contains("openingControlsRevision")
        && opening.at("openingControlsRevision") == lost.snapshot->mainOpeningControls->revision
        && !opening.contains("localAdmissionRevision"), "Actorless opening dropped its displayed navigation or policy scope");
    Check(!router.Poll(cleanup, 350), "Opening keys immediately closed or selected administrative Exit");
    router.held.clear();
    Check(!router.Poll(cleanup, 400), "Neutral cleanup release emitted a command");
    router.held = {70};
    Check(router.Poll(cleanup, 450) == runtime::Input::Select, "Fresh Use could not select the sole Exit row");
    const auto command = Json::parse(runtime::CommandJson(4242, cleanup.lastSequence + 1,
        runtime::Input::Select, cleanup.snapshot->navigationRevision,
        runtime::ObservedOpeningControlsRevision(*cleanup.snapshot)));
    Check(command.at("input") == "select" && !command.contains("localAdmissionRevision")
        && !command.contains("menuRevision"), "Administrative Exit acquired manufactured actor scope");
    router.held.clear(); router.Poll(cleanup, 500);
    router.held = {86};
    Check(router.Poll(cleanup, 550) == runtime::Input::Back, "Fresh melee could not close the administrative root");
    return opening.dump();
}

void FocusAndPendingWorkConsumeOpeningKeysUntilRelease(const runtime::ParsedState& closed) {
    for (bool focusLoss : {false, true}) {
        Router router;
        router.Poll(closed, 0);
        router.foreground = !focusLoss;
        router.pending = !focusLoss;
        router.held = {2, 86};
        Check(!router.Poll(closed, 50), "An unfocused or pending actorless chord emitted a command");
        router.foreground = true;
        router.pending = false;
        Check(!router.Poll(closed, 100), "A denied held opening replayed after focus or work recovered");
        router.held.clear(); router.Poll(closed, 150);
        router.held = {2, 86};
        Check(router.Poll(closed, 200) == runtime::Input::Toggle, "A later fresh actorless chord remained suppressed");
    }
}
}

int main(int argc, char** argv) {
    if (argc != 2 && argc != 3) return 2;
    try {
        std::map<std::string, iw::overlay::runtime::ParsedState> states;
        iw::overlay::tests::cleanup::ActualPacketsKeepAdministrativeRowsSeparateFromActorProof(argv[1], states);
        const auto opening = iw::overlay::tests::cleanup::HeldOpeningCannotSurviveActorLossAndFreshInputsReachExit(states);
        iw::overlay::tests::cleanup::FocusAndPendingWorkConsumeOpeningKeysUntilRelease(states.at("lost-closed.json"));
        if (argc == 3) {
            // A separate new directory carries the actual native command back to the managed consumer
            const std::filesystem::path directory{argv[2]};
            iw::overlay::tests::Check(std::filesystem::create_directory(directory), "Native command output already exists");
            std::ofstream stream(directory / "toggle.json", std::ios::binary);
            stream << opening;
            iw::overlay::tests::Check(static_cast<bool>(stream), "The actual native Toggle could not be written");
        }
        std::cout << "PASS actual owned cleanup packets, actor-loss freshness, Exit/Back, focus and pending boundaries\n";
        return 0;
    } catch (const std::exception& exception) {
        std::cerr << "FAIL " << exception.what() << '\n';
        return 1;
    }
}
