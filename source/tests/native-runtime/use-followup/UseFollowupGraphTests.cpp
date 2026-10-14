#include "../use-monitor/UseContinuationGraphSupport.h"
#include <array>
#include <iostream>

namespace iw::overlay::tests::useGraph {
void ActualFollowupInventoryMatchesIndependentOraclesAndBothReaders(const std::filesystem::path& directory);
namespace {
void Begin(Router& router,const runtime::ParsedState& before,std::uint64_t sequence) {
    Check(!router.Poll(before,0),"Pre-admission graph emitted an unpressed action");router.held={70};
    Check(router.Poll(before,0)==runtime::Input::Select,"Actual follow-up predecessor did not select");
    router.input.CaptureUseDispatch(sequence);
}
void ActualHeldChoicesResumeAfterUseAndStartTheirOwnPause(const std::filesystem::path& directory) {
    for(const char* prefix:{"plain-next","plain-previous"}) {
        const auto path=[&](const char* suffix){return directory/(std::string(prefix)+suffix+".json");};
        Router router;Begin(router,Read(path("-before0")),15);
        Check(!router.Poll(Read(path("-use0")),0),"Choice escaped the admitted Select");
        const bool next=std::string(prefix)=="plain-next";router.held={next?52:51};
        Check(!router.Poll(Read(path("-use249")),249),"Held choice escaped the plain callback");
        Check(router.Poll(Read(path("-ready250")),250)==(next?runtime::Input::NextChoice:runtime::Input::PreviousChoice),
            "Actual normal callback retirement did not resume held choice");
        const auto started=Read(path("-choice250"));
        Check(!started.snapshot->menuUseContinuation&&started.snapshot->menuMonitorWait,
            "The new choice retained old Select permission or lost its own wait");
        Check(started.snapshot->rows[started.snapshot->selectedIndex].value==(next?"Text":"Text Color"),
            "Actual choice changed the wrong UI draft");
        Check(!router.Poll(started,250)&&!router.Poll(Read(path("-choice429")),429),"New choice borrowed earlier callback time");
        Check(router.Poll(Read(path("-choice430")),430)==(next?runtime::Input::NextChoice:runtime::Input::PreviousChoice),
            "Actual choice lost its existing held repeat at180ms");
        if(next) {
            Check(!router.Poll(Read(path("-repeat430")),430),"Repeated choice caused a same-frame burst");
            router.held.clear();Check(!router.Poll(Read(path("-repeat610")),610),"Released choice replayed at expiry");
        }
    }
}
void ActualPlainReadyKnifeReturnsToParentWithAnIndependentWait(const std::filesystem::path& directory) {
    Router router;Begin(router,Read(directory/"plain-back-before0.json"),15);
    Check(!router.Poll(Read(directory/"plain-back-use0.json"),0),"Plain callback emitted early Back");router.held={86};
    Check(!router.Poll(Read(directory/"plain-back-use249.json"),249),"Held knife escaped the plain callback");
    Check(router.Poll(Read(directory/"plain-back-ready250.json"),250)==runtime::Input::Back,"Actual plain callback lost ready knife");
    const auto child=Read(directory/"plain-back-child250.json");
    Check(!child.snapshot->menuUseContinuation&&child.snapshot->menuMonitorWait&&child.snapshot->isOpen,
        "Parent Back retained old receipt or lost its own250 pause");
    Check(!router.Poll(child,250)&&!router.Poll(Read(directory/"plain-back-child499.json"),499),"Parent Back pause replayed knife");
    // Later parent/root packets are real explicit controller commands, not proof of held mechanical continuation
    const auto main=Read(directory/"plain-back-main500.json"),root=Read(directory/"plain-back-root750.json"),ready=Read(directory/"plain-back-root1000.json");
    Check(main.snapshot->menuMonitorWait->revision==2&&root.snapshot->menuMonitorWait->revision==3
        &&!root.snapshot->isOpen&&!ready.snapshot->menuMonitorWait,"Explicit subsequent Back invocations lost their own waits");
}
void ActualReadyChildKnifeClosesOnlyAfterItsNinetyMillisecondLoop(const std::filesystem::path& directory) {
    Router router;const auto before=Read(directory/"quick-exit-child-before0.json");
    Check(before.snapshot->appearance.quickExit&&!before.snapshot->isRootMenu,"Actual child graph lost its selected Quick Exit setting");
    Begin(router,before,28);Check(!router.Poll(Read(directory/"quick-exit-child-use0.json"),0),"Quick child callback escaped its wait");
    router.held={86};Check(!router.Poll(Read(directory/"quick-exit-child-use249.json"),249),"Knife escaped callback249");
    Check(!router.Poll(Read(directory/"quick-exit-child-ready250.json"),250),"Ready child knife skipped the source hold loop");
    Check(!router.Poll(Read(directory/"quick-exit-child-ready339.json"),339),"Child knife closed at89ms");
    Check(router.Poll(Read(directory/"quick-exit-child-ready340.json"),340)==runtime::Input::Close,"Actual ready knife did not close at90ms");
    const auto closed=Read(directory/"quick-exit-child-close340.json");
    Check(!closed.snapshot->isOpen&&!closed.snapshot->menuUseContinuation&&closed.snapshot->menuMonitorWait,
        "Close failed to retire Select or retain its final Back pause");
    Check(!router.Poll(closed,340)&&!router.Poll(Read(directory/"quick-exit-child-close589.json"),589)
        &&!router.Poll(Read(directory/"quick-exit-child-close590.json"),590),"Closed child replayed the old hold");
}
void ActualRootKnifeKeepsFreshBackEvenWhenQuickExitIsEnabled(const std::filesystem::path& directory) {
    const auto before=Read(directory/"quick-exit-root-before0.json");
    Check(before.snapshot->appearance.quickExit&&before.snapshot->isRootMenu&&!before.snapshot->menuUseContinuation,
        "Producer root graph was mistaken for a causal callback predecessor");
    // This graph begins at an ordinary root, so only its fresh legacy route is joined
    Router router;Check(!router.Poll(before,0),"Root graph emitted an unpressed command");router.held={86};
    Check(router.Poll(before,0)==runtime::Input::Back,"Enabled Quick Exit changed root knife into a hold");
    const auto closed=Read(directory/"quick-exit-root-back0.json");
    Check(!closed.snapshot->isOpen&&closed.snapshot->menuMonitorWait->kind==MenuMonitorWaitKind::Back,
        "Root Back lost its closed source pause");
    Check(!Read(directory/"quick-exit-root-back250.json").snapshot->menuMonitorWait,"Root Back failed to retire at250ms");
}
void ActualBooleanReadyKnifeWaitsForRedrawAndFullCallbackRetirement(const std::filesystem::path& directory) {
    Router router;Begin(router,Read(directory/"boolean-back-before0.json"),13);
    Check(!router.Poll(Read(directory/"boolean-back-use0.json"),0),"Boolean callback returned Back immediately");router.held={86};
    const std::array frames{std::pair{"boolean-back-use179.json",179u},std::pair{"boolean-back-use180.json",180u},std::pair{"boolean-back-use429.json",429u}};
    for(const auto&[name,now]:frames)Check(!router.Poll(Read(directory/name),now),"Boolean redraw became ready knife input");
    Check(router.Poll(Read(directory/"boolean-back-ready430.json"),430)==runtime::Input::Back,"Actual Boolean retirement lost ready knife");
    const auto back=Read(directory/"boolean-back-back430.json");
    Check(!back.snapshot->menuUseContinuation&&back.snapshot->menuMonitorWait->kind==MenuMonitorWaitKind::Back,
        "Boolean Back reused its original430ms callback");
    Check(!router.Poll(back,430)&&!router.Poll(Read(directory/"boolean-back-back679.json"),679),"Boolean parent pause ended early");
    Check(!Read(directory/"boolean-back-back680.json").snapshot->menuMonitorWait,"Boolean parent wait did not finish at250ms");
}
}
}
int main(int argc,char**argv) {
    if(argc!=2)return 2;
    using namespace iw::overlay::tests::useGraph;
    const std::array tests{
        std::pair{"ActualFollowupInventoryMatchesIndependentOraclesAndBothReaders",ActualFollowupInventoryMatchesIndependentOraclesAndBothReaders},
        std::pair{"ActualHeldChoicesResumeAfterUseAndStartTheirOwnPause",ActualHeldChoicesResumeAfterUseAndStartTheirOwnPause},
        std::pair{"ActualPlainReadyKnifeReturnsToParentWithAnIndependentWait",ActualPlainReadyKnifeReturnsToParentWithAnIndependentWait},
        std::pair{"ActualReadyChildKnifeClosesOnlyAfterItsNinetyMillisecondLoop",ActualReadyChildKnifeClosesOnlyAfterItsNinetyMillisecondLoop},
        std::pair{"ActualRootKnifeKeepsFreshBackEvenWhenQuickExitIsEnabled",ActualRootKnifeKeepsFreshBackEvenWhenQuickExitIsEnabled},
        std::pair{"ActualBooleanReadyKnifeWaitsForRedrawAndFullCallbackRetirement",ActualBooleanReadyKnifeWaitsForRedrawAndFullCallbackRetirement}};
    int failed=0;
    for(const auto&[name,run]:tests){try{run(argv[1]);std::cout<<"PASS "<<name<<'\n';}catch(const std::exception&error){++failed;std::cout<<"FAIL "<<name<<": "<<error.what()<<'\n';}}
    std::cout<<tests.size()-failed<<'/'<<tests.size()<<" actual Use follow-up graph checks passed\n";return failed?1:0;
}
