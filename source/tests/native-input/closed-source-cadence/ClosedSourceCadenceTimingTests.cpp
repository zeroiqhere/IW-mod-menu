#include "ClosedSourceCadenceSupport.h"

namespace iw::overlay::runtime::tests::closedCadence {
namespace {
void IdleClosedIterationWaitsFiftyBeforeUsingTheCurrentHold() {
    RouterRig rig;
    Require(!rig.Poll(), "Unpressed first closed cycle emitted input");
    rig.held = {2, 86};
    rig.now = 20;
    Require(!rig.Poll(), "Idle source cycle acted on the twenty millisecond poll");
    rig.now = 49;
    Require(!rig.Poll(), "Idle source cycle ended before fifty");
    rig.now = 50;
    Require(rig.Poll() == Input::OpenMain, "Current held Main did not run at fifty");
    Require(!rig.Poll(), "One logical cycle emitted twice at the same timestamp");
}
void TransientChordsDuringTheWaitAreNeverQueued() {
    RouterRig rig;
    rig.Poll();
    rig.held = {2, 86};
    rig.now = 20;
    Require(!rig.Poll(), "Transient hold ran during the idle wait");
    rig.held.clear();
    rig.now = 49;
    rig.Poll();
    rig.now = 50;
    Require(!rig.Poll(), "Released chord was queued into a later source cycle");
    // Only the latest current Quick chord can run at the next completed wait
    rig.held = {2, 69};
    rig.now = 70;
    Require(!rig.Poll(), "New Quick bypassed the next idle wait");
    rig.now = 100;
    Require(rig.Poll() == Input::OpenQuick, "Current Quick was replaced by an older Main intent");
}
void GuardRecoveryRetainsTheCycleAndUsesFreshPermission() {
    for (const auto guard : {0, 1, 2}) {
        RouterRig rig;
        if (guard == 0) rig.admission.hasMenu = false;
        if (guard == 1) rig.admission.controlsDisabled = true;
        if (guard == 2) rig.admission.life = PlayerLifeState::Unknown;
        rig.held = {2, 86};
        Require(!rig.Poll(), "Guard-false source iteration granted opening");
        // These facts change permission, rather than the identity of a source loop
        ++rig.admission.revision;
        rig.admission.hasMenu = true;
        rig.admission.controlsDisabled = false;
        rig.admission.life = PlayerLifeState::Alive;
        rig.now = 20;
        Require(!rig.Poll(), "Permission recovery restarted or skipped the idle wait");
        rig.now = 50;
        Require(rig.Poll() == Input::OpenMain, "Fresh permitted actor lost the next source cycle");
    }
}
void PolicyReplacementUsesItsCurrentChordAtTheRetainedDeadline() {
    RouterRig rig;
    rig.Poll();
    ++rig.policies.revision;
    ++rig.policies.main.revision;
    rig.policies.main.actions = {OpeningAction::Aim, OpeningAction::Frag};
    rig.held = {2, 71};
    rig.now = 20;
    Require(!rig.Poll(), "Policy revision invented a new monitor cycle");
    rig.now = 50;
    Require(rig.Poll() == Input::OpenMain, "New verified tuple required an old chord edge");
}
void MissingPolicyRecoveryUsesTheRetainedDeadlineWithoutGuessingKeys() {
    RouterRig rig;
    rig.hasPolicy = false;
    rig.held = {2, 86};
    Require(!rig.Poll(), "Typed owner with missing policy used a guessed opening chord");
    // Restoring policy does not identify a new source monitor thread
    rig.hasPolicy = true;
    rig.now = 20;
    Require(!rig.Poll(), "Policy recovery restarted the completed idle iteration");
    rig.now = 50;
    Require(rig.Poll() == Input::OpenMain, "Verified policy lost the retained fifty deadline");
}
void AdoptionAndLateWakeUseRelativeWaitsWithoutCatchUp() {
    RouterRig rig;
    rig.now = 123;
    rig.Poll();
    rig.held = {2, 86};
    rig.now = 150;
    Require(!rig.Poll(), "Closed cycle aligned itself to a global clock grid");
    rig.now = 172;
    Require(!rig.Poll(), "Relative first cycle ended early");
    rig.now = 200;
    Require(rig.Poll() == Input::OpenMain, "Late current evaluation was skipped");
    Require(!rig.Poll(), "Late wake caught up with a second command");
    rig.now = 249;
    Require(!rig.Poll(), "Late evaluation inherited an older absolute deadline");
    rig.now = 250;
    Require(rig.Poll() == Input::OpenMain, "Late evaluation did not anchor its next fifty wait");
}
}
void RegisterTimingChecks(void (*add)(const char *, void (*)())) {
    add("IdleClosedIterationWaitsFiftyBeforeUsingTheCurrentHold", IdleClosedIterationWaitsFiftyBeforeUsingTheCurrentHold);
    add("TransientChordsDuringTheWaitAreNeverQueued", TransientChordsDuringTheWaitAreNeverQueued);
    add("GuardRecoveryRetainsTheCycleAndUsesFreshPermission", GuardRecoveryRetainsTheCycleAndUsesFreshPermission);
    add("PolicyReplacementUsesItsCurrentChordAtTheRetainedDeadline", PolicyReplacementUsesItsCurrentChordAtTheRetainedDeadline);
    add("MissingPolicyRecoveryUsesTheRetainedDeadlineWithoutGuessingKeys", MissingPolicyRecoveryUsesTheRetainedDeadlineWithoutGuessingKeys);
    add("AdoptionAndLateWakeUseRelativeWaitsWithoutCatchUp", AdoptionAndLateWakeUseRelativeWaitsWithoutCatchUp);
}
}
