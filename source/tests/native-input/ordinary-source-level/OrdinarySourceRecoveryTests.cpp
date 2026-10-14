#include "OrdinarySourceLevelSupport.h"

namespace iw::overlay::runtime::tests::ordinarySource {
namespace {
enum class Recovery { Reset, Focus, ReadFailure, BackwardTime };

void HeldUseNeedsNeutralAndFreshFiftyAfterRecovery(Recovery recovery) {
  RouterRig rig;
  rig.open = true;
  Require(!rig.Poll(), "Unpressed first open cycle emitted input");
  rig.held = {70};
  rig.now = 50;
  Require(rig.Poll() == Input::Select, "Initial held Use was not admitted");

  if (recovery == Recovery::Reset) {
    rig.now = 51;
    rig.router.Reset();
  } else if (recovery == Recovery::Focus) {
    rig.foreground = false;
    rig.now = 51;
    Require(!rig.Poll(), "Focus loss emitted held Use");
    rig.foreground = true;
    rig.now = 52;
  } else if (recovery == Recovery::ReadFailure) {
    rig.failRead = true;
    rig.now = 51;
    bool rejected = false;
    try {
      rig.Poll();
    } catch (const std::runtime_error &) {
      rejected = true;
    }
    Require(rejected, "A saved action read failure was not observed");
    rig.failRead = false;
    rig.now = 52;
  } else {
    rig.now = 49;
    Require(!rig.Poll(), "Backward time emitted held Use");
    rig.now = 50;
  }

  Require(!rig.Poll(), "Recovery replayed held Use before neutral");
  rig.now = 60;
  Require(!rig.Poll(), "A continuing hold crossed the neutral barrier");
  rig.held.clear();
  rig.now = 70;
  Require(!rig.Poll(), "The required neutral sample emitted input");
  rig.held = {70};
  rig.now = 71;
  Require(!rig.Poll(), "Neutral recovery bypassed its fresh fifty wait");
  rig.now = 119;
  Require(!rig.Poll(), "Recovered source cycle ended before fifty");
  rig.now = 120;
  Require(rig.Poll() == Input::Select,
          "Neutral release did not restore current Use at fifty");
}

void SourceLevelsWithoutTypedOwnerCannotUseOrdinaryEdgeFallback() {
  RouterRig rig;
  rig.open = true;
  rig.hasAdmission = false;
  Require(!rig.Poll(), "Ownerless source sample emitted input");
  rig.held = {70};
  rig.now = 50;
  Require(!rig.Poll(),
          "SourceLevels without typed ownership fell back to ordinary Select");
}

void ResetRecoveryRequiresOneNeutralSampleBeforeRestartingTheCycle() {
  HeldUseNeedsNeutralAndFreshFiftyAfterRecovery(Recovery::Reset);
}
void FocusRecoveryRequiresOneNeutralSampleBeforeRestartingTheCycle() {
  HeldUseNeedsNeutralAndFreshFiftyAfterRecovery(Recovery::Focus);
}
void FailedReadRequiresOneNeutralSampleBeforeRestartingTheCycle() {
  HeldUseNeedsNeutralAndFreshFiftyAfterRecovery(Recovery::ReadFailure);
}
void BackwardTimeRequiresOneNeutralSampleBeforeRestartingTheCycle() {
  HeldUseNeedsNeutralAndFreshFiftyAfterRecovery(Recovery::BackwardTime);
}
} // namespace

void RegisterRecoveryChecks(void (*add)(const char *, void (*)())) {
  add("ResetRecoveryRequiresOneNeutralSampleBeforeRestartingTheCycle",
      ResetRecoveryRequiresOneNeutralSampleBeforeRestartingTheCycle);
  add("FocusRecoveryRequiresOneNeutralSampleBeforeRestartingTheCycle",
      FocusRecoveryRequiresOneNeutralSampleBeforeRestartingTheCycle);
  add("FailedReadRequiresOneNeutralSampleBeforeRestartingTheCycle",
      FailedReadRequiresOneNeutralSampleBeforeRestartingTheCycle);
  add("BackwardTimeRequiresOneNeutralSampleBeforeRestartingTheCycle",
      BackwardTimeRequiresOneNeutralSampleBeforeRestartingTheCycle);
  add("SourceLevelsWithoutTypedOwnerCannotUseOrdinaryEdgeFallback",
      SourceLevelsWithoutTypedOwnerCannotUseOrdinaryEdgeFallback);
}
} // namespace iw::overlay::runtime::tests::ordinarySource
