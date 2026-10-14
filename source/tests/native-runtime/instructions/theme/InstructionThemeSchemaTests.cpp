#include "InstructionThemeTests.h"
#include "../../TestSupport.h"
namespace iw::overlay::tests::instructions {
namespace {
Json Packet() {
    auto state=StateJson();
    state["localMenuEligibility"]={{"revision",7},{"life","alive"},{"hasMenu",true},{"controlsDisabled",false},{"blocked",false}};
    state["instructionHud"]={{"exists",true},{"creationRevision",2},{"admissionRevision",7},{"creationDesign","AIO"},
        {"creationAlternate",false},{"creationBackground",true},{"creationAccent","#3998FE"},{"creationRainbow",false}};
    state["instructionTheme"]={{"creationRevision",2},{"prefixComplete",true},{"events",Json::array({
        {{"revision",1},{"target","background"},{"accent","#ff0000"},{"elapsedMilliseconds",430},
        {"predecessorGapMilliseconds",nullptr},{"stoppedRainbow",false}},
        {{"revision",2},{"target","background"},{"accent","#00FF00"},{"elapsedMilliseconds",0},
        {"predecessorGapMilliseconds",430},{"stoppedRainbow",false}}})}};
    return state;
}
auto Read(const Json& state) { return runtime::ParseState(state.dump(),GetCurrentProcessId()).snapshot; }
}
void InstructionThemeCopiesExplicitOwnedJournalWithoutChangingAuthority() {
    for(bool open:{false,true}) {
        auto state=Packet();state["isOpen"]=open;auto result=Read(state);
        Check(result->instructionTheme && result->instructionTheme->creationRevision==2
            && result->instructionTheme->events.size()==2,"Owned journal was not copied");
        Check(result->instructionTheme->events[0].accent==InstructionRgb{255,0,0}
            && result->instructionTheme->events[1].predecessorGapMilliseconds==430,"Captured RGB or gap was lost");
        Check(result->accent=="#3998FE" && result->rows[0].value=="OFF" && result->isOpen==open,
            "Instruction callback changed current authority");
        state["instructionTheme"]["events"][0]["accent"]="#000000";
        Check(result->instructionTheme->events[0].accent==InstructionRgb{255,0,0},"Published journal borrowed mutable JSON");
        auto& theme=state["instructionTheme"];theme["events"]=Json::array();
        Check(Read(state)->instructionTheme->events.empty(),"Explicit empty journal lost authority");
        theme["prefixComplete"]=false;Check(!Read(state)->instructionTheme->prefixComplete,"Recovery lost incomplete prefix");
        state["instructionHud"]["creationBackground"]=false;Check(Read(state)->instructionTheme->events.empty(),"No-background footer lost its explicit empty journal");
        state.erase("instructionTheme");Check(!Read(state)->instructionTheme,"Legacy packet gained a theme journal");
    }
    auto state=Packet();const auto max=std::numeric_limits<std::uint64_t>::max();
    state["instructionHud"]["creationRevision"]=max;state["instructionTheme"]["creationRevision"]=max;
    state["instructionTheme"]["events"]=Json::array({{{"revision",max},{"target","outline"},{"accent","#000000"},
        {"elapsedMilliseconds",max},{"predecessorGapMilliseconds",nullptr},{"stoppedRainbow",true}}});
    Check(Read(state)->instructionTheme->events[0].elapsedMilliseconds==max,"Unsigned age maximum was truncated");
    state=Packet();state["instructionTheme"]["events"][0]["elapsedMilliseconds"]=429;
    Check(Read(state)->instructionTheme->events.size()==2,"Valid one-millisecond gap quantization was rejected");
}
void InstructionThemeRejectsMalformedJournalAndCrossCreationClaims() {
    Check(Read(Packet())->instructionHud->exists,"Theme negative test baseline is invalid");
    auto rejects=[](const Json& state){Reject([&]{Read(state);});};
    for(const Json& bad:{Json(nullptr),Json(false),Json::array()}) {auto s=Packet();s["instructionTheme"]=bad;rejects(s);}
    for(const auto* field:{"creationRevision","prefixComplete","events"}) {auto s=Packet();s["instructionTheme"].erase(field);rejects(s);}
    for(const Json& rev:{Json(0),Json(-1),Json(1.0),Json(true),Json("2")}) {auto s=Packet();s["instructionTheme"]["creationRevision"]=rev;rejects(s);}
    auto s=Packet();s["instructionTheme"]["extra"]=1;rejects(s);
    s=Packet();s["instructionTheme"]["creationRevision"]=3;rejects(s);
    s=Packet();s["instructionHud"]=nullptr;rejects(s);
    s=Packet();s["instructionHud"]["creationBackground"]=false;rejects(s);
    for(const auto* field:{"revision","target","accent","elapsedMilliseconds","predecessorGapMilliseconds","stoppedRainbow"}) {
        s=Packet();s["instructionTheme"]["events"][0].erase(field);rejects(s);
    }
    for(const Json& bad:{Json(-1),Json(1.5),Json(false),Json(nullptr)}) {s=Packet();s["instructionTheme"]["events"][0]["elapsedMilliseconds"]=bad;rejects(s);}
    for(const Json& bad:{Json(-1),Json(501),Json(1.0),Json(false)}) {s=Packet();s["instructionTheme"]["events"][1]["predecessorGapMilliseconds"]=bad;rejects(s);}
    for(const Json& bad:{Json("#ABCDEF0"),Json("#ABCG00"),Json(1),Json(nullptr)}) {s=Packet();s["instructionTheme"]["events"][0]["accent"]=bad;rejects(s);}
    s=Packet();s["instructionTheme"]["events"][0]["target"]="title";rejects(s);
    s=Packet();s["instructionTheme"]["events"][1]["revision"]=1;rejects(s);
    s=Packet();s["instructionTheme"]["events"][1]["elapsedMilliseconds"]=431;rejects(s);
    s=Packet();s["instructionTheme"]["events"][1]["predecessorGapMilliseconds"]=400;rejects(s);
    s=Packet();s["instructionTheme"]["events"][1]["predecessorGapMilliseconds"]=nullptr;rejects(s);
    s=Packet();s["instructionTheme"]["events"][0]["predecessorGapMilliseconds"]=500;rejects(s);
    s=Packet();s["instructionTheme"]["events"][0]["extra"]=false;rejects(s);
    s=Packet();s["instructionTheme"]["prefixComplete"]=false;
    s["instructionTheme"]["events"][0]["revision"]=9;s["instructionTheme"]["events"][0]["elapsedMilliseconds"]=1000;
    s["instructionTheme"]["events"][1]["revision"]=12;s["instructionTheme"]["events"][1]["predecessorGapMilliseconds"]=500;rejects(s);
    s=Packet();while(s["instructionTheme"]["events"].size()<9)s["instructionTheme"]["events"].push_back(s["instructionTheme"]["events"][0]);rejects(s);
    auto bytes=Packet().dump();auto at=bytes.find("\"prefixComplete\":");bytes.insert(at,"\"prefix\\u0043omplete\":true,");
    Check(bytes.find("\\u0043")!=std::string::npos,"Decoded duplicate test lost JSON escape");
    Reject([&]{runtime::ParseState(bytes,GetCurrentProcessId());});
    bytes=Packet().dump();at=bytes.find("\"stoppedRainbow\":");bytes.insert(at,"\"stopped\\u0052ainbow\":false,");
    Reject([&]{runtime::ParseState(bytes,GetCurrentProcessId());});
}
}
