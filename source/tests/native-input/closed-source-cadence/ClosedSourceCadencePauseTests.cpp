#include "ClosedSourceCadenceSupport.h"

namespace iw::overlay::runtime::tests::closedCadence {
namespace {
void RejectedOpeningAckCannotResetTheIdleCycle() {
    RouterRig rig;
    rig.held = {2, 86};
    Require(rig.Poll() == Input::OpenMain, "Original opening attempt was not emitted");
    rig.pending = true;
    rig.now = 20;
    Require(!rig.Poll(), "Pending opening emitted another request");
    rig.pending = false;
    ++rig.ack;
    rig.now = 30;
    Require(!rig.Poll(), "Rejected opening ACK reset the source wait");
    rig.now = 50;
    Require(rig.Poll() == Input::OpenMain, "Rejected ACK lost the next current evaluation");
}
void LongPendingTransportDoesNotAddAnotherWaitAfterRetirement() {
    RouterRig rig;
    rig.held = {2, 69};
    Require(rig.Poll() == Input::OpenQuick, "Original Quick attempt missing");
    rig.pending = true;
    for (const auto now : {20U, 50U, 100U, 249U}) {
        rig.now = now;
        Require(!rig.Poll(), "Pending transport emitted an opening burst");
    }
    // ACK only serializes writes; it is not a source callback sleep
    rig.pending = false;
    ++rig.ack;
    rig.now = 250;
    Require(rig.Poll() == Input::OpenQuick, "Pending transport added a fresh idle wait on retirement");
    Require(!rig.Poll(), "Retired transport allowed more than one current evaluation");
}
void BackendBlockedWorkSuspendsRatherThanConsumesTheIdleCycle() {
    RouterRig rig;
    rig.Poll();
    rig.held = {2, 86};
    rig.admission.blocked = true;
    for (const auto now : {20U, 50U, 100U}) {
        rig.now = now;
        Require(!rig.Poll(), "Backend-owned work emitted an opening intent");
    }
    // Completion uses the overdue existing phase, unlike a guard-false idle iteration
    rig.admission.blocked = false;
    rig.now = 120;
    Require(rig.Poll() == Input::OpenMain, "Backend work consumed or restarted the original idle deadline");
    Require(!rig.Poll(), "Backend completion emitted a second logical cycle");
}
void ClosedNonordinaryModePreservesTheExistingIdleDeadline() {
    RouterRig rig;
    rig.Poll();
    rig.held = {2, 86};
    rig.inputMode = InputMode::MenuLocked;
    rig.now = 20;
    Require(!rig.Poll(), "Closed input lock admitted an opening chord");
    // An input lock alone is not an open predecessor or a new monitor identity
    rig.inputMode = InputMode::Menu;
    rig.now = 30;
    Require(!rig.Poll(), "Temporary closed input lock restarted the source phase");
    rig.now = 50;
    Require(rig.Poll() == Input::OpenMain, "Recovered ordinary mode lost its existing idle deadline");
}
void UnownedBackWaitCannotRestartAnExistingIdlePhase() {
    RouterRig rig;
    rig.Poll();
    rig.ack = 11;
    rig.receipt = MenuMonitorContinuation{1, 11, 4, MenuMonitorWaitKind::Back};
    rig.wait = MenuMonitorWait{1, MenuMonitorWaitKind::Back};
    rig.held = {2, 86};
    rig.now = 20;
    Require(!rig.Poll(), "Unowned Back wait allowed opening");
    rig.wait.reset();
    rig.now = 30;
    Require(!rig.Poll(), "Foreign receipt reset the already adopted idle clock");
    rig.now = 50;
    Require(rig.Poll() == Input::OpenMain, "Current policy lost its own retained deadline");
}
void OpenPredecessorAndLateOwnBackCompletionNeedNoExtraFifty() {
    RouterRig rig;
    rig.Poll();
    rig.open = true;
    rig.Poll();
    rig.held = {86};
    rig.now = 49;
    Require(!rig.Poll(), "Open predecessor knife escaped its idle fifty");
    rig.now = 50;
    Require(rig.Poll() == Input::Back, "Open predecessor did not stage its own Back");
    rig.router.CaptureMonitorDispatch(11);
    rig.ack = 11;
    rig.open = false;
    ++rig.navigation;
    ++rig.menu;
    rig.receipt = MenuMonitorContinuation{1, 11, 4, MenuMonitorWaitKind::Back};
    rig.wait = MenuMonitorWait{1, MenuMonitorWaitKind::Back};
    rig.held = {2, 86};
    Require(!rig.Poll(), "Own Back granted opening during its wait");
    rig.now = 299;
    Require(!rig.Poll(), "Own Back ended before its complete pause");
    // A late first observation cannot add fifty to the already completed branch
    rig.wait.reset();
    rig.now = 300;
    Require(rig.Poll() == Input::OpenMain, "Late own Back completion paid an extra idle wait");
}
void FreshEdgeCompatibilityKeepsItsImmediateNextPress() {
    RouterRig rig;
    rig.mode = ClosedOpeningPolling::FreshEdges;
    rig.Poll();
    rig.held = {2, 86};
    rig.now = 20;
    Require(rig.Poll() == Input::OpenMain, "Modern idle cadence changed the legacy fresh-edge route");
}
}
void RegisterPauseChecks(void (*add)(const char *, void (*)())) {
    add("RejectedOpeningAckCannotResetTheIdleCycle", RejectedOpeningAckCannotResetTheIdleCycle);
    add("LongPendingTransportDoesNotAddAnotherWaitAfterRetirement", LongPendingTransportDoesNotAddAnotherWaitAfterRetirement);
    add("BackendBlockedWorkSuspendsRatherThanConsumesTheIdleCycle", BackendBlockedWorkSuspendsRatherThanConsumesTheIdleCycle);
    add("ClosedNonordinaryModePreservesTheExistingIdleDeadline", ClosedNonordinaryModePreservesTheExistingIdleDeadline);
    add("UnownedBackWaitCannotRestartAnExistingIdlePhase", UnownedBackWaitCannotRestartAnExistingIdlePhase);
    add("OpenPredecessorAndLateOwnBackCompletionNeedNoExtraFifty", OpenPredecessorAndLateOwnBackCompletionNeedNoExtraFifty);
    add("FreshEdgeCompatibilityKeepsItsImmediateNextPress", FreshEdgeCompatibilityKeepsItsImmediateNextPress);
}
}
