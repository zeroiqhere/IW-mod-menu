#include "OrdinarySourceLevelSupport.h"

namespace iw::overlay::runtime::tests::ordinarySource {
namespace {
void NewOpenMenuPressWaitsForTheNextFiftyMillisecondCycle() {
  RouterRig rig;
  rig.open = true;
  Require(!rig.Poll(), "Unpressed first open cycle emitted input");
  rig.held = {70};
  rig.now = 20;
  Require(!rig.Poll(), "A new Use bypassed the open source wait");
  rig.now = 49;
  Require(!rig.Poll(), "The open source wait ended before fifty");
  rig.now = 50;
  Require(rig.Poll() == Input::Select,
          "The current held Use was missing at the completed open cycle");
}

void GuardFalseOpenCycleConsumesItsFiftyMillisecondIteration() {
  RouterRig rig;
  rig.open = true;
  rig.admission.hasMenu = false;
  Require(!rig.Poll(), "A source guard-false cycle admitted input");
  rig.admission.hasMenu = true;
  rig.held = {70};
  rig.now = 20;
  Require(!rig.Poll(), "Permission recovery skipped the open source wait");
  rig.now = 49;
  Require(!rig.Poll(), "A guard-false cycle ended before fifty");
  rig.now = 50;
  Require(rig.Poll() == Input::Select,
          "Fresh permission lost the current input at its retained deadline");
}

void PendingAndBlockedWorkKeepTheOverdueOpenCycle() {
  for (const bool backendBlocked : {false, true}) {
    RouterRig rig;
    rig.open = true;
    Require(!rig.Poll(), "Unpressed first open cycle emitted input");
    rig.held = {38};
    if (backendBlocked)
      rig.admission.blocked = true;
    else
      rig.pending = true;
    rig.now = 20;
    Require(!rig.Poll(), "Queued work admitted an open source action");
    rig.now = 100;
    Require(!rig.Poll(), "Blocked open source work emitted input");
    if (backendBlocked)
      rig.admission.blocked = false;
    else
      rig.pending = false;
    rig.now = 120;
    Require(rig.Poll() == Input::Up,
            "Suspended work added a fresh fifty after the overdue cycle");
  }
}

void FreshEdgesStillRunsANewOpenMenuPressImmediately() {
  RouterRig rig;
  rig.open = true;
  rig.mode = ClosedOpeningPolling::FreshEdges;
  Require(!rig.Poll(), "Legacy unpressed open sample emitted input");
  rig.held = {70};
  rig.now = 20;
  Require(rig.Poll() == Input::Select,
          "Source-level cadence changed the legacy fresh-edge route");
}
} // namespace

void RegisterTimingChecks(void (*add)(const char *, void (*)())) {
  add("NewOpenMenuPressWaitsForTheNextFiftyMillisecondCycle",
      NewOpenMenuPressWaitsForTheNextFiftyMillisecondCycle);
  add("GuardFalseOpenCycleConsumesItsFiftyMillisecondIteration",
      GuardFalseOpenCycleConsumesItsFiftyMillisecondIteration);
  add("PendingAndBlockedWorkKeepTheOverdueOpenCycle",
      PendingAndBlockedWorkKeepTheOverdueOpenCycle);
  add("FreshEdgesStillRunsANewOpenMenuPressImmediately",
      FreshEdgesStillRunsANewOpenMenuPressImmediately);
}
} // namespace iw::overlay::runtime::tests::ordinarySource
