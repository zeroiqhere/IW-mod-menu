#include "ClosedLevelOpeningSupport.h"
namespace iw::overlay::runtime::tests::closedLevel {
namespace {
void CurrentHeldMainOpensWithoutAnAggregateEdge() {
  ArbitrationRig rig;
  rig.held = {2, 86};
  Require(rig.Poll() == ClosedMenuOpeningIntent::Main,
          "Current held Main was seeded away");
}
void CurrentHeldQuickUsesItsOwnSavedChord() {
  ArbitrationRig rig;
  rig.held = {2, 69};
  Require(rig.Poll() == ClosedMenuOpeningIntent::Quick,
          "Current held Quick was seeded away");
}
void DeadCurrentAimJumpOpensQuickWithoutConfiguredLivingChord() {
  ArbitrationRig rig;
  rig.context.eligibility.life = PlayerLifeState::Dead;
  rig.held = {2, 32};
  Require(rig.Poll() == ClosedMenuOpeningIntent::Quick,
          "Dead current fixed Aim/Jump was seeded away");
}
void MenuPermissionRecoveryChecksTheCurrentHold() {
  ArbitrationRig rig;
  rig.held = {2, 86};
  rig.context.eligibility.hasMenu = false;
  Require(!rig.Poll(), "Missing menu permission opened Main");
  rig.context.eligibility.hasMenu = true;
  Require(rig.Poll() == ClosedMenuOpeningIntent::Main,
          "Permission recovery required an extra press");
}
void ControlsRecoveryChecksTheCurrentHold() {
  ArbitrationRig rig;
  rig.held = {2, 86};
  rig.context.eligibility.controlsDisabled = true;
  Require(!rig.Poll(), "Disabled controls opened Main");
  rig.context.eligibility.controlsDisabled = false;
  Require(rig.Poll() == ClosedMenuOpeningIntent::Main,
          "Controls recovery consumed a source hold");
}
void PendingRetirementChecksCurrentHoldWithoutQueuingAnOldIntent() {
  ArbitrationRig rig;
  rig.held = {2, 86};
  rig.context.commandPending = true;
  Require(!rig.Poll(), "Pending command admitted opening");
  // Read the current chord after retirement rather than replaying the earlier
  // chord
  rig.held = {2, 69};
  rig.context.commandPending = false;
  Require(rig.Poll() == ClosedMenuOpeningIntent::Quick,
          "Pending retirement did not choose current Quick");
}
void MainCurrentLevelRetainsPriorityOverSimultaneousQuick() {
  ArbitrationRig rig;
  rig.held = {2, 86, 69};
  Require(rig.Poll() == ClosedMenuOpeningIntent::Main,
          "Current simultaneous chords lost Main priority");
}
void DisabledQuickCannotSuppressEligibleHeldMain() {
  ArbitrationRig rig;
  rig.policies.quickMenuDisabled = true;
  rig.held = {2, 69};
  Require(!rig.Poll(), "Disabled Quick opened");
  rig.held = {2, 86};
  Require(rig.Poll() == ClosedMenuOpeningIntent::Main,
          "Disabled Quick suppressed current Main");
}
void UnknownLifeNeverBorrowsLivingOrDeadOpening() {
  ArbitrationRig rig;
  rig.context.eligibility.life = PlayerLifeState::Unknown;
  rig.held = {2, 86, 69, 32};
  Require(!rig.Poll(), "Unknown life gained an opening route");
}
void InvalidPollingModeRejectsBeforeAnyKeyCallback() {
  ArbitrationRig rig;
  int reads = 0;
  bool rejected = false;
  try {
    rig.arbitrator.Poll(
        rig.context, rig.policies,
        [&](int) {
          ++reads;
          return true;
        },
        true, static_cast<ClosedOpeningPolling>(99));
  } catch (const std::invalid_argument &) {
    rejected = true;
  }
  Require(rejected && reads == 0,
          "Malformed sampling mode touched physical input");
}

void SourceForegroundLossSkipsKeysAndRequiresNeutralBeforeReturn() {
  ArbitrationRig rig;
  rig.context.foregroundAllowed = false;
  int reads = 0;
  Require(!rig.arbitrator.Poll(
              rig.context, rig.policies,
              [&](int) {
                ++reads;
                return true;
              },
              true, rig.mode),
          "Background source sampler emitted an intent");
  Require(reads == 0, "Background source sampler read external keys");
  rig.context.foregroundAllowed = true;
  rig.held = {2, 86};
  Require(!rig.Poll() && !rig.Poll(),
          "Foreground return reused a discarded source chord");
  rig.held.clear();
  rig.Poll();
  rig.held = {2, 86};
  Require(rig.Poll() == ClosedMenuOpeningIntent::Main,
          "Neutral sample did not restore current opening");
}
void FailedSourceKeyReadCannotRearmHeldOpeningAfterContextRefresh() {
  ArbitrationRig rig;
  bool rejected = false;
  try {
    rig.arbitrator.Poll(
        rig.context, rig.policies,
        [](int) -> bool {
          throw std::runtime_error("Owned fixture read failed");
        },
        true, rig.mode);
  } catch (const std::runtime_error &) {
    rejected = true;
  }
  Require(rejected, "Failed key callback was swallowed");
  // A benign policy refresh cannot erase the failed-read hold barrier
  rig.arbitrator.Reset(false);
  ++rig.policies.revision;
  rig.held = {2, 86};
  Require(!rig.Poll(), "Context refresh revived a failed input read");
  rig.held.clear();
  rig.Poll();
  rig.held = {2, 86};
  Require(rig.Poll() == ClosedMenuOpeningIntent::Main,
          "Release did not recover a failed read");
}
void EnablingSourceSamplingCannotGrantUnknownLifeAnOpening() {
  ArbitrationRig rig;
  rig.mode = ClosedOpeningPolling::FreshEdges;
  rig.context.eligibility.life = PlayerLifeState::Unknown;
  rig.held = {2, 86, 69, 32};
  rig.Poll();
  rig.mode = ClosedOpeningPolling::SourceLevels;
  Require(!rig.Poll(), "Sampling mode became actor permission");
}
void NewValidPolicyChecksItsCurrentChordInsteadOfTheOldTuple() {
  ArbitrationRig rig;
  rig.context.blocked = true;
  rig.held = {2, 86};
  Require(!rig.Poll(), "Blocked old policy opened");
  rig.policies.main.actions = {OpeningAction::Sprint, OpeningAction::Use};
  ++rig.policies.main.revision;
  ++rig.policies.revision;
  rig.context.blocked = false;
  rig.held = {16, 70};
  Require(rig.Poll() == ClosedMenuOpeningIntent::Main,
          "Current valid replacement policy was seeded away");
}
} // namespace
void RegisterArbitrationChecks(void (*add)(const char *, void (*)())) {
  add("SourceForegroundLossSkipsKeysAndRequiresNeutralBeforeReturn",
      SourceForegroundLossSkipsKeysAndRequiresNeutralBeforeReturn);
  add("FailedSourceKeyReadCannotRearmHeldOpeningAfterContextRefresh",
      FailedSourceKeyReadCannotRearmHeldOpeningAfterContextRefresh);
  add("EnablingSourceSamplingCannotGrantUnknownLifeAnOpening",
      EnablingSourceSamplingCannotGrantUnknownLifeAnOpening);
  add("NewValidPolicyChecksItsCurrentChordInsteadOfTheOldTuple",
      NewValidPolicyChecksItsCurrentChordInsteadOfTheOldTuple);
  add("CurrentHeldMainOpensWithoutAnAggregateEdge",
      CurrentHeldMainOpensWithoutAnAggregateEdge);
  add("CurrentHeldQuickUsesItsOwnSavedChord",
      CurrentHeldQuickUsesItsOwnSavedChord);
  add("DeadCurrentAimJumpOpensQuickWithoutConfiguredLivingChord",
      DeadCurrentAimJumpOpensQuickWithoutConfiguredLivingChord);
  add("MenuPermissionRecoveryChecksTheCurrentHold",
      MenuPermissionRecoveryChecksTheCurrentHold);
  add("ControlsRecoveryChecksTheCurrentHold",
      ControlsRecoveryChecksTheCurrentHold);
  add("PendingRetirementChecksCurrentHoldWithoutQueuingAnOldIntent",
      PendingRetirementChecksCurrentHoldWithoutQueuingAnOldIntent);
  add("MainCurrentLevelRetainsPriorityOverSimultaneousQuick",
      MainCurrentLevelRetainsPriorityOverSimultaneousQuick);
  add("DisabledQuickCannotSuppressEligibleHeldMain",
      DisabledQuickCannotSuppressEligibleHeldMain);
  add("UnknownLifeNeverBorrowsLivingOrDeadOpening",
      UnknownLifeNeverBorrowsLivingOrDeadOpening);
  add("InvalidPollingModeRejectsBeforeAnyKeyCallback",
      InvalidPollingModeRejectsBeforeAnyKeyCallback);
}
} // namespace iw::overlay::runtime::tests::closedLevel
