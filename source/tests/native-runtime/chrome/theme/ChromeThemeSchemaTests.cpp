#include "ChromeThemeTests.h"
#include "../../TestSupport.h"
#include "../../../native-layout/BackendPreview.h"
#include <fstream>
#include <limits>
namespace iw::overlay::tests::chrome {
namespace {
Json Packet() {
    auto s=StateJson();s["isOpen"]=true;s["menuChromeRevision"]=2;
    s["localMenuEligibility"]={{"revision",7},{"life","alive"},{"hasMenu",true},{"controlsDisabled",false},{"blocked",false}};
    s["chromeTheme"]={{"exists",true},{"creationRevision",2},{"admissionRevision",7},{"creationDesign","Apparition"},
        {"creationAccent","#0000ff"},{"creationRainbow",false},{"currentRoleMask",31},{"prefixComplete",true},
        {"events",Json::array({{{"revision",1},{"targetMask",28},{"accent","#FF0000"},{"elapsedMilliseconds",430},
        {"predecessorGapMilliseconds",nullptr},{"stoppedRainbow",false}},
        {{"revision",2},{"targetMask",28},{"accent","#0000FF"},{"elapsedMilliseconds",0},
        {"predecessorGapMilliseconds",430},{"stoppedRainbow",false}}})}};
    return s;
}
auto Read(const Json& s) {return runtime::ParseState(s.dump(),GetCurrentProcessId()).snapshot;}
Json Absent() {return {{"exists",false},{"creationRevision",nullptr},{"admissionRevision",nullptr},{"creationDesign",nullptr},
    {"creationAccent",nullptr},{"creationRainbow",nullptr},{"currentRoleMask",0},{"prefixComplete",true},{"events",Json::array()}};}
}
void ChromeThemeOwnsSourceCreationAndHistoricalMasks() {
    auto s=Packet();auto result=Read(s);Check(result->chromeTheme && result->chromeTheme->exists,"Owned chrome was not copied");
    Check(result->chromeTheme->creationAccent==ChromeRgb{0,0,255} && result->chromeTheme->events.size()==2,"Captured RGB/receipts were not owned");
    Check(result->accent=="#3998FE" && result->rows[0].value=="OFF","Chrome metadata changed current authority");
    s["chromeTheme"]["currentRoleMask"]=27;Check(Read(s)->chromeTheme->events[0].targetMask==28,"Historical title receipt was rejected after width destruction");
    for(bool open:{false,true}) {s=Packet();s["isOpen"]=open;s["chromeTheme"]=Absent();Check(!Read(s)->chromeTheme->exists,"Explicit source absence was rejected");}
    s=Packet();s.erase("chromeTheme");Check(!Read(s)->chromeTheme,"Legacy gained an owned color journal");
    s=Packet();s["menuUnlock"]="rebuild";s["isRootMenu"]=false;
    // Existing chrome can survive the source unlock monitor returning to rebuild after clock rollback
    Check(Read(s)->chromeTheme->exists,"Retained unlock creation was rejected");
    const auto max=std::numeric_limits<std::uint64_t>::max();s=Packet();s["menuChromeRevision"]=max;
    s["chromeTheme"]["creationRevision"]=max;s["localMenuEligibility"]["revision"]=max;s["chromeTheme"]["admissionRevision"]=max;
    s["chromeTheme"]["events"]=Json::array({{{"revision",max},{"targetMask",28},{"accent","#abcdef"},
        {"elapsedMilliseconds",max},{"predecessorGapMilliseconds",nullptr},{"stoppedRainbow",true}}});
    Check(Read(s)->chromeTheme->events[0].elapsedMilliseconds==max,"Unsigned age maximum was truncated");
}
void ChromeThemeRejectsMalformedFieldsAndUnprovedScope() {
    Check(Read(Packet())->isOpen,"Negative baseline packet is invalid");
    auto reject=[](Json s){Reject([&]{Read(s);});};
    for(const Json& bad:{Json(nullptr),Json(false),Json::array()}) {auto s=Packet();s["chromeTheme"]=bad;reject(s);}
    for(const auto* field:{"exists","creationRevision","admissionRevision","creationDesign","creationAccent","creationRainbow","currentRoleMask","prefixComplete","events"}) {
        auto s=Packet();s["chromeTheme"].erase(field);reject(s);
    }
    for(const Json& bad:{Json(0),Json(-1),Json(1.0),Json(true),Json("2")}) {auto s=Packet();s["chromeTheme"]["creationRevision"]=bad;reject(s);}
    for(const auto* field:{"creationRainbow","prefixComplete"}) {auto s=Packet();s["chromeTheme"][field]=nullptr;reject(s);}
    for(const Json& bad:{Json(32),Json(-1),Json(1.0),Json(false),Json(0),Json(30)}) {auto s=Packet();s["chromeTheme"]["currentRoleMask"]=bad;reject(s);}
    for(const Json& bad:{Json("#ABCDE"),Json("#ABCG00"),Json(1),Json(nullptr)}) {auto s=Packet();s["chromeTheme"]["creationAccent"]=bad;reject(s);}
    auto s=Packet();s["chromeTheme"]["extra"]=1;reject(s);s=Packet();s["isOpen"]=false;reject(s);
    s=Packet();s["chromeTheme"]["creationDesign"]="Basic";s["chromeTheme"]["currentRoleMask"]=7;
    for(auto& event:s["chromeTheme"]["events"]) { event["targetMask"]=4; }
    reject(s);
    s=Packet();s["chromeTheme"]["admissionRevision"]=8;reject(s);s=Packet();s["chromeTheme"]["creationRevision"]=3;reject(s);
    s=Packet();s["localMenuEligibility"]["life"]="unknown";reject(s);s=Packet();s["localMenuEligibility"]["hasMenu"]=false;reject(s);
    for(const auto* field:{"revision","targetMask","accent","elapsedMilliseconds","predecessorGapMilliseconds","stoppedRainbow"}) {s=Packet();s["chromeTheme"]["events"][0].erase(field);reject(s);}
    for(const Json& bad:{Json(-1),Json(1.0),Json(false),Json(nullptr)}) {s=Packet();s["chromeTheme"]["events"][0]["elapsedMilliseconds"]=bad;reject(s);}
    s=Packet();s["chromeTheme"]["events"][0]["targetMask"]=4;reject(s);
    for(bool prefix:{false,true}) {s=Packet();s["chromeTheme"]["prefixComplete"]=prefix;s["chromeTheme"]["events"][1]["revision"]=3;reject(s);}
    s=Packet();s["chromeTheme"]["events"][1]["predecessorGapMilliseconds"]=400;reject(s);
    s=Packet();while(s["chromeTheme"]["events"].size()<9)s["chromeTheme"]["events"].push_back(s["chromeTheme"]["events"][0]);reject(s);
    s=Packet();s["chromeTheme"]=Absent();s["chromeTheme"]["creationAccent"]="#000000";reject(s);
    auto bytes=Packet().dump();bytes.insert(bytes.find("\"currentRoleMask\":"),"\"currentRole\\u004dask\":31,");
    Check(bytes.find("\\u004d")!=std::string::npos,"Decoded duplicate lost JSON escape");Reject([&]{runtime::ParseState(bytes,GetCurrentProcessId());});
}
}
