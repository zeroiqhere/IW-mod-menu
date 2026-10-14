#pragma once
#include "../use-monitor/UseContinuationGraphSupport.h"

namespace iw::overlay::tests::mechanicalGraph {
using useGraph::Read;
struct Router {
  runtime::NativeInputRouter input{useGraph::Bindings()};
  std::set<int> held;
  bool foreground = true, pending = false;
  std::optional<runtime::Input> Poll(const runtime::ParsedState &state,
                                     std::uint64_t now) {
    const auto &frame = *state.snapshot;
    // The acknowledgment and both continuations share one immutable packet
    runtime::UseMonitorObservation use{
        frame.menuUseContinuation ? &*frame.menuUseContinuation : nullptr,
        frame.menuOptionCallback ? &*frame.menuOptionCallback : nullptr,
        state.lastSequence};
    runtime::MonitorContinuationObservation mechanical{
        frame.menuMonitorContinuation ? &*frame.menuMonitorContinuation
                                      : nullptr,
        state.lastSequence};
    return input.Poll(
        foreground, frame.isOpen, pending,
        [&](int key) { return held.contains(key); }, now,
        frame.navigationRevision, frame.appearance.quickExit, frame.isRootMenu,
        runtime::InputModeForSnapshot(frame),
        frame.appearance.scrollAnimationMilliseconds, frame.menuRevision,
        frame.mainOpeningControls ? &*frame.mainOpeningControls : nullptr, 0, 0,
        frame.openingPolicies ? &*frame.openingPolicies : nullptr,
        frame.localMenuEligibility ? &*frame.localMenuEligibility : nullptr,
        frame.menuSurface,
        frame.menuMonitorWait ? &*frame.menuMonitorWait : nullptr, &use,
        &mechanical);
  }
};
void ActualMechanicalInventoryMatchesLiteralOraclesAndBothReaders(
    const std::filesystem::path &directory);
} // namespace iw::overlay::tests::mechanicalGraph
