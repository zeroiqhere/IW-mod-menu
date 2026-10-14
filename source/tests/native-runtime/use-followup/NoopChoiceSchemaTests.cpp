#include "../TestSupport.h"
#include "../../native-layout/BackendPreview.h"
#include <array>
#include <chrono>
#include <fstream>
#include <iostream>
#include <set>

namespace iw::overlay::tests::noopChoice {
namespace {
Json Packet() {
    auto value=StateJson();value["isOpen"]=true;value["lastSequence"]=10;value["menuSurface"]="main";
    value["localMenuEligibility"]={{"revision",4},{"life","alive"},{"hasMenu",true},{"controlsDisabled",false},{"blocked",false}};
    value["menuMonitorWait"]={{"revision",11},{"kind","choiceNoop"}};
    return value;
}
struct PreviewFile {
    std::filesystem::path path=std::filesystem::temp_directory_path()/
        ("iw-noop-choice-"+std::to_string(GetCurrentProcessId())+"-"+
            std::to_string(std::chrono::steady_clock::now().time_since_epoch().count())+".json");
    explicit PreviewFile(const std::string& bytes){std::ofstream stream(path);stream<<bytes;Check(static_cast<bool>(stream),"No-op fixture could not be written");}
    ~PreviewFile(){std::error_code error;std::filesystem::remove(path,error);}
};
auto Both(const Json& value) {
    const auto result=runtime::ParseState(value.dump(),GetCurrentProcessId());PreviewFile file{value.dump()};
    const auto preview=ReadBackendPreview(file.path);
    Check(result.snapshot->menuMonitorWait==preview.menuMonitorWait,"Readers disagree about the no-op branch");return result;
}
void RejectBoth(const Json& value) {
    Reject([&]{runtime::ParseState(value.dump(),GetCurrentProcessId());});PreviewFile file{value.dump()};
    Reject([&]{ReadBackendPreview(file.path);});
}
void NoopChoiceCopiesExactIdentityWithoutInventingControlsOrAFeatureResult() {
    for(const char* surface:{"main","quick"})for(const char* life:{"alive","dead"}) {
        auto value=Packet();value["menuSurface"]=surface;value["localMenuEligibility"]["life"]=life;
        auto state=Both(value);Check(state.snapshot->menuMonitorWait->revision==11
            &&state.snapshot->menuMonitorWait->kind==MenuMonitorWaitKind::ChoiceNoop,"No-op wait lost its original invocation or kind");
        Check(runtime::InputModeForSnapshot(*state.snapshot)==runtime::InputMode::Menu
            &&state.snapshot->localMenuEligibility->controlsDisabled==false&&state.snapshot->rows[0].value=="OFF",
            "No-op wait fabricated a modal, control lock or changed value");
    }
}
void NoopChoiceRejectsClosedUnknownUnownedAndCompetingVisits() {
    for(int change:{0,1,2,3,4,5,6,7,8}) {
        auto value=Packet();
        if(change==0)value["isOpen"]=false;
        else if(change==1)value["localMenuEligibility"]["life"]="unknown";
        else if(change==2)value["localMenuEligibility"]["hasMenu"]=nullptr;
        else if(change==3)value.erase("localMenuEligibility");
        else if(change==4)value["rows"]=Json::array();
        else if(change==5)value["menuMonitorWait"]["revision"]=0;
        else if(change==6)value["menuMonitorWait"]["kind"]="ChoiceNoop";
        else if(change==7)value["menuMonitorWait"]["duration"]=50;
        else value["menuOptionCallback"]={{"revision",1},{"kind","plain"},{"phase","cooldown"}};
        RejectBoth(value);
    }
}
void NoopKindLeavesOriginalChoiceAndBackPacketsUnchanged() {
    for(const char* kind:{"choice","back"}) {
        auto value=Packet();value["menuMonitorWait"]["kind"]=kind;const auto state=Both(value);
        Check(state.snapshot->menuMonitorWait->kind==(std::string(kind)=="choice"?MenuMonitorWaitKind::Choice:MenuMonitorWaitKind::Back),
            "No-op addition changed a prior wait kind");
    }
}
struct Router {
    runtime::NativeInputRouter router{runtime::Bindings{{2},{86},{2,38},{1,40},{70},{51},{52}}};
    std::set<int> held;
    std::optional<runtime::Input> Poll(const MenuSnapshot& frame,std::uint64_t now,int* reads=nullptr) {
        return router.Poll(true,frame.isOpen,false,[&](int key){if(reads)++*reads;return held.contains(key);},now,
            frame.navigationRevision,false,frame.isRootMenu,runtime::InputModeForSnapshot(frame),120,
            frame.menuRevision,nullptr,0,0,nullptr,frame.localMenuEligibility?&*frame.localMenuEligibility:nullptr,
            frame.menuSurface,frame.menuMonitorWait?&*frame.menuMonitorWait:nullptr);
    }
};
void NoopChoiceConsumesObservedEdgesUntilItsActualPublishedRetirement() {
    const auto waiting=Both(Packet());auto readyValue=Packet();readyValue.erase("menuMonitorWait");const auto ready=Both(readyValue);
    // Packet disappearance, rather than elapsed native time, owns readiness
    Router router;Check(!router.Poll(*waiting.snapshot,0),"No-op phase emitted an unpressed input");router.held={70,86};
    Check(!router.Poll(*waiting.snapshot,49)&&!router.Poll(*waiting.snapshot,10000),
        "Native elapsed time incorrectly granted no-op readiness");
    Check(!router.Poll(*ready.snapshot,10001),"Keys observed during the no-op pause replayed at retirement");
    router.held.clear();router.Poll(*ready.snapshot,10002);router.held={70};
    Check(router.Poll(*ready.snapshot,10003)==runtime::Input::Select,"Fresh source input failed after no-op retirement");
}
void NoopChoiceDirectBoundaryRejectsClosedOrModalFramesBeforeKeys() {
    for(int change:{0,1}) {
        // The direct boundary must remain safe even when serialization is bypassed
        auto frame=*Both(Packet()).snapshot;
        if(change==0)frame.isOpen=false;
        else frame.menuUnlock=MenuUnlockPhase::Pause;
        Router router;int reads=0;bool rejected=false;
        try{router.Poll(frame,0,&reads);}catch(const std::invalid_argument&){rejected=true;}
        Check(rejected&&reads==0,"Invalid direct no-op wait reached the key reader");
    }
}
}
}
int main() {
    using namespace iw::overlay::tests::noopChoice;
    struct Test{const char*name;void(*run)();};
    const std::array tests{
        Test{"NoopChoiceCopiesExactIdentityWithoutInventingControlsOrAFeatureResult",NoopChoiceCopiesExactIdentityWithoutInventingControlsOrAFeatureResult},
        Test{"NoopChoiceRejectsClosedUnknownUnownedAndCompetingVisits",NoopChoiceRejectsClosedUnknownUnownedAndCompetingVisits},
        Test{"NoopKindLeavesOriginalChoiceAndBackPacketsUnchanged",NoopKindLeavesOriginalChoiceAndBackPacketsUnchanged},
        Test{"NoopChoiceConsumesObservedEdgesUntilItsActualPublishedRetirement",NoopChoiceConsumesObservedEdgesUntilItsActualPublishedRetirement},
        Test{"NoopChoiceDirectBoundaryRejectsClosedOrModalFramesBeforeKeys",NoopChoiceDirectBoundaryRejectsClosedOrModalFramesBeforeKeys}};
    int failed=0;
    for(const auto&test:tests){try{test.run();std::cout<<"PASS "<<test.name<<'\n';}catch(const std::exception&error){++failed;std::cout<<"FAIL "<<test.name<<": "<<error.what()<<'\n';}}
    std::cout<<tests.size()-failed<<'/'<<tests.size()<<" no-op choice schema checks passed\n";return failed?1:0;
}
