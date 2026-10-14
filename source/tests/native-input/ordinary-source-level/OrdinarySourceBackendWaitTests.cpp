#include "OrdinarySourceLevelSupport.h"

namespace iw::overlay::runtime::tests::ordinarySource {
namespace {
void AdmitChoiceWait(RouterRig &rig, MenuMonitorWaitKind kind,
                     std::uint64_t sequence = 11) {
  rig.open = true;
  rig.Poll();
  rig.held = {52};
  rig.now = 50;
  Require(rig.Poll() == Input::NextChoice,
          "Current choice was not admitted at its source cycle");
  rig.router.CaptureMonitorDispatch(sequence);
  rig.ack = sequence;
  rig.receipt = MenuMonitorContinuation{1, sequence, 4, kind};
  rig.wait = MenuMonitorWait{1, kind};
  rig.now = 51;
  Require(!rig.Poll(), "An actual choice wait admitted another command");
}

void ChoiceNoopUsesItsActualFiftyWaitWithoutAnotherOuterWait() {
  RouterRig rig;
  AdmitChoiceWait(rig, MenuMonitorWaitKind::ChoiceNoop);
  rig.now = 99;
  Require(!rig.Poll(), "A no-op choice escaped before its actual fifty wait");
  rig.wait.reset();
  rig.now = 100;
  Require(rig.Poll() == Input::NextChoice,
          "Actual no-op completion appended another source wait");
}

void SliderChoiceUsesItsActualOneEightyWaitWithoutAnotherOuterWait() {
  RouterRig rig;
  AdmitChoiceWait(rig, MenuMonitorWaitKind::Choice);
  rig.now = 229;
  Require(!rig.Poll(), "Slider choice escaped before its actual180 wait");
  rig.wait.reset();
  rig.now = 230;
  Require(rig.Poll() == Input::NextChoice,
          "Actual slider completion appended another source wait");
}

void BackUsesItsActualTwoHundredFiftyWaitAtTheResultingParent() {
  RouterRig rig;
  rig.open = true;
  rig.rootMenu = false;
  Require(!rig.Poll(), "Unpressed first open cycle emitted input");
  rig.held = {86};
  rig.now = 50;
  Require(rig.Poll() == Input::Back, "Original child Back was not admitted");
  rig.router.CaptureMonitorDispatch(11);
  rig.ack = 11;
  ++rig.navigation;
  ++rig.menu;
  rig.rootMenu = true;
  rig.receipt = MenuMonitorContinuation{1, 11, 4, MenuMonitorWaitKind::Back};
  rig.wait = MenuMonitorWait{1, MenuMonitorWaitKind::Back};
  rig.held = {70};
  rig.now = 51;
  Require(!rig.Poll(), "The accepted Back bypassed its own wait");
  rig.now = 299;
  Require(!rig.Poll(), "The parent visit escaped before its actual250 wait");
  rig.wait.reset();
  rig.now = 300;
  Require(rig.Poll() == Input::Select,
          "Actual Back readiness added another ordinary fifty wait");
}

void HeldUseResumesOnlyWhenItsSameCallbackReceiptIsReady() {
  RouterRig rig;
  rig.open = true;
  Require(!rig.Poll(), "Unpressed first open cycle emitted input");
  rig.held = {70};
  rig.now = 50;
  Require(rig.Poll() == Input::Select, "Current Use was not admitted");
  rig.router.CaptureUseDispatch(11);
  rig.ack = 11;
  rig.useContinuation = MenuUseContinuation{1, 11, 4,
                                            MenuOptionCallbackKind::Plain};
  rig.callback = MenuOptionCallback{1, MenuOptionCallbackKind::Plain,
                                    MenuOptionCallbackPhase::Cooldown};
  rig.inputMode = InputMode::MenuLocked;
  rig.admission.controlsDisabled = true;
  rig.now = 51;
  Require(!rig.Poll(), "A typed callback wait admitted held Use");
  rig.now = 500;
  Require(!rig.Poll(), "Native elapsed time overrode the live callback");
  rig.callback.reset();
  rig.inputMode = InputMode::Menu;
  rig.admission.controlsDisabled = false;
  rig.now = 501;
  Require(rig.Poll() == Input::Select,
          "The same actual callback receipt did not resume current held Use");
}
} // namespace

void RegisterBackendWaitChecks(void (*add)(const char *, void (*)())) {
  add("ChoiceNoopUsesItsActualFiftyWaitWithoutAnotherOuterWait",
      ChoiceNoopUsesItsActualFiftyWaitWithoutAnotherOuterWait);
  add("SliderChoiceUsesItsActualOneEightyWaitWithoutAnotherOuterWait",
      SliderChoiceUsesItsActualOneEightyWaitWithoutAnotherOuterWait);
  add("BackUsesItsActualTwoHundredFiftyWaitAtTheResultingParent",
      BackUsesItsActualTwoHundredFiftyWaitAtTheResultingParent);
  add("HeldUseResumesOnlyWhenItsSameCallbackReceiptIsReady",
      HeldUseResumesOnlyWhenItsSameCallbackReceiptIsReady);
}
} // namespace iw::overlay::runtime::tests::ordinarySource
