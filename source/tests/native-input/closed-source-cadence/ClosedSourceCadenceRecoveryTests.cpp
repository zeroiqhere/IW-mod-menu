#include "ClosedSourceCadenceSupport.h"
#include <algorithm>

namespace iw::overlay::runtime::tests::closedCadence {
namespace {
void RollbackBetweenIdleEvaluationsRejectsKeysAndRequiresNeutralRecovery() {
    RouterRig rig;
    rig.Poll();
    rig.now = 20;
    rig.Poll();
    rig.now = 10;
    rig.held = {2, 86};
    Require(!rig.Poll() && std::all_of(rig.reads.begin(), rig.reads.end(),
        [](unsigned reads) { return reads == 0; }), "Backward source sample read keys or opened a chord");
    rig.now = 11;
    Require(!rig.Poll(), "Rollback rearmed an inherited full chord");
    rig.held.clear();
    rig.now = 12;
    rig.Poll();
    rig.held = {2, 86};
    rig.now = 61;
    Require(rig.Poll() == Input::OpenMain, "Neutral rollback recovery did not restore the next source cycle");
}
void RelativeSubtractionWorksAtTheLargestTimestamp() {
    RouterRig rig;
    const auto maximum = std::numeric_limits<std::uint64_t>::max();
    rig.now = maximum - 50;
    rig.Poll();
    rig.held = {2, 86};
    rig.now = maximum - 1;
    Require(!rig.Poll(), "Near-limit source cycle overflowed or ended at49");
    rig.now = maximum;
    Require(rig.Poll() == Input::OpenMain, "Largest timestamp failed its exact relative fifty wait");
    Require(!rig.Poll(), "Largest timestamp burst a second logical cycle");
}
void SourceWaitThenMissingOwnerPreservesLegacyRollbackProtection() {
    RouterRig rig;
    rig.now = 100;
    rig.wait = MenuMonitorWait{1, MenuMonitorWaitKind::Back};
    Require(!rig.Poll(), "Source wait granted opening");
    // The worker uses legacy sampling if the next packet lacks a typed owner
    rig.hasAdmission = false;
    rig.mode = ClosedOpeningPolling::FreshEdges;
    rig.now = 90;
    rig.held = {2, 86};
    Require(!rig.Poll() && std::all_of(rig.reads.begin(), rig.reads.end(),
        [](unsigned reads) { return reads == 0; }), "Owner loss across sampling modes bypassed wait rollback rejection");
}
void InvalidModeRecoveryClearsOldPhaseWithoutBypassingTheNeutralBarrier() {
    RouterRig rig;
    rig.Poll();
    rig.now = 20;
    rig.held = {2, 86};
    rig.inputMode = static_cast<InputMode>(99);
    bool rejected = false;
    try { rig.Poll(); } catch (const std::invalid_argument&) { rejected = true; }
    Require(rejected && std::all_of(rig.reads.begin(), rig.reads.end(),
        [](unsigned reads) { return reads == 0; }), "Invalid mode recovery read physical keys");
    rig.inputMode = InputMode::Menu;
    rig.now = 21;
    Require(!rig.Poll(), "Invalid mode recovery ignored the neutral barrier");
    rig.held.clear();
    rig.now = 22;
    rig.Poll();
    rig.held = {2, 86};
    rig.now = 70;
    Require(!rig.Poll(), "Recovered source cycle ended before its own fifty");
    rig.now = 71;
    Require(rig.Poll() == Input::OpenMain, "Recovered phase did not admit the current permitted chord");
}
void FocusRecoveryKeepsFastNeutralSamplingAndASeparateIdleWait() {
    RouterRig rig;
    rig.Poll();
    rig.foreground = false;
    rig.now = 20;
    rig.held = {2, 69};
    Require(!rig.Poll() && std::all_of(rig.reads.begin(), rig.reads.end(),
        [](unsigned reads) { return reads == 0; }), "Background source cycle read desktop keys");
    rig.foreground = true;
    rig.now = 30;
    Require(!rig.Poll(), "Focus recovery replayed the discarded complete hold");
    // The host barrier can observe release before the next logical source evaluation
    rig.held.clear();
    rig.now = 31;
    rig.Poll();
    rig.held = {2, 69};
    rig.now = 79;
    Require(!rig.Poll(), "Fast neutral sampling bypassed the recovered idle wait");
    rig.now = 80;
    Require(rig.Poll() == Input::OpenQuick, "Fast neutral release failed to restore current Quick at fifty");
}
}
void RegisterRecoveryChecks(void (*add)(const char *, void (*)())) {
    add("RollbackBetweenIdleEvaluationsRejectsKeysAndRequiresNeutralRecovery", RollbackBetweenIdleEvaluationsRejectsKeysAndRequiresNeutralRecovery);
    add("RelativeSubtractionWorksAtTheLargestTimestamp", RelativeSubtractionWorksAtTheLargestTimestamp);
    add("SourceWaitThenMissingOwnerPreservesLegacyRollbackProtection", SourceWaitThenMissingOwnerPreservesLegacyRollbackProtection);
    add("InvalidModeRecoveryClearsOldPhaseWithoutBypassingTheNeutralBarrier", InvalidModeRecoveryClearsOldPhaseWithoutBypassingTheNeutralBarrier);
    add("FocusRecoveryKeepsFastNeutralSamplingAndASeparateIdleWait", FocusRecoveryKeepsFastNeutralSamplingAndASeparateIdleWait);
}
}
