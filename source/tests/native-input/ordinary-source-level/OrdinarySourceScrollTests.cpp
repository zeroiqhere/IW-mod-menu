#include "OrdinarySourceLevelSupport.h"
#include <tuple>

namespace iw::overlay::runtime::tests::ordinarySource {
namespace {
void ScrollIntervalUsesAnimationOverheadAndDirectionSpacerHalf() {
  for (const auto [key, skipped, animation, interval, expected] : {
           std::tuple{38, 2U, 120, 315U, Input::Up},
           std::tuple{40, 1U, 140, 285U, Input::Down}}) {
    RouterRig rig;
    rig.open = true;
    rig.animationMilliseconds = animation;
    if (expected == Input::Up)
      rig.skippedUp = skipped;
    else
      rig.skippedDown = skipped;
    Require(!rig.Poll(), "Unpressed first open cycle emitted input");
    rig.held = {key};
    rig.now = 50;
    Require(rig.Poll() == expected, "Current scroll was missing at its source cycle");
    rig.now = 50 + interval - 1;
    Require(!rig.Poll(), "Scroll repeated before animation, overhead and spacer waits");
    rig.now = 50 + interval;
    Require(rig.Poll() == expected, "Scroll did not repeat at its full source interval");
  }
}

void ReleasedScrollWaitResumesTheCurrentHigherBranchOnce() {
  RouterRig rig;
  rig.open = true;
  Require(!rig.Poll(), "Unpressed first open cycle emitted input");
  rig.held = {38};
  rig.now = 50;
  Require(rig.Poll() == Input::Up, "Initial Up scroll was missing");
  rig.held = {70};
  rig.now = 60;
  Require(!rig.Poll(), "Use interrupted the active source scroll wait");
  rig.now = 244;
  Require(!rig.Poll(), "Released scroll ended before its complete wait");
  rig.now = 245;
  Require(rig.Poll() == Input::Select,
          "The current higher branch paid a second wait after scrolling");
}

void MenuRevisionDoesNotRestartAnOwnedScrollInterval() {
  RouterRig rig;
  rig.open = true;
  rig.skippedUp = 1;
  Require(!rig.Poll(), "Unpressed first open cycle emitted input");
  rig.held = {38};
  rig.now = 50;
  Require(rig.Poll() == Input::Up, "Initial Up scroll was missing");
  ++rig.menu;
  rig.now = 80;
  Require(!rig.Poll(), "A menu revision bypassed the scroll wait");
  rig.now = 304;
  Require(!rig.Poll(), "Menu revision shortened the retained scroll interval");
  rig.now = 305;
  Require(rig.Poll() == Input::Up,
          "Menu revision discarded or restarted the source scroll interval");
}

void SourceScrollXorRejectsBothDirections() {
  RouterRig rig;
  rig.open = true;
  Require(!rig.Poll(), "Unpressed first open cycle emitted input");
  rig.held = {38, 40};
  rig.now = 50;
  Require(!rig.Poll(), "Opposed scroll directions passed the source XOR branch");
}

void SourceBranchPriorityKeepsScrollUseChoiceAndKnifeOrder() {
  for (const auto &[keys, expected] : {
           std::pair{std::set<int>{38, 70, 52, 86}, Input::Up},
           std::pair{std::set<int>{70, 52, 86}, Input::Select},
           std::pair{std::set<int>{52, 86}, Input::NextChoice},
           std::pair{std::set<int>{51, 52, 86}, Input::Back}}) {
    RouterRig rig;
    rig.open = true;
    Require(!rig.Poll(), "Unpressed first open cycle emitted input");
    rig.held = keys;
    rig.now = 50;
    Require(rig.Poll() == expected,
            "Ordinary source branch priority or exclusive predicate changed");
  }
}
} // namespace

void RegisterScrollChecks(void (*add)(const char *, void (*)())) {
  add("ScrollIntervalUsesAnimationOverheadAndDirectionSpacerHalf",
      ScrollIntervalUsesAnimationOverheadAndDirectionSpacerHalf);
  add("ReleasedScrollWaitResumesTheCurrentHigherBranchOnce",
      ReleasedScrollWaitResumesTheCurrentHigherBranchOnce);
  add("MenuRevisionDoesNotRestartAnOwnedScrollInterval",
      MenuRevisionDoesNotRestartAnOwnedScrollInterval);
  add("SourceScrollXorRejectsBothDirections", SourceScrollXorRejectsBothDirections);
  add("SourceBranchPriorityKeepsScrollUseChoiceAndKnifeOrder",
      SourceBranchPriorityKeepsScrollUseChoiceAndKnifeOrder);
}
} // namespace iw::overlay::runtime::tests::ordinarySource
