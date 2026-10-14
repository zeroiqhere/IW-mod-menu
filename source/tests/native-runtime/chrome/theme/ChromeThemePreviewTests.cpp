#include "ChromeThemeTests.h"
#include "../../TestSupport.h"
#include "../../../native-layout/BackendPreview.h"
#include <fstream>
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
Json Absent() {return {{"exists",false},{"creationRevision",nullptr},{"admissionRevision",nullptr},{"creationDesign",nullptr},
    {"creationAccent",nullptr},{"creationRainbow",nullptr},{"currentRoleMask",0},{"prefixComplete",true},{"events",Json::array()}};}
}
void ChromeThemePreviewMatchesProductionOwnership() {
    const auto dir=std::filesystem::temp_directory_path()/"iw-chrome-theme-schema";std::filesystem::create_directories(dir);
    const auto path=dir/"state.json";
    auto preview=[&](const Json& s){std::ofstream out(path);out<<s.dump();out.close();return ReadBackendPreview(path);};
    auto s=Packet();auto model=preview(s);Check(model.chromeTheme && model.chromeTheme->events.size()==2,"Preview lost owned chrome journal");
    s["chromeTheme"]=Absent();Check(!preview(s).chromeTheme->exists,"Preview lost explicit absence");s.erase("chromeTheme");Check(!preview(s).chromeTheme,"Preview invented legacy owner");
    for(const Json& bad:{Json(nullptr),Json(0),Json(false)}) {s=Packet();s["chromeTheme"]=bad;Reject([&]{preview(s);});}
    s=Packet();s["chromeTheme"]["currentRoleMask"]=0;Reject([&]{preview(s);});
    s=Packet();s["chromeTheme"]["creationDesign"]="Basic";s["chromeTheme"]["currentRoleMask"]=7;
    for(auto& event:s["chromeTheme"]["events"]) { event["targetMask"]=4; }
    Reject([&]{preview(s);});
    s=Packet();s["chromeTheme"]["events"][0]["accent"]="#ZZ0000";Reject([&]{preview(s);});
    std::filesystem::remove_all(dir);
}
}
