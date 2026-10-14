#pragma once
#include "../use-monitor/UseContinuationGraphSupport.h"
#include "OpeningPolicyScope.h"
#include <map>

namespace iw::overlay::tests::closedLevelGraph {
using useGraph::Read;
struct Router {
    runtime::NativeInputRouter input{useGraph::Bindings()};
    std::set<int> held;
    bool foreground = true, pending = false;
    std::optional<runtime::Input> Poll(const runtime::ParsedState& state, std::uint64_t now) {
        const auto& frame = *state.snapshot;
        runtime::UseMonitorObservation use{frame.menuUseContinuation ? &*frame.menuUseContinuation : nullptr,
            frame.menuOptionCallback ? &*frame.menuOptionCallback : nullptr, state.lastSequence};
        runtime::MonitorContinuationObservation mechanical{
            frame.menuMonitorContinuation ? &*frame.menuMonitorContinuation : nullptr, state.lastSequence};
        // Each external key is read once, even when several saved actions share that key
        std::map<int, unsigned> reads;
        const auto result = input.Poll(foreground, frame.isOpen, pending,
            [&](int key) { Check(++reads[key] == 1, "Opening read an external key more than once"); return held.contains(key); },
            now, frame.navigationRevision, frame.appearance.quickExit, frame.isRootMenu,
            runtime::InputModeForSnapshot(frame), frame.appearance.scrollAnimationMilliseconds, frame.menuRevision,
            frame.mainOpeningControls ? &*frame.mainOpeningControls : nullptr, 0, 0,
            frame.openingPolicies ? &*frame.openingPolicies : nullptr,
            frame.localMenuEligibility ? &*frame.localMenuEligibility : nullptr, frame.menuSurface,
            frame.menuMonitorWait ? &*frame.menuMonitorWait : nullptr, &use, &mechanical,
            ClosedOpeningPolling::SourceLevels);
        if (!foreground) Check(reads.empty(), "Background opening read global keys");
        return result;
    }
};
inline Json OpeningCommand(const runtime::ParsedState& state, runtime::Input input, std::uint64_t sequence) {
    const auto& frame = *state.snapshot;
    Check(frame.localMenuEligibility.has_value(), "Opening command lacks its original actor scope");
    const auto command = Json::parse(runtime::CommandJson(42, sequence, input, frame.navigationRevision,
        runtime::ObservedOpeningControlsRevision(frame), frame.localMenuEligibility->revision));
    // Opening uses its immutable observed trio, without mechanical or address fields
    Check(command.size() == 7 && command.at("version") == 1 && command.at("processId") == 42
        && command.at("sequence") == sequence && command.at("input") == runtime::InputName(input)
        && command.at("navigationRevision") == frame.navigationRevision
        && command.at("openingControlsRevision") == *runtime::ObservedOpeningControlsRevision(frame)
        && command.at("localAdmissionRevision") == frame.localMenuEligibility->revision
        && !command.contains("menuRevision"), "Opening serialization retargeted or broadened its captured scope");
    return command;
}
void ActualClosedInventoryMatchesLiteralPermissionsTimingAndBothReaders(const std::filesystem::path& directory);
}

namespace iw::overlay::tests::closedLevelGraph {
void ActualMainBackResumesHeldOpeningAtTwoFiftyAndCreatesChromeAtFifty(const std::filesystem::path& directory);
void ActualLivingQuickCreatesImmediatelyAndKeepsItsOwnFiveFiftyWait(const std::filesystem::path& directory);
void ActualDeadAimJumpReopensOnlyAfterItsOwnBackWait(const std::filesystem::path& directory);
void ActualPermissionRecoveryUsesCurrentAuthorityWithoutANewChordEdge(const std::filesystem::path& directory);
void ActualQuickDisabledKeepsMainPriorityAndDeniesLivingQuick(const std::filesystem::path& directory);
void ActualUnknownAndMissingFactsDenySourceLevelsWithoutLegacyBootstrap(const std::filesystem::path& directory);
void ActualNavigationReplacementRejectsTheOriginalOpeningScope(const std::filesystem::path& directory);
void ActualPolicyReplacementRejectsTheOriginalOpeningScope(const std::filesystem::path& directory);
void ActualAdmissionReplacementRejectsTheOriginalOpeningScope(const std::filesystem::path& directory);
void ActualQueuedFocusLossRetiresThePendingOpenWithoutNativeReplay(const std::filesystem::path& directory);
void ActualQueuedAdmissionReplacementNeverReusesTheOldCommandScope(const std::filesystem::path& directory);
void ActualQueuedOwnerReplacementNeedsItsNewAdmissionScope(const std::filesystem::path& directory);
}
