#include "MechanicalMonitorRig.h"
#include <array>
#include <iostream>

namespace iw::overlay::runtime::tests {
namespace {
void NonSliderNextRepeatsAtActualFiftyMillisecondRetirement() {
  MechanicalMonitorRig rig;
  rig.Choice();
  rig.now = 49;
  RequireMonitor(!rig.Poll(), "No-op repeated early");
  rig.Ready(50);
  RequireMonitor(rig.Poll() == Input::NextChoice,
                 "No-op still used the native180 deadline");
  RequireMonitor(!rig.Poll(), "One ready receipt created a same-frame burst");
}
void NonSliderPreviousRepeatsAtActualFiftyMillisecondRetirement() {
  MechanicalMonitorRig rig;
  rig.Choice(MenuMonitorWaitKind::ChoiceNoop, true);
  rig.Ready(50);
  RequireMonitor(rig.Poll() == Input::PreviousChoice,
                 "Previous slot lost source50 continuation");
}
void SliderKeepsItsOwnPublishedOneHundredEightyMillisecondWait() {
  MechanicalMonitorRig rig;
  rig.Choice(MenuMonitorWaitKind::Choice);
  rig.now = 179;
  RequireMonitor(!rig.Poll(), "Slider escaped its actual wait");
  rig.Ready(180);
  RequireMonitor(rig.Poll() == Input::NextChoice,
                 "Slider failed to resume on normal expiry");
}
void DirectionChangedDuringWaitResumesOnlyTheCurrentLevel() {
  MechanicalMonitorRig rig;
  rig.Choice();
  rig.held = {51};
  rig.now = 49;
  rig.Poll();
  rig.Ready(50);
  RequireMonitor(rig.Poll() == Input::PreviousChoice,
                 "Paused reversal needed an extra physical press");
}
void ReleaseAndRepressDuringWaitResumesOnceAfterExpiry() {
  MechanicalMonitorRig rig;
  rig.Choice();
  rig.held.clear();
  rig.now = 20;
  rig.Poll();
  rig.held = {52};
  rig.now = 49;
  rig.Poll();
  rig.Ready(50);
  RequireMonitor(rig.Poll() == Input::NextChoice,
                 "Paused re-press was permanently consumed");
}
void CurrentUseCanWinAfterChoiceRetirement() {
  MechanicalMonitorRig rig;
  rig.Choice();
  rig.held = {70};
  rig.now = 49;
  rig.Poll();
  rig.Ready(50);
  RequireMonitor(rig.Poll() == Input::Select,
                 "Held Use failed to resume after choice");
}
void CurrentScrollRetainsPriorityOverUseChoiceAndKnife() {
  MechanicalMonitorRig rig;
  rig.Choice();
  rig.held = {38, 70, 52, 86};
  rig.now = 49;
  rig.Poll();
  rig.Ready(50);
  RequireMonitor(rig.Poll() == Input::Up,
                 "Mechanical ready changed source branch priority");
}
void ExclusiveChoiceKeepsPriorityOverKnife() {
  MechanicalMonitorRig rig;
  rig.Choice();
  rig.held = {52, 86};
  rig.now = 49;
  rig.Poll();
  rig.Ready(50);
  RequireMonitor(rig.Poll() == Input::NextChoice,
                 "Knife stole an exclusive source slot");
}
void OpposedChoiceSlotsFallThroughToHeldKnife() {
  MechanicalMonitorRig rig;
  rig.Choice();
  rig.held = {51, 52, 86};
  rig.now = 49;
  rig.Poll();
  rig.Ready(50);
  RequireMonitor(rig.Poll() == Input::Back,
                 "Opposed slots blocked the source knife branch");
}
void ChildQuickExitStillNeedsItsAdmittedNinetyMillisecondLoop() {
  MechanicalMonitorRig rig;
  rig.quick = true;
  rig.Choice();
  rig.held = {86};
  rig.now = 49;
  rig.Poll();
  rig.Ready(50);
  RequireMonitor(!rig.Poll(), "Child hold closed immediately");
  rig.now = 139;
  RequireMonitor(!rig.Poll(), "Child hold closed before90");
  rig.now = 140;
  RequireMonitor(rig.Poll() == Input::Close,
                 "Ready knife did not own its90ms loop");
}
void OwnBackParentResumesHeldKnifeAtTwoHundredFiftyMilliseconds() {
  MechanicalMonitorRig rig;
  rig.Back();
  rig.now = 249;
  RequireMonitor(!rig.Poll(), "Back repeated before actual expiry");
  rig.Ready(250);
  RequireMonitor(rig.Poll() == Input::Back,
                 "Own parent transition consumed the held knife");
}
void OwnBackResultMayResumeAHigherCurrentChoice() {
  MechanicalMonitorRig rig;
  rig.Back();
  rig.held = {52};
  rig.now = 249;
  rig.Poll();
  rig.Ready(250);
  RequireMonitor(rig.Poll() == Input::NextChoice,
                 "Own parent readiness failed to sample current source slot");
}
void ClosedRootReceiptCannotCreateOrdinaryOrOpeningPermission() {
  MechanicalMonitorRig rig;
  rig.root = true;
  rig.Poll();
  rig.held = {86};
  RequireMonitor(rig.Poll() == Input::Back, "Root Back missing");
  rig.router.CaptureMonitorDispatch(11);
  rig.ack = 11;
  rig.navigation = 18;
  rig.menu = 32;
  rig.open = false;
  rig.receipt = MenuMonitorContinuation{1, 11, 4, MenuMonitorWaitKind::Back};
  rig.wait = MenuMonitorWait{1, MenuMonitorWaitKind::Back};
  rig.Poll();
  rig.held = {2, 86, 70, 52};
  rig.now = 249;
  rig.Poll();
  rig.Ready(250);
  RequireMonitor(
      !rig.Poll(),
      "Closed Back receipt granted ordinary input or a held opening chord");
}
void MissingReceiptCannotFallbackToTheOldNativeRepeatTimer() {
  MechanicalMonitorRig rig;
  rig.Choice();
  rig.receipt.reset();
  rig.Ready(1000);
  RequireMonitor(!rig.Poll(),
                 "Receipt cancellation silently became legacy180 repetition");
}
void ForeignAcknowledgmentCannotReuseTheOriginalInvocation() {
  MechanicalMonitorRig rig;
  rig.Choice();
  rig.ack = 12;
  rig.Ready(1000);
  RequireMonitor(!rig.Poll(), "Later acknowledgment inherited old choice");
}
void ScopeReplacementRetiresAllInheritedMechanicalHolds() {
  for (int change = 0; change < 5; ++change) {
    MechanicalMonitorRig rig;
    rig.Choice();
    if (change == 0)
      ++rig.navigation;
    if (change == 1)
      ++rig.menu;
    if (change == 2)
      ++rig.eligibility.revision;
    if (change == 3)
      rig.surface = MenuSurface::Quick;
    if (change == 4)
      rig.eligibility.life = PlayerLifeState::Unknown;
    rig.receipt.reset();
    rig.Ready(1000);
    RequireMonitor(!rig.Poll(), "Changed source scope inherited a held choice");
  }
}
void AFailedWriteCannotClaimTheBackendReceipt() {
  MechanicalMonitorRig rig;
  rig.Choice(MenuMonitorWaitKind::ChoiceNoop, false, false);
  rig.Ready(1000);
  RequireMonitor(!rig.Poll(),
                 "A failed write borrowed an unrelated successful receipt");
}
void DescriptorAbsenceDoesNotInventNormalRetirement() {
  MechanicalMonitorRig rig;
  rig.Choice();
  rig.receipt.reset();
  rig.Ready(50);
  RequireMonitor(!rig.Poll(),
                 "Missing mechanical metadata became source readiness");
}
void PendingReadyPacketWaitsWithoutLosingItsCurrentLevel() {
  MechanicalMonitorRig rig;
  rig.Choice();
  rig.Ready(50);
  rig.pending = true;
  RequireMonitor(!rig.Poll(), "Pending command allowed a burst");
  rig.pending = false;
  rig.now = 51;
  RequireMonitor(rig.Poll() == Input::NextChoice,
                 "Pending ready frame erased the same receipt");
}
void NativeDeadlineNeverOverridesAnActiveProducerWait() {
  MechanicalMonitorRig rig;
  rig.Choice();
  rig.now = 100000;
  RequireMonitor(!rig.Poll(),
                 "Native elapsed time ended a producer-owned wait");
}
void NativeClockRollbackConsumesInheritedLevelsUntilRelease() {
  MechanicalMonitorRig rig;
  rig.Choice();
  rig.now = 49;
  rig.Poll();
  rig.Ready(48);
  RequireMonitor(!rig.Poll(), "Native rollback replayed a hold");
  rig.now = 1000;
  RequireMonitor(!rig.Poll(), "Later native time resurrected retired proof");
}
void MalformedReceiptIsRejectedBeforeAnyKeyRead() {
  MechanicalMonitorRig rig;
  rig.receipt =
      MenuMonitorContinuation{1, 11, 4, MenuMonitorWaitKind::ChoiceNoop};
  rig.ack = 10;
  MonitorContinuationObservation observation{&*rig.receipt, rig.ack};
  int reads = 0;
  bool rejected = false;
  try {
    rig.router.Poll(
        true, true, false,
        [&](int) {
          ++reads;
          return false;
        },
        0, 7, false, false, InputMode::Menu, 120, 20, nullptr, 0, 0, nullptr,
        &rig.eligibility, MenuSurface::Main, nullptr, nullptr, &observation);
  } catch (const std::invalid_argument &) {
    rejected = true;
  }
  RequireMonitor(rejected && reads == 0,
                 "Invalid causal receipt sampled physical keys");
}
void LegacyObservationOmissionKeepsExistingOneHundredEightyRepeat() {
  MechanicalMonitorRig rig;
  rig.modern = false;
  rig.Poll();
  rig.held = {52};
  RequireMonitor(rig.Poll() == Input::NextChoice, "Legacy choice missing");
  rig.now = 179;
  RequireMonitor(!rig.Poll(), "Legacy repeat changed its bound");
  rig.now = 180;
  RequireMonitor(rig.Poll() == Input::NextChoice,
                 "Legacy omitted contract lost existing repeat");
}
void ConsecutiveNoopInvocationsEachOwnTheirWrittenSequence() {
  MechanicalMonitorRig rig;
  rig.Choice();
  rig.Ready(50);
  RequireMonitor(rig.Poll() == Input::NextChoice,
                 "First actual50 expiry missing");
  rig.router.CaptureMonitorDispatch(12);
  rig.ack = 12;
  rig.receipt =
      MenuMonitorContinuation{2, 12, 4, MenuMonitorWaitKind::ChoiceNoop};
  rig.wait = MenuMonitorWait{2, MenuMonitorWaitKind::ChoiceNoop};
  rig.Poll();
  rig.now = 99;
  RequireMonitor(!rig.Poll(), "Second iteration ended early");
  rig.Ready(100);
  RequireMonitor(rig.Poll() == Input::NextChoice,
                 "New sequence failed its own50 expiry");
}

void ClosedChildReceiptDoesNotReopenOrReplay() {
  // Close retains the child page while its own wait remains observable
  MechanicalMonitorRig closed;
  closed.Poll();
  closed.held = {86};
  RequireMonitor(closed.Poll() == Input::Back, "Child Back missing");
  closed.router.CaptureMonitorDispatch(11);
  closed.ack = 11;
  closed.open = false;
  closed.navigation = 18;
  closed.menu = 32;
  closed.receipt = MenuMonitorContinuation{1, 11, 4, MenuMonitorWaitKind::Back};
  closed.wait = MenuMonitorWait{1, MenuMonitorWaitKind::Back};
  closed.Poll();
  closed.held = {2, 86, 70, 52};
  closed.Ready(250);
  RequireMonitor(!closed.Poll(), "Closed child replayed an ordinary branch");
}
void FirstReadyPacketBindsOnlyItsOwnActualBackResult() {
  MechanicalMonitorRig rig;
  rig.Poll();
  rig.held = {86};
  RequireMonitor(rig.Poll() == Input::Back, "Child Back missing");
  rig.router.CaptureMonitorDispatch(11);
  rig.ack = 11;
  rig.navigation = 18;
  rig.menu = 32;
  rig.receipt = MenuMonitorContinuation{1, 11, 4, MenuMonitorWaitKind::Back};
  // The consumer may miss all waiting frames without inventing a local deadline
  rig.Ready(250);
  RequireMonitor(rig.Poll() == Input::Back,
                 "Own ready result required a prior wait packet");
}
void IndependentBackResultTopologyChangeCancelsInheritedKnife() {
  MechanicalMonitorRig rig;
  rig.Back();
  ++rig.menu;
  rig.Ready(250);
  RequireMonitor(!rig.Poll(), "A foreign parent row revision inherited knife");
}
void ForeignReceiptRevisionCannotRestartTheOriginalInvocation() {
  MechanicalMonitorRig rig;
  rig.Choice();
  ++rig.receipt->revision;
  rig.Ready(1000);
  RequireMonitor(!rig.Poll(),
                 "A later receipt revision reused the original sequence");
}
void ChangedKnownLifeRetiresTheOriginalInvocation() {
  MechanicalMonitorRig rig;
  rig.Choice();
  rig.eligibility.life = PlayerLifeState::Dead;
  rig.Ready(50);
  RequireMonitor(!rig.Poll(),
                 "A changed actor life inherited its old ready receipt");
}
void FocusLossConsumesHeldInputAfterForegroundReturns() {
  MechanicalMonitorRig rig;
  rig.Choice();
  rig.focused = false;
  rig.now = 49;
  RequireMonitor(!rig.Poll(), "Background input escaped");
  rig.focused = true;
  rig.Ready(50);
  RequireMonitor(!rig.Poll(), "Foreground return rearmed a consumed hold");
  rig.now = 1000;
  RequireMonitor(!rig.Poll(), "Legacy repeat revived a background hold");
}
void OlderReceiptCannotRearmASecondWrittenCommand() {
  MechanicalMonitorRig rig;
  rig.Choice();
  rig.Ready(50);
  RequireMonitor(rig.Poll() == Input::NextChoice, "First repeat missing");
  rig.router.CaptureMonitorDispatch(12);
  rig.ack = 12;
  // A new sequence alone cannot reuse the last observed wait revision
  rig.receipt->sequence = 12;
  rig.now = 1000;
  RequireMonitor(!rig.Poll(), "A second command borrowed an old wait revision");
}
void MismatchedWaitReceiptIsRejectedBeforeKeyReads() {
  MechanicalMonitorRig rig;
  MenuMonitorContinuation receipt{1, 11, 4, MenuMonitorWaitKind::ChoiceNoop};
  MenuMonitorWait wait{2, MenuMonitorWaitKind::ChoiceNoop};
  MonitorContinuationObservation observation{&receipt, 11};
  int reads = 0;
  bool rejected = false;
  try {
    rig.router.Poll(
        true, true, false,
        [&](int) {
          ++reads;
          return false;
        },
        0, 7, false, false, InputMode::Menu, 120, 20, nullptr, 0, 0, nullptr,
        &rig.eligibility, MenuSurface::Main, &wait, nullptr, &observation);
  } catch (const std::invalid_argument &) {
    rejected = true;
  }
  RequireMonitor(rejected && reads == 0,
                 "Mismatched receipt sampled physical keys");
}
void InvalidDispatchCaptureClearsInheritedRepeatTimers() {
  for (const auto invalidSequence : {std::uint64_t{0}, std::uint64_t{12}}) {
    MechanicalMonitorRig rig;
    rig.Poll();
    rig.held = {52};
    RequireMonitor(rig.Poll() == Input::NextChoice, "Original choice missing");
    if (invalidSequence == 12)
      rig.router.CaptureMonitorDispatch(11);
    bool rejected = false;
    try {
      rig.router.CaptureMonitorDispatch(invalidSequence);
    } catch (const std::invalid_argument &) {
      rejected = true;
    }
    RequireMonitor(rejected, "Invalid dispatch capture was accepted");
    rig.now = 1000;
    RequireMonitor(!rig.Poll(),
                   "Invalid capture left the old repeat timer armed");
    rig.now = 2000;
    RequireMonitor(!rig.Poll(), "Invalid capture revived a held repeat later");
  }
}

} // namespace
} // namespace iw::overlay::runtime::tests
int main() {
  using namespace iw::overlay::runtime::tests;
  const std::array tests{
      std::pair{"ClosedChildReceiptDoesNotReopenOrReplay",
                ClosedChildReceiptDoesNotReopenOrReplay},
      std::pair{"FirstReadyPacketBindsOnlyItsOwnActualBackResult",
                FirstReadyPacketBindsOnlyItsOwnActualBackResult},
      std::pair{"IndependentBackResultTopologyChangeCancelsInheritedKnife",
                IndependentBackResultTopologyChangeCancelsInheritedKnife},
      std::pair{"ForeignReceiptRevisionCannotRestartTheOriginalInvocation",
                ForeignReceiptRevisionCannotRestartTheOriginalInvocation},
      std::pair{"ChangedKnownLifeRetiresTheOriginalInvocation",
                ChangedKnownLifeRetiresTheOriginalInvocation},
      std::pair{"FocusLossConsumesHeldInputAfterForegroundReturns",
                FocusLossConsumesHeldInputAfterForegroundReturns},
      std::pair{"OlderReceiptCannotRearmASecondWrittenCommand",
                OlderReceiptCannotRearmASecondWrittenCommand},
      std::pair{"MismatchedWaitReceiptIsRejectedBeforeKeyReads",
                MismatchedWaitReceiptIsRejectedBeforeKeyReads},
      std::pair{"InvalidDispatchCaptureClearsInheritedRepeatTimers",
                InvalidDispatchCaptureClearsInheritedRepeatTimers},
      std::pair{"NonSliderNextRepeatsAtActualFiftyMillisecondRetirement",
                NonSliderNextRepeatsAtActualFiftyMillisecondRetirement},
      std::pair{"NonSliderPreviousRepeatsAtActualFiftyMillisecondRetirement",
                NonSliderPreviousRepeatsAtActualFiftyMillisecondRetirement},
      std::pair{"SliderKeepsItsOwnPublishedOneHundredEightyMillisecondWait",
                SliderKeepsItsOwnPublishedOneHundredEightyMillisecondWait},
      std::pair{"DirectionChangedDuringWaitResumesOnlyTheCurrentLevel",
                DirectionChangedDuringWaitResumesOnlyTheCurrentLevel},
      std::pair{"ReleaseAndRepressDuringWaitResumesOnceAfterExpiry",
                ReleaseAndRepressDuringWaitResumesOnceAfterExpiry},
      std::pair{"CurrentUseCanWinAfterChoiceRetirement",
                CurrentUseCanWinAfterChoiceRetirement},
      std::pair{"CurrentScrollRetainsPriorityOverUseChoiceAndKnife",
                CurrentScrollRetainsPriorityOverUseChoiceAndKnife},
      std::pair{"ExclusiveChoiceKeepsPriorityOverKnife",
                ExclusiveChoiceKeepsPriorityOverKnife},
      std::pair{"OpposedChoiceSlotsFallThroughToHeldKnife",
                OpposedChoiceSlotsFallThroughToHeldKnife},
      std::pair{"ChildQuickExitStillNeedsItsAdmittedNinetyMillisecondLoop",
                ChildQuickExitStillNeedsItsAdmittedNinetyMillisecondLoop},
      std::pair{"OwnBackParentResumesHeldKnifeAtTwoHundredFiftyMilliseconds",
                OwnBackParentResumesHeldKnifeAtTwoHundredFiftyMilliseconds},
      std::pair{"OwnBackResultMayResumeAHigherCurrentChoice",
                OwnBackResultMayResumeAHigherCurrentChoice},
      std::pair{"ClosedRootReceiptCannotCreateOrdinaryOrOpeningPermission",
                ClosedRootReceiptCannotCreateOrdinaryOrOpeningPermission},
      std::pair{"MissingReceiptCannotFallbackToTheOldNativeRepeatTimer",
                MissingReceiptCannotFallbackToTheOldNativeRepeatTimer},
      std::pair{"ForeignAcknowledgmentCannotReuseTheOriginalInvocation",
                ForeignAcknowledgmentCannotReuseTheOriginalInvocation},
      std::pair{"ScopeReplacementRetiresAllInheritedMechanicalHolds",
                ScopeReplacementRetiresAllInheritedMechanicalHolds},
      std::pair{"AFailedWriteCannotClaimTheBackendReceipt",
                AFailedWriteCannotClaimTheBackendReceipt},
      std::pair{"DescriptorAbsenceDoesNotInventNormalRetirement",
                DescriptorAbsenceDoesNotInventNormalRetirement},
      std::pair{"PendingReadyPacketWaitsWithoutLosingItsCurrentLevel",
                PendingReadyPacketWaitsWithoutLosingItsCurrentLevel},
      std::pair{"NativeDeadlineNeverOverridesAnActiveProducerWait",
                NativeDeadlineNeverOverridesAnActiveProducerWait},
      std::pair{"NativeClockRollbackConsumesInheritedLevelsUntilRelease",
                NativeClockRollbackConsumesInheritedLevelsUntilRelease},
      std::pair{"MalformedReceiptIsRejectedBeforeAnyKeyRead",
                MalformedReceiptIsRejectedBeforeAnyKeyRead},
      std::pair{"LegacyObservationOmissionKeepsExistingOneHundredEightyRepeat",
                LegacyObservationOmissionKeepsExistingOneHundredEightyRepeat},
      std::pair{"ConsecutiveNoopInvocationsEachOwnTheirWrittenSequence",
                ConsecutiveNoopInvocationsEachOwnTheirWrittenSequence},
  };
  int failed = 0;
  for (const auto &[name, run] : tests) {
    try {
      run();
      std::cout << "PASS " << name << '\n';
    } catch (const std::exception &error) {
      ++failed;
      std::cout << "FAIL " << name << ": " << error.what() << '\n';
    }
  }
  std::cout << tests.size() - failed << '/' << tests.size()
            << " mechanical input checks passed\n";
  return failed ? 1 : 0;
}
