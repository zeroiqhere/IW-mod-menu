#include "ClosedLevelOpeningSupport.h"
#include <algorithm>
namespace iw::overlay::runtime::tests::closedLevel {
namespace {
void FirstVerifiedClosedPacketChecksHeldMain() {
  RouterRig rig;
  rig.held = {2, 86};
  Require(rig.Poll() == Input::OpenMain,
          "Verified current Main was seeded away by router");
}
void OwnRootBackBlocksAt249ThenIndependentPolicyMayOpenAt250() {
  RouterRig rig;
  rig.open = true;
  rig.Poll();
  rig.held = {86};
  rig.now = 49;
  Require(!rig.Poll(), "Root knife bypassed its idle fifty");
  rig.now = 50;
  Require(rig.Poll() == Input::Back, "Original root Back missing");
  rig.router.CaptureMonitorDispatch(11);
  rig.ack = 11;
  rig.open = false;
  ++rig.navigation;
  ++rig.menu;
  rig.receipt = MenuMonitorContinuation{1, 11, 4, MenuMonitorWaitKind::Back};
  rig.wait = MenuMonitorWait{1, MenuMonitorWaitKind::Back};
  rig.held = {2, 86};
  Require(!rig.Poll(), "Closed Back wait granted opening");
  rig.now = 299;
  Require(!rig.Poll(), "Held chord bypassed final Back250");
  // Current opening policy and admission supply permission independently of the
  // Back receipt
  rig.now = 300;
  rig.wait.reset();
  Require(rig.Poll() == Input::OpenMain,
          "Ready Back required release or appended another50");
}
void SourceModeWithoutAdmissionCannotFallbackToLegacyToggle() {
  RouterRig rig;
  rig.hasAdmission = false;
  rig.Poll();
  rig.held = {2, 86};
  Require(!rig.Poll(),
          "Source mode without actor admission fell through to Toggle");
}
void SourceModeWithoutPolicyCannotUseAReceiptAsPermission() {
  RouterRig rig;
  rig.hasPolicy = false;
  rig.held = {2, 86};
  rig.ack = 11;
  rig.receipt = MenuMonitorContinuation{1, 11, 4, MenuMonitorWaitKind::Back};
  Require(!rig.Poll(), "Receipt invented a missing opening policy");
  rig.now = 1000;
  Require(!rig.Poll(), "Closed receipt gained later opening permission");
}
void CurrentVerifiedAdmissionReplacementReevaluatesHeldOpening() {
  RouterRig rig;
  rig.admission.hasMenu = false;
  rig.held = {2, 86};
  Require(!rig.Poll(), "Unavailable admission opened");
  ++rig.admission.revision;
  rig.admission.hasMenu = true;
  rig.now = 20;
  Require(!rig.Poll(), "Current permission bypassed the source idle wait");
  rig.now = 50;
  Require(rig.Poll() == Input::OpenMain,
          "New current permission needed an inherited edge");
}
void SourceLevelsReuseOnePhysicalSampleOnContextChanges() {
  RouterRig rig;
  rig.held = {2, 86};
  rig.Poll();
  Require(std::all_of(rig.reads.begin(), rig.reads.end(),
                      [](unsigned count) { return count <= 1; }),
          "Current-level arbitration sampled physical keys twice");
}
void LegacyDefaultRetainsFreshReleaseRequirement() {
  RouterRig rig;
  rig.mode = ClosedOpeningPolling::FreshEdges;
  rig.held = {2, 86};
  Require(!rig.Poll() && !rig.Poll(), "Legacy initial hold opened");
  rig.held.clear();
  rig.Poll();
  rig.held = {2, 86};
  Require(rig.Poll() == Input::OpenMain && !rig.Poll(),
          "Legacy fresh route changed");
}
void PendingAfterSuccessfulOpeningPreventsRepeatedRequests() {
  RouterRig rig;
  rig.held = {2, 86};
  Require(rig.Poll() == Input::OpenMain, "Original current opening missing");
  rig.pending = true;
  Require(!rig.Poll(), "Successful pending write burst another request");
  rig.open = true;
  rig.pending = false;
  ++rig.navigation;
  Require(!rig.Poll(),
          "Opening result replayed a closed chord as ordinary input");
}
void ForegroundLossRemainsReleaseBoundAsAHostAdaptation() {
  RouterRig rig;
  rig.Poll();
  rig.foreground = false;
  rig.held = {2, 86};
  Require(!rig.Poll(), "Background chord ran");
  rig.foreground = true;
  Require(!rig.Poll() && !rig.Poll(),
          "Foreground return replayed a discarded source hold");
  // A later release restores independent current opening intent
  rig.held.clear();
  rig.Poll();
  rig.held = {2, 86};
  rig.now = 50;
  Require(rig.Poll() == Input::OpenMain,
          "Foreground release failed to restore opening");
}
void InvalidRouterPollingModeRejectsBeforePhysicalKeys() {
  RouterRig rig;
  rig.mode = static_cast<ClosedOpeningPolling>(99);
  bool rejected = false;
  try {
    rig.Poll();
  } catch (const std::invalid_argument &) {
    rejected = true;
  }
  Require(rejected && std::all_of(rig.reads.begin(), rig.reads.end(),
                                  [](unsigned n) { return n == 0; }),
          "Invalid router mode sampled input");
}
void RequireInvalidModeRejectsAndConsumesHeldOpening(RouterRig &rig) {
  rig.held = {2, 86};
  bool rejected = false;
  try {
    rig.Poll();
  } catch (const std::invalid_argument &) {
    rejected = true;
  }
  Require(rejected && std::all_of(rig.reads.begin(), rig.reads.end(),
                                  [](unsigned count) { return count == 0; }),
          "Invalid mode contract read physical keys or failed to reject");
  // Restoring a valid mode must not reuse the chord from a rejected call
  rig.inputMode = InputMode::Menu;
  Require(!rig.Poll() && !rig.Poll(),
          "Invalid mode recovery replayed a held opening chord");
  rig.held.clear();
  rig.Poll();
  rig.held = {2, 86};
  rig.now = 50;
  Require(rig.Poll() == Input::OpenMain,
          "Neutral release failed to restore valid opening");
}
void UnknownInputModeRejectsBeforeKeysAndRequiresNeutralRecovery() {
  RouterRig rig;
  rig.inputMode = static_cast<InputMode>(99);
  RequireInvalidModeRejectsAndConsumesHeldOpening(rig);
}
void MissingEditorBindingsRejectBeforeKeysAndRequireNeutralRecovery() {
  // Each editor owns a different required binding group
  for (const auto editor : {InputMode::WidthEditor,
                            InputMode::InstructionPositionEditor,
                            InputMode::MenuPositionEditor}) {
    auto bindings = SavedBindings();
    if (editor == InputMode::WidthEditor)
      bindings.attack.clear();
    else if (editor == InputMode::InstructionPositionEditor)
      bindings.moveUp.clear();
    else
      bindings.frag.clear();
    RouterRig rig(std::move(bindings));
    rig.inputMode = editor;
    RequireInvalidModeRejectsAndConsumesHeldOpening(rig);
  }
}
} // namespace
void RegisterRouterChecks(void (*add)(const char *, void (*)())) {
  add("FirstVerifiedClosedPacketChecksHeldMain",
      FirstVerifiedClosedPacketChecksHeldMain);
  add("OwnRootBackBlocksAt249ThenIndependentPolicyMayOpenAt250",
      OwnRootBackBlocksAt249ThenIndependentPolicyMayOpenAt250);
  add("SourceModeWithoutAdmissionCannotFallbackToLegacyToggle",
      SourceModeWithoutAdmissionCannotFallbackToLegacyToggle);
  add("SourceModeWithoutPolicyCannotUseAReceiptAsPermission",
      SourceModeWithoutPolicyCannotUseAReceiptAsPermission);
  add("CurrentVerifiedAdmissionReplacementReevaluatesHeldOpening",
      CurrentVerifiedAdmissionReplacementReevaluatesHeldOpening);
  add("SourceLevelsReuseOnePhysicalSampleOnContextChanges",
      SourceLevelsReuseOnePhysicalSampleOnContextChanges);
  add("LegacyDefaultRetainsFreshReleaseRequirement",
      LegacyDefaultRetainsFreshReleaseRequirement);
  add("PendingAfterSuccessfulOpeningPreventsRepeatedRequests",
      PendingAfterSuccessfulOpeningPreventsRepeatedRequests);
  add("ForegroundLossRemainsReleaseBoundAsAHostAdaptation",
      ForegroundLossRemainsReleaseBoundAsAHostAdaptation);
  add("InvalidRouterPollingModeRejectsBeforePhysicalKeys",
      InvalidRouterPollingModeRejectsBeforePhysicalKeys);
  add("UnknownInputModeRejectsBeforeKeysAndRequiresNeutralRecovery",
      UnknownInputModeRejectsBeforeKeysAndRequiresNeutralRecovery);
  add("MissingEditorBindingsRejectBeforeKeysAndRequireNeutralRecovery",
      MissingEditorBindingsRejectBeforeKeysAndRequireNeutralRecovery);
}
} // namespace iw::overlay::runtime::tests::closedLevel
