#include "MechanicalGraphSupport.h"
#include <array>
#include <iostream>

namespace iw::overlay::tests::mechanicalGraph {
namespace {
void Begin(Router &router, const runtime::ParsedState &before,
           runtime::Input expected, int key) {
  Check(!router.Poll(before, 0), "Unpressed predecessor emitted an action");
  router.held = {key};
  Check(router.Poll(before, 0) == expected,
        "Original mechanical branch missing");
  router.input.CaptureMonitorDispatch(1001);
}
void ActualChoiceReversalThenUseEachOwnsItsNewInvocation(
    const std::filesystem::path &directory) {
  Router router;
  Begin(router, Read(directory / "choice-before0.json"),
        runtime::Input::NextChoice, 52);
  Check(!router.Poll(Read(directory / "choice-next0.json"), 0),
        "Choice escaped its wait");
  router.held = {51};
  Check(!router.Poll(Read(directory / "choice-next179.json"), 179),
        "Reversal escaped179");
  Check(router.Poll(Read(directory / "choice-ready180.json"), 180) ==
            runtime::Input::PreviousChoice,
        "Actual ready slider did not resume the current direction");
  router.input.CaptureMonitorDispatch(1002);
  Check(!router.Poll(Read(directory / "choice-previous180.json"), 180),
        "New direction burst");
  router.held = {70};
  Check(!router.Poll(Read(directory / "choice-previous359.json"), 359),
        "Use escaped its predecessor");
  Check(router.Poll(Read(directory / "choice-ready360.json"), 360) ==
            runtime::Input::Select,
        "Actual choice retirement did not resume current Use");
  router.input.CaptureUseDispatch(1003);
  Check(!router.Poll(Read(directory / "choice-use360.json"), 360),
        "Use borrowed slider readiness");
  Check(!router.Poll(Read(directory / "choice-use609.json"), 609),
        "Use cooldown ended early");
  Check(router.Poll(Read(directory / "choice-use610.json"), 610) ==
            runtime::Input::Select,
        "Actual plain callback failed its own250 retirement");
}
void ActualNonsliderRepeatsAtFiftyWithoutChangingItsBoolean(
    const std::filesystem::path &directory) {
  Router router;
  Begin(router, Read(directory / "noop-before0.json"),
        runtime::Input::NextChoice, 52);
  Check(!router.Poll(Read(directory / "noop-next0.json"), 0), "No-op burst");
  Check(!router.Poll(Read(directory / "noop-next49.json"), 49),
        "No-op ended early");
  Check(router.Poll(Read(directory / "noop-ready50.json"), 50) ==
            runtime::Input::NextChoice,
        "Actual nonslider still waited for a native180 timer");
  router.input.CaptureMonitorDispatch(1002);
  Check(!router.Poll(Read(directory / "noop-repeat50.json"), 50),
        "Second no-op burst");
  Check(!router.Poll(Read(directory / "noop-repeat99.json"), 99),
        "Second no-op borrowed old time");
  Check(router.Poll(Read(directory / "noop-ready100.json"), 100) ==
            runtime::Input::NextChoice,
        "Second nonslider lost its own sequence and50 wait");
}
void ActualHeldKnifeTraversesParentsThenClosesRootWithoutReopening(
    const std::filesystem::path &directory) {
  Router router;
  Begin(router, Read(directory / "back-before0.json"), runtime::Input::Back,
        86);
  Check(!router.Poll(Read(directory / "back-child0.json"), 0),
        "Child Back burst");
  Check(!router.Poll(Read(directory / "back-child249.json"), 249),
        "Child Back ended early");
  Check(router.Poll(Read(directory / "back-child250.json"), 250) ==
            runtime::Input::Back,
        "Own rebuilt parent consumed held knife");
  router.input.CaptureMonitorDispatch(1002);
  Check(!router.Poll(Read(directory / "back-parent250.json"), 250),
        "Parent Back burst");
  Check(!router.Poll(Read(directory / "back-parent499.json"), 499),
        "Parent Back borrowed child wait");
  Check(router.Poll(Read(directory / "back-parent500.json"), 500) ==
            runtime::Input::Back,
        "Own root transition lost held knife");
  router.input.CaptureMonitorDispatch(1003);
  Check(!router.Poll(Read(directory / "back-root500.json"), 500),
        "Closed root replayed Back");
  router.held = {2, 86, 52, 70};
  Check(!router.Poll(Read(directory / "back-root749.json"), 749),
        "Closed root escaped250");
  Check(!router.Poll(Read(directory / "back-root750.json"), 750),
        "Closed receipt granted opening permission");
}
void ActualBackResumesQuickExitKnifeForNinetyThenRetainsClosedChild(
    const std::filesystem::path &directory) {
  Router router;
  const auto before = Read(directory / "close-before0.json");
  // A short child knife tap admits Back before its ninety millisecond hold
  // boundary
  Check(!router.Poll(before, 0), "Unpressed child emitted Back");
  router.held = {86};
  Check(!router.Poll(before, 0), "Quick Exit child skipped its hold loop");
  router.held.clear();
  Check(router.Poll(before, 1) == runtime::Input::Back,
        "Short child knife tap lost Back");
  router.input.CaptureMonitorDispatch(1001);
  Check(!router.Poll(Read(directory / "close-back0.json"), 1),
        "First Back escaped250");
  router.held = {86};
  Check(!router.Poll(Read(directory / "close-ready250.json"), 251),
        "Quick Exit skipped its90 loop");
  Check(!router.Poll(Read(directory / "close-ready339.json"), 340),
        "Quick Exit ended at89");
  Check(router.Poll(Read(directory / "close-ready340.json"), 341) ==
            runtime::Input::Close,
        "Ready parent knife did not close at90");
  router.input.CaptureMonitorDispatch(1002);
  Check(!router.Poll(Read(directory / "close-child340.json"), 341),
        "Closed child replayed a branch");
  Check(!router.Poll(Read(directory / "close-child589.json"), 590),
        "Closed child ended early");
  Check(!router.Poll(Read(directory / "close-child590.json"), 591),
        "Closed child receipt reopened its retained page");
}
void ActualDeadQuickJumpRetainsOnlyItsClosedBackWait(
    const std::filesystem::path &directory) {
  Router router;
  Begin(router, Read(directory / "jump-before0.json"), runtime::Input::JumpBack,
        32);
  Check(!router.Poll(Read(directory / "jump0.json"), 0),
        "Closed Jump replayed");
  Check(!router.Poll(Read(directory / "jump249.json"), 249),
        "Jump lost250 wait");
  Check(!router.Poll(Read(directory / "jump250.json"), 250),
        "Dead closed Jump granted ordinary input");
}
void ActualFocusAndIndependentNavigationWithdrawReadyProof(
    const std::filesystem::path &directory) {
  Router router;
  Begin(router, Read(directory / "withdraw-before0.json"),
        runtime::Input::NextChoice, 52);
  Check(!router.Poll(Read(directory / "withdraw-choice0.json"), 0),
        "Withdraw predecessor burst");
  router.foreground = false;
  Check(!router.Poll(Read(directory / "withdraw-ready180.json"), 180),
        "Background held slot ran");
  Check(!router.Poll(Read(directory / "withdraw-focus180.json"), 180),
        "Focus withdrawal ran");
  router.foreground = true;
  Check(!router.Poll(Read(directory / "withdraw-return180.json"), 180),
        "Foreground return rearmed a hold");
  Check(!router.Poll(Read(directory / "withdraw-nav180.json"), 180),
        "Independent navigation inherited proof");
  Check(!router.Poll(Read(directory / "withdraw-nav180.json"), 1000),
        "Old native timer revived after withdrawal");
}
void ActualBackendClockRollbackRestartsWithoutNativeTimeEndingWait(
    const std::filesystem::path &directory) {
  Router router;
  Begin(router, Read(directory / "rollback-before0.json"),
        runtime::Input::NextChoice, 52);
  Check(!router.Poll(Read(directory / "rollback-choice0.json"), 0),
        "Rollback predecessor burst");
  Check(!router.Poll(Read(directory / "rollback100.json"), 100),
        "Rollback wait ended early");
  // Consumer time stays monotonic while the independent producer clock goes
  // back
  Check(!router.Poll(Read(directory / "rollback10.json"), 101),
        "Producer rollback invented readiness");
  Check(!router.Poll(Read(directory / "rollback189.json"), 1000),
        "Native elapsed time ended restarted producer wait");
  Check(router.Poll(Read(directory / "rollback190.json"), 1001) ==
            runtime::Input::NextChoice,
        "Normally restarted producer wait lost its exact causal sequence");
}
void ActualUnsupportedAmmoSliderKeepsUnavailableAndItsOwnAttemptPause(
    const std::filesystem::path &directory) {
  Router router;
  Begin(router, Read(directory / "ammo-before0.json"),
        runtime::Input::NextChoice, 52);
  Check(!router.Poll(Read(directory / "ammo-choice0.json"), 0),
        "Unsupported slider burst");
  Check(!router.Poll(Read(directory / "ammo-choice179.json"), 179),
        "Unsupported attempt escaped180");
  Check(router.Poll(Read(directory / "ammo-ready180.json"), 180) ==
            runtime::Input::NextChoice,
        "Actual declared slider lost its attempt pause");
}
} // namespace
} // namespace iw::overlay::tests::mechanicalGraph
int main(int argc, char **argv) {
  if (argc != 2)
    return 2;
  using namespace iw::overlay::tests::mechanicalGraph;
  const std::array tests{
      std::pair{"ActualMechanicalInventoryMatchesLiteralOraclesAndBothReaders",
                ActualMechanicalInventoryMatchesLiteralOraclesAndBothReaders},
      std::pair{"ActualChoiceReversalThenUseEachOwnsItsNewInvocation",
                ActualChoiceReversalThenUseEachOwnsItsNewInvocation},
      std::pair{"ActualNonsliderRepeatsAtFiftyWithoutChangingItsBoolean",
                ActualNonsliderRepeatsAtFiftyWithoutChangingItsBoolean},
      std::pair{"ActualHeldKnifeTraversesParentsThenClosesRootWithoutReopening",
                ActualHeldKnifeTraversesParentsThenClosesRootWithoutReopening},
      std::pair{
          "ActualBackResumesQuickExitKnifeForNinetyThenRetainsClosedChild",
          ActualBackResumesQuickExitKnifeForNinetyThenRetainsClosedChild},
      std::pair{"ActualDeadQuickJumpRetainsOnlyItsClosedBackWait",
                ActualDeadQuickJumpRetainsOnlyItsClosedBackWait},
      std::pair{"ActualFocusAndIndependentNavigationWithdrawReadyProof",
                ActualFocusAndIndependentNavigationWithdrawReadyProof},
      std::pair{"ActualBackendClockRollbackRestartsWithoutNativeTimeEndingWait",
                ActualBackendClockRollbackRestartsWithoutNativeTimeEndingWait},
      std::pair{
          "ActualUnsupportedAmmoSliderKeepsUnavailableAndItsOwnAttemptPause",
          ActualUnsupportedAmmoSliderKeepsUnavailableAndItsOwnAttemptPause}};
  int failed = 0;
  for (const auto &[name, run] : tests) {
    try {
      run(argv[1]);
      std::cout << "PASS " << name << '\n';
    } catch (const std::exception &error) {
      ++failed;
      std::cout << "FAIL " << name << ": " << error.what() << '\n';
    }
  }
  std::cout << tests.size() - failed << '/' << tests.size()
            << " actual mechanical graph checks passed\n";
  return failed ? 1 : 0;
}
