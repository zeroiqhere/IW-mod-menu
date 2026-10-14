#include "OrdinarySourceLevelSupport.h"
#include <array>

namespace iw::overlay::runtime::tests::ordinarySource {
namespace {
void InitialOpenSampleUsesHeldUseScrollChoiceAndKnifePredicates() {
  const std::array cases{
      std::pair{std::set<int>{70}, Input::Select},
      std::pair{std::set<int>{38}, Input::Up},
      std::pair{std::set<int>{52}, Input::NextChoice},
      std::pair{std::set<int>{86}, Input::Back}};
  for (const auto &[keys, expected] : cases) {
    RouterRig rig;
    rig.open = true;
    rig.held = keys;
    Require(rig.Poll() == expected,
            "First open source sample ignored its current held predicate");
  }
}

void GenericHeldUseWaitsForItsActualCallbackBeforeRepeating() {
  RouterRig rig;
  rig.open = true;
  rig.held = {70};
  Require(rig.Poll() == Input::Select,
          "Initial current Use was not admitted");
  rig.now = 50;
  Require(!rig.Poll(),
          "An untyped Select repeated from an assumed source wait");
  rig.now = 500;
  Require(!rig.Poll(),
          "An unowned generic Select became a held repeat receipt");
}

void DeadJumpUsesTheQuickSurfaceBranch() {
  RouterRig rig;
  rig.open = true;
  rig.surface = MenuSurface::Quick;
  rig.admission.life = PlayerLifeState::Dead;
  Require(!rig.Poll(), "Neutral dead Quick sample emitted input");
  rig.held = {32};
  rig.now = 20;
  Require(!rig.Poll(), "Dead Quick Jump bypassed the current open iteration");
  rig.now = 49;
  Require(!rig.Poll(), "Dead Quick Jump ran before the next source cycle");
  rig.now = 50;
  Require(rig.Poll() == Input::JumpBack,
          "Held Jump did not enter the dead Quick source branch");
}

} // namespace

void RegisterCurrentLevelChecks(void (*add)(const char *, void (*)())) {
  add("InitialOpenSampleUsesHeldUseScrollChoiceAndKnifePredicates",
      InitialOpenSampleUsesHeldUseScrollChoiceAndKnifePredicates);
  add("GenericHeldUseWaitsForItsActualCallbackBeforeRepeating",
      GenericHeldUseWaitsForItsActualCallbackBeforeRepeating);
  add("DeadJumpUsesTheQuickSurfaceBranch", DeadJumpUsesTheQuickSurfaceBranch);
}
} // namespace iw::overlay::runtime::tests::ordinarySource
