#include "../../native/render/instructions/color/InstructionThemeTimeline.h"
#include <functional>
#include <iostream>
#include <limits>
#include <stdexcept>
#include <vector>
using namespace iw::overlay;
namespace {
void Require(bool value,const char* message){if(!value)throw std::runtime_error(message);}
InstructionThemeEvent Event(std::uint64_t id,InstructionThemeTarget target,InstructionRgb rgb,std::uint64_t age,
    std::optional<unsigned> gap=std::nullopt,bool stopped=false){return {id,target,rgb,age,gap,stopped};}
InstructionHudColors Seed(){return {{0,0,255,255},{0,0,0,217}};}
const InstructionHudColors& Have(const std::optional<InstructionHudColors>& value){Require(value.has_value(),"Owned timeline did not return colors");return *value;}
void Color(layout::Color value,unsigned r,unsigned g,unsigned b,unsigned a=255){
    Require(value.red==r&&value.green==g&&value.blue==b&&value.alpha==a,"Independent RGB/time oracle failed");
}
}
int main(){
    const auto outline=InstructionThemeTarget::Outline,background=InstructionThemeTarget::Background;
    const std::vector<std::pair<const char*,std::function<void()>>> checks{
    {"Linear fixed callback keeps captured target and original alpha",[&]{
        InstructionThemeTimeline timeline;InstructionThemeState state{2,true,{Event(1,background,{255,0,0},0)}};
        auto colors=timeline.Update(state,Seed(),std::nullopt,1000);Require(colors.has_value(),"Owned event was not admitted");
        Color(Have(colors).background,0,0,0,217);Color(Have(colors).outline,0,0,255);
        state.events[0].elapsedMilliseconds=250;colors=timeline.Update(state,Seed(),std::nullopt,1250);
        Require(colors.has_value(),"Advancing event was rejected");Color(Have(colors).background,128,0,0,217);
        state.events[0].elapsedMilliseconds=500;colors=timeline.Update(state,Seed(),std::nullopt,1500);
        Color(Have(colors).background,255,0,0,217);Color(Have(colors).outline,0,0,255);
    }},
    {"Interrupted430ms fades preserve floating RGB until final draw conversion",[&]{
        InstructionThemeTimeline timeline;InstructionThemeState state{2,true,{Event(1,outline,{255,0,0},0)}};
        Require(timeline.Update(state,Seed(),std::nullopt,1000).has_value(),"Initial segment missing");
        state.events={Event(1,outline,{255,0,0},430),Event(2,outline,{0,0,255},0,430)};
        auto colors=timeline.Update(state,Seed(),std::nullopt,1430);Color(Have(colors).outline,219,0,36);
        state.events[0].elapsedMilliseconds=500;state.events[1].elapsedMilliseconds=70;
        colors=timeline.Update(state,Seed(),std::nullopt,1500);Color(Have(colors).outline,189,0,66);
        // A complete first receipt reconstructs the same two fixed invocations from creation RGB
        InstructionThemeTimeline late;colors=late.Update(state,Seed(),std::nullopt,9000);Color(Have(colors).outline,189,0,66);
    }},
    {"Truncated or unseen Rainbow starts hold prior colors until a settled endpoint",[&]{
        InstructionThemeTimeline timeline;InstructionThemeState state{2,false,{Event(9,outline,{255,0,0},250,430)}};
        auto colors=timeline.Update(state,Seed(),std::nullopt,1000);Require(colors.has_value(),"Unknown channel lost footer ownership");
        Color(Have(colors).outline,0,0,255);state.events[0].elapsedMilliseconds=500;
        colors=timeline.Update(state,Seed(),std::nullopt,1250);Color(Have(colors).outline,255,0,0);
        InstructionThemeTimeline unseen;state={3,true,{Event(10,background,{0,255,0},250,std::nullopt,true)}};
        colors=unseen.Update(state,Seed(),std::nullopt,1000);Color(Have(colors).background,0,0,0,217);
        state.events[0].elapsedMilliseconds=500;colors=unseen.Update(state,Seed(),std::nullopt,1250);Color(Have(colors).background,0,255,0,217);
        state={4,false,{}};colors=unseen.Update(state,Seed(),std::nullopt,1500);Color(Have(colors).outline,0,0,255);
        InstructionThemeTimeline overlap;state={5,true,{Event(11,outline,{255,0,0},500,std::nullopt,true),
            Event(12,outline,{0,255,0},70,430)}};
        colors=overlap.Update(state,Seed(),std::nullopt,1600);Color(Have(colors).outline,0,0,255);
        state.events[0].elapsedMilliseconds=930;state.events[1].elapsedMilliseconds=500;
        colors=overlap.Update(state,Seed(),std::nullopt,2030);Color(Have(colors).outline,0,255,0);
        InstructionThemeTimeline lost;state={6,true,{Event(1,outline,{255,0,0},0)}};
        lost.Update(state,Seed(),std::nullopt,1000);colors=lost.Update(state,Seed(),std::nullopt,1100);Color(Have(colors).outline,51,0,204);
        state.prefixComplete=false;state.events.clear();
        colors=lost.Update(state,Seed(),std::nullopt,1101);Color(Have(colors).outline,51,0,204);
        InstructionThemeTimeline bridge;state={7,true,{Event(1,outline,{255,0,0},0)}};
        bridge.Update(state,Seed(),std::nullopt,1000);colors=bridge.Update(state,Seed(),std::nullopt,1100);
        state={7,false,{Event(1,outline,{255,0,0},750),Event(3,outline,{0,255,0},250,500)}};
        Require(!bridge.Update(state,Seed(),std::nullopt,1750),"Forged internal journal holes were admitted");
        InstructionThemeTimeline retained;
        for(std::uint64_t invocation=1;invocation<=10;++invocation) {
            state={8,invocation<=8,{}};const auto first=invocation>8?invocation-7:1;
            for(auto id=first;id<=invocation;++id) state.events.push_back(Event(id,outline,
                id%2?InstructionRgb{255,0,0}:InstructionRgb{0,0,255},(invocation-id)*430,
                id==1?std::nullopt:std::optional<unsigned>{430}));
            colors=retained.Update(state,Seed(),std::nullopt,1000+(invocation-1)*430);
            Require(colors.has_value(),"Retained owner lost continuity at journal pruning");
        }
        Color(Have(colors).outline,224,0,31);
        InstructionThemeTimeline missed;InstructionThemeState empty{9,true,{}};
        missed.Update(empty,Seed(),std::nullopt,1000);state.creationRevision=9;
        colors=missed.Update(state,Seed(),std::nullopt,5000);Color(Have(colors).outline,0,0,255);
    }},
    {"Journal mutations rollback and parent regression quarantine until newer creation",[&]{
        InstructionThemeTimeline timeline;InstructionThemeState state{2,true,{Event(1,outline,{255,0,0},250)}};
        Require(timeline.Update(state,Seed(),std::nullopt,1000).has_value(),"Initial owned journal missing");
        auto bad=state;bad.events[0].accent={0,255,0};Require(!timeline.Update(bad,Seed(),std::nullopt,1100),"Immutable event changed RGB");
        Require(!timeline.Update(state,Seed(),std::nullopt,1200),"Quarantine recovered without creation");
        state.creationRevision=3;Require(timeline.Update(state,Seed(),std::nullopt,1300).has_value(),"New creation could not recover");
        bad=state;bad.events[0].elapsedMilliseconds=249;Require(!timeline.Update(bad,Seed(),std::nullopt,1400),"Event age regressed");
        state.creationRevision=4;Require(timeline.Update(state,Seed(),std::nullopt,1500).has_value(),"New creation was blocked");
        Require(!timeline.Update(state,Seed(),std::nullopt,1499),"Native clock regression restarted fade");
        state.creationRevision=5;state.events[0].elapsedMilliseconds=std::numeric_limits<std::uint64_t>::max();
        auto colors=timeline.Update(state,Seed(),std::nullopt,1600);Require(colors.has_value(),"Maximum age overflowed");Color(Have(colors).outline,255,0,0);
    }},
    {"Native report lead is bounded and repeated reports do not restart fades",[&]{
        InstructionThemeTimeline timeline;InstructionThemeState state{2,true,{Event(1,outline,{255,0,0},0)}};
        timeline.Update(state,Seed(),std::nullopt,1000);
        auto colors=timeline.Update(state,Seed(),std::nullopt,1050);Color(Have(colors).outline,26,0,230);
        colors=timeline.Update(state,Seed(),std::nullopt,1400);Color(Have(colors).outline,51,0,204);
        state.events[0].elapsedMilliseconds=250;colors=timeline.Update(state,Seed(),std::nullopt,1450);Color(Have(colors).outline,128,0,128);
        InstructionThemeTimeline slow;state={3,true,{Event(3,outline,{255,0,0},0)}};
        slow.Update(state,Seed(),std::nullopt,1000);colors=slow.Update(state,Seed(),std::nullopt,1100);Color(Have(colors).outline,51,0,204);
        state.events[0].elapsedMilliseconds=1;colors=slow.Update(state,Seed(),std::nullopt,1101);Color(Have(colors).outline,51,0,204);
    }},
    {"Independent channels quiet gap and same-color invocations retain their own identity",[&]{
        InstructionThemeTimeline timeline;InstructionThemeState state{2,true,{Event(1,outline,{255,0,0},1000),
            Event(2,background,{0,255,0},570),Event(3,outline,{0,0,255},0,500)}};
        auto colors=timeline.Update(state,Seed(),std::nullopt,1000);Require(colors.has_value(),"Interchannel journal missing");
        Color(Have(colors).outline,255,0,0);Color(Have(colors).background,0,255,0,217);
        state.events[0].elapsedMilliseconds+=250;state.events[1].elapsedMilliseconds+=250;state.events[2].elapsedMilliseconds=250;
        colors=timeline.Update(state,Seed(),std::nullopt,1250);Color(Have(colors).outline,128,0,128);
        timeline.Override(background,{12,34,56,255});colors=timeline.Update(state,Seed(),std::nullopt,1250);
        Color(Have(colors).background,12,34,56,217);
    }} };
    unsigned passed=0;for(const auto& [name,run]:checks){try{run();++passed;std::cout<<"PASS "<<name<<'\n';}catch(const std::exception& e){std::cerr<<"FAIL "<<name<<": "<<e.what()<<'\n';}}
    std::cout<<passed<<" passed, "<<checks.size()-passed<<" failed\n";return passed==checks.size()?0:1;
}
