#include "OrdinarySourceLevelSupport.h"

namespace iw::overlay::runtime::tests::ordinarySource {
namespace {
void ChildKnifeLoopOwnsItsNinetyMillisecondsBeforeTheFinalBackWait() {
    RouterRig rig;
    rig.open = true;
    rig.rootMenu = false;
    rig.quickExitEnabled = true;
    rig.Poll();
    rig.held = {86};
    rig.now = 50;
    Require(!rig.Poll(), "Child knife hold closed before its inner loop");
    // Scrolling has priority only after this admitted inner loop returns
    rig.held = {38, 86};
    rig.now = 60;
    Require(!rig.Poll(), "A higher ordinary branch interrupted the knife loop");
    rig.now = 139;
    Require(!rig.Poll(), "Ten source counts completed before their nine waits");
    rig.now = 140;
    Require(rig.Poll() == Input::Close, "Held child knife did not close at ninety");
    rig.held = {38};
    rig.now = 389;
    Require(!rig.Poll(), "The terminal result shortened its final250 wait");
    rig.now = 390;
    Require(rig.Poll() == Input::Up, "The completed child loop added another fifty wait");
}

void ReleasedChildKnifeStartsItsFinalWaitAtTheObservedRelease() {
    RouterRig rig;
    rig.open = true;
    rig.rootMenu = false;
    rig.quickExitEnabled = true;
    rig.held = {86};
    Require(!rig.Poll(), "Initial child knife did not enter its source loop");
    rig.held = {38};
    rig.now = 20;
    Require(rig.Poll() == Input::Back, "Released child knife did not return to its parent");
    rig.now = 269;
    Require(!rig.Poll(), "Release used the admission timestamp for its final wait");
    rig.now = 270;
    Require(rig.Poll() == Input::Up, "Release readiness failed to evaluate current scrolling");
}

void PartialOrdinaryReleaseCannotClearTheHostNeutralBarrier() {
    RouterRig rig;
    rig.open = true;
    rig.held = {70};
    Require(rig.Poll() == Input::Select, "Initial held Use was not admitted");
    rig.router.Reset();
    rig.held = {38, 70};
    rig.now = 1;
    Require(!rig.Poll(), "Reset replayed an ordinary action before neutral");
    rig.held = {38};
    rig.now = 2;
    Require(!rig.Poll(), "Releasing only Use cleared a still-held scroll action");
    rig.held.clear();
    rig.now = 10;
    Require(!rig.Poll(), "The all-neutral sample emitted an action");
    rig.held = {38};
    rig.now = 59;
    Require(!rig.Poll(), "Neutral recovery did not preserve its fresh50 wait");
    rig.now = 60;
    Require(rig.Poll() == Input::Up, "The all-neutral sample did not restore scrolling");
}

void UnclassifiedHeldUseShadowsLowerBranchesAcrossRowChanges() {
    RouterRig rig;
    rig.open = true;
    rig.held = {70};
    Require(rig.Poll() == Input::Select, "Initial held Use was not admitted");
    rig.held = {70, 52, 86};
    rig.now = 50;
    Require(!rig.Poll(), "Unclassified Use repeated or leaked into a lower branch");
    ++rig.navigation;
    ++rig.menu;
    rig.now = 100;
    Require(!rig.Poll(), "Row replacement turned consumed Use into a new action");
    rig.held = {52, 86};
    rig.now = 150;
    Require(rig.Poll() == Input::NextChoice, "Physical Use release did not restore source priority");
}
}

void RegisterGestureChecks(void (*add)(const char*, void (*)())) {
    add("ChildKnifeLoopOwnsItsNinetyMillisecondsBeforeTheFinalBackWait", ChildKnifeLoopOwnsItsNinetyMillisecondsBeforeTheFinalBackWait);
    add("ReleasedChildKnifeStartsItsFinalWaitAtTheObservedRelease", ReleasedChildKnifeStartsItsFinalWaitAtTheObservedRelease);
    add("PartialOrdinaryReleaseCannotClearTheHostNeutralBarrier", PartialOrdinaryReleaseCannotClearTheHostNeutralBarrier);
    add("UnclassifiedHeldUseShadowsLowerBranchesAcrossRowChanges", UnclassifiedHeldUseShadowsLowerBranchesAcrossRowChanges);
}
}
