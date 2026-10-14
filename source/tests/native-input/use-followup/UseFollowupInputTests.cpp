#include "UseFollowupRig.h"
#include <array>
#include <iostream>

using namespace iw::overlay;
using namespace iw::overlay::runtime;
using namespace iw::overlay::runtime::tests;
namespace {
void ReadyPlainAndBooleanResumeEitherExclusiveChoiceOnce() {
    for(auto kind:{MenuOptionCallbackKind::Plain,MenuOptionCallbackKind::Boolean})for(int key:{51,52}) {
        UseFollowupRig rig;rig.Admit(kind);rig.Hold({key});rig.Ready();
        Require(rig.Poll()==(key==51?Input::PreviousChoice:Input::NextChoice),"Ready callback lost the current held choice");
        Require(!rig.Poll(),"Ready choice emitted a same-frame burst");
        // A newly admitted choice owns its own cadence rather than the completed Select wait
        rig.now=679;Require(!rig.Poll(),"New choice borrowed the preceding Use wait");
        rig.now=680;Require(rig.Poll()==(key==51?Input::PreviousChoice:Input::NextChoice),"New choice lost its own180ms cadence");
    }
}
void OpposingChoicesAreNeutralUntilTheCurrentDirectionBecomesExclusive() {
    UseFollowupRig rig;rig.Admit();rig.Hold({51,52});rig.Ready();
    Require(!rig.Poll(),"Opposed source slots invented a direction");
    rig.held={52};++rig.now;Require(rig.Poll()==Input::NextChoice,"Neutral slots consumed the ready iteration");
}
void ReadySelectStillPrecedesChoiceAndKnife() {
    UseFollowupRig rig;rig.Admit();rig.Hold({70,52,86});rig.Ready();
    Require(rig.Poll()==Input::Select,"Ready lower branch bypassed held Use");
}
void ReadyScrollStillPrecedesChoiceAndKnife() {
    UseFollowupRig rig;rig.Admit();rig.Hold({38,52,86});rig.Ready();
    Require(rig.Poll()==Input::Up,"Ready lower branch bypassed held scroll");
}
void ReadyChoicePrecedesKnifeAndConsumesTheOriginalReceipt() {
    UseFollowupRig rig;rig.Admit();rig.Hold({52,86});rig.Ready();
    Require(rig.Poll()==Input::NextChoice,"Ready knife bypassed an exclusive source choice");
    // Keeping knife down cannot reuse the invocation consumed by the higher branch
    rig.held={86};++rig.now;Require(!rig.Poll(),"Choice left the old receipt armed for a delayed knife");
}
void OpposingChoicesFallThroughToTheHeldKnifeBranch() {
    UseFollowupRig rig;rig.Admit();rig.Hold({51,52,86});rig.Ready();
    Require(rig.Poll()==Input::Back,"Opposed choices incorrectly blocked source knife fallthrough");
}
void ReadyKnifeReturnsBackWhenQuickExitIsDisabledOrAtRoot() {
    for(bool root:{false,true})for(bool quick:{false,true}) {
        if(quick&&!root)continue;
        UseFollowupRig rig;rig.quick=quick;rig.root=root;rig.Admit();rig.Hold({86});rig.Ready();
        Require(rig.Poll()==Input::Back,"Ready root or ordinary knife did not return Back");
        Require(!rig.Poll(),"Consumed ready receipt emitted another Back");
    }
}
void ReadyChildKnifeOwnsExactlyOneNinetyMillisecondClose() {
    UseFollowupRig rig;rig.quick=true;rig.Admit(MenuOptionCallbackKind::Boolean);rig.Hold({86});rig.Ready();
    Require(!rig.Poll(),"Ready child knife skipped its source hold loop");
    // Once admitted, the source knife loop remains exclusive even when higher keys appear
    rig.held={86,38,70,52};rig.now=589;Require(!rig.Poll(),"Active source knife loop yielded to another branch or closed early");
    rig.now=590;Require(rig.Poll()==Input::Close,"Ready child knife did not close at90ms");
    Require(!rig.Poll(),"Completed knife loop replayed its ready receipt");
}
void ReadyShortKnifeReleaseReturnsBackWithoutALateClose() {
    UseFollowupRig rig;rig.quick=true;rig.Admit();rig.Hold({86});rig.Ready();Require(!rig.Poll(),"Child knife closed immediately");
    rig.held.clear();rig.now=589;Require(rig.Poll()==Input::Back,"Short ready knife release lost Back");
    rig.now=10000;Require(!rig.Poll(),"Released ready knife became a late Close");
}
void ReadyDeadQuickJumpUsesTheCurrentHeldPredicate() {
    UseFollowupRig rig;rig.surface=MenuSurface::Quick;rig.eligibility.life=PlayerLifeState::Dead;
    rig.Admit();rig.Hold({32});rig.Ready();
    Require(rig.Poll()==Input::JumpBack,"Ready dead Quick Jump still required a new edge");
    Require(!rig.Poll(),"Ready Jump left its original receipt armed");
}
void ReadyDeadJumpAndKnifeShareTheSameChildHold() {
    for(bool release:{false,true}) {
        UseFollowupRig rig;rig.surface=MenuSurface::Quick;rig.eligibility.life=PlayerLifeState::Dead;rig.quick=true;
        rig.Admit();rig.Hold({32,86});rig.Ready();Require(!rig.Poll(),"Dead Jump bypassed its held child knife loop");
        if(release){rig.held={32};rig.now=589;Require(rig.Poll()==Input::Back,"Short Jump knife loop lost ordinary Back");}
        else {rig.now=589;Require(!rig.Poll(),"Jump knife loop closed before90ms");rig.now=590;Require(rig.Poll()==Input::Close,"Jump knife loop lost Close");}
        rig.now=10000;Require(!rig.Poll(),"Jump reused an already consumed causal receipt");
    }
}
void AliveOrMainJumpCannotBorrowTheQuickDeadBranch() {
    for(bool main:{false,true}) {
        UseFollowupRig rig;rig.surface=main?MenuSurface::Main:MenuSurface::Quick;
        rig.eligibility.life=main?PlayerLifeState::Dead:PlayerLifeState::Alive;
        rig.Admit();rig.Hold({32});rig.Ready();Require(!rig.Poll(),"Noneligible Jump borrowed dead Quick Back");
    }
}
void BusyAndPendingDoNotStartALowerBranchOrAnEarlyHoldClock() {
    for(int key:{51,52,86,32}) {
        UseFollowupRig rig;rig.quick=true;rig.surface=MenuSurface::Quick;rig.eligibility.life=PlayerLifeState::Dead;
        rig.Admit();rig.Hold({key});rig.Ready();rig.pending=true;Require(!rig.Poll(),"Pending callback completion admitted a lower branch");
        rig.pending=false;rig.eligibility.blocked=true;rig.now=9000;Require(!rig.Poll(),"Busy callback completion admitted a lower branch");
        // Time spent Busy cannot count toward a hold that has not started
        rig.eligibility.blocked=false;
        if(key==86){Require(!rig.Poll(),"Ready knife borrowed time spent Busy");rig.now=9089;Require(!rig.Poll(),"Busy knife closed early");rig.now=9090;Require(rig.Poll()==Input::Close,"Busy knife did not start a fresh90ms hold");}
        else Require(rig.Poll()==(key==51?Input::PreviousChoice:key==52?Input::NextChoice:Input::JumpBack),"Original scope did not resume after Busy cleared");
    }
}
void ReplacedScopeNeverTransfersTheReadyLowerBranches() {
    for(int key:{51,52,86,32})for(int change:{0,1,2,3,4,5}) {
        UseFollowupRig rig;rig.surface=MenuSurface::Quick;rig.eligibility.life=PlayerLifeState::Dead;
        rig.Admit();rig.Hold({key});
        if(change==0){rig.focused=false;Require(!rig.Poll(),"Focus loss emitted a command");rig.focused=true;}
        else if(change==1)++rig.navigation;
        else if(change==2){++rig.eligibility.revision;rig.continuation->admissionRevision=rig.eligibility.revision;}
        // A syntactically valid replacement still cannot inherit the original ticket
        else if(change==3)rig.surface=MenuSurface::Main;
        else if(change==4)rig.continuation.reset();
        else {rig.continuation->revision=2;rig.callback->revision=2;Require(!rig.Poll(),"Replacement callback emitted early input");}
        rig.Ready();Require(!rig.Poll(),"Changed owner, scope or invocation transferred lower held input");
    }
}
void MissingSuccessfulWriteAndForeignAckCannotResumeLowerBranches() {
    for(int key:{51,52,86,32})for(bool capture:{false,true}) {
        UseFollowupRig rig;rig.surface=MenuSurface::Quick;rig.eligibility.life=PlayerLifeState::Dead;
        rig.Admit(MenuOptionCallbackKind::Plain,capture);rig.Hold({key});rig.Ready();
        if(capture)rig.ack=12;
        // A later command acknowledgment is different from the captured Select write
        Require(!rig.Poll(),"Missing write or foreign ACK resumed a lower held branch");
    }
}
void NativeRollbackRetiresLowerHeldInputBeforeReadiness() {
    for(int key:{51,52,86,32}) {
        UseFollowupRig rig;rig.surface=MenuSurface::Quick;rig.eligibility.life=PlayerLifeState::Dead;
        rig.Admit();rig.Hold({key});rig.now=50;Require(!rig.Poll(),"Rollback emitted held input");rig.Ready();
        Require(!rig.Poll(),"Rollback left lower source input ready");
    }
}
void LegacyOmissionKeepsExistingFreshPressBehavior() {
    for(int key:{51,52,86}) {
        UseFollowupRig rig;rig.Poll();rig.held={key};
        Require(rig.Poll()==(key==51?Input::PreviousChoice:key==52?Input::NextChoice:Input::Back),"Legacy fresh branch stopped working");
        Require(!rig.Poll(),"Legacy omission invented a ready receipt burst");
    }
}
}
int main() {
    struct Test{const char* name;void(*run)();};
    const std::array tests{
        Test{"ReadyPlainAndBooleanResumeEitherExclusiveChoiceOnce",ReadyPlainAndBooleanResumeEitherExclusiveChoiceOnce},
        Test{"OpposingChoicesAreNeutralUntilTheCurrentDirectionBecomesExclusive",OpposingChoicesAreNeutralUntilTheCurrentDirectionBecomesExclusive},
        Test{"ReadySelectStillPrecedesChoiceAndKnife",ReadySelectStillPrecedesChoiceAndKnife},
        Test{"ReadyScrollStillPrecedesChoiceAndKnife",ReadyScrollStillPrecedesChoiceAndKnife},
        Test{"ReadyChoicePrecedesKnifeAndConsumesTheOriginalReceipt",ReadyChoicePrecedesKnifeAndConsumesTheOriginalReceipt},
        Test{"OpposingChoicesFallThroughToTheHeldKnifeBranch",OpposingChoicesFallThroughToTheHeldKnifeBranch},
        Test{"ReadyKnifeReturnsBackWhenQuickExitIsDisabledOrAtRoot",ReadyKnifeReturnsBackWhenQuickExitIsDisabledOrAtRoot},
        Test{"ReadyChildKnifeOwnsExactlyOneNinetyMillisecondClose",ReadyChildKnifeOwnsExactlyOneNinetyMillisecondClose},
        Test{"ReadyShortKnifeReleaseReturnsBackWithoutALateClose",ReadyShortKnifeReleaseReturnsBackWithoutALateClose},
        Test{"ReadyDeadQuickJumpUsesTheCurrentHeldPredicate",ReadyDeadQuickJumpUsesTheCurrentHeldPredicate},
        Test{"ReadyDeadJumpAndKnifeShareTheSameChildHold",ReadyDeadJumpAndKnifeShareTheSameChildHold},
        Test{"AliveOrMainJumpCannotBorrowTheQuickDeadBranch",AliveOrMainJumpCannotBorrowTheQuickDeadBranch},
        Test{"BusyAndPendingDoNotStartALowerBranchOrAnEarlyHoldClock",BusyAndPendingDoNotStartALowerBranchOrAnEarlyHoldClock},
        Test{"ReplacedScopeNeverTransfersTheReadyLowerBranches",ReplacedScopeNeverTransfersTheReadyLowerBranches},
        Test{"MissingSuccessfulWriteAndForeignAckCannotResumeLowerBranches",MissingSuccessfulWriteAndForeignAckCannotResumeLowerBranches},
        Test{"NativeRollbackRetiresLowerHeldInputBeforeReadiness",NativeRollbackRetiresLowerHeldInputBeforeReadiness},
        Test{"LegacyOmissionKeepsExistingFreshPressBehavior",LegacyOmissionKeepsExistingFreshPressBehavior}};
    int failed=0;
    for(const auto& test:tests){try{test.run();std::cout<<"PASS "<<test.name<<'\n';}catch(const std::exception& error){++failed;std::cout<<"FAIL "<<test.name<<": "<<error.what()<<'\n';}}
    std::cout<<tests.size()-failed<<'/'<<tests.size()<<" Use follow-up input checks passed\n";
    return failed?1:0;
}
