#include "InstructionThemeTests.h"
#include "../../TestSupport.h"
#include "../../../native-layout/BackendPreview.h"
#include <fstream>
#include <random>
namespace iw::overlay::tests::instructions {
void InstructionThemePreviewUsesTheSameStrictOwnedJournal() {
    std::filesystem::path directory;
    std::random_device random;
    for(unsigned attempt=0;attempt<32;++attempt) {
        auto candidate=std::filesystem::temp_directory_path()/("iw-theme-preview-"+std::to_string(random()));
        if(std::filesystem::create_directory(candidate)){directory=candidate;break;}
    }
    Check(!directory.empty(),"Could not reserve theme preview directory");
    struct Cleanup {std::filesystem::path path;~Cleanup(){std::error_code e;std::filesystem::remove_all(path,e);}} cleanup{directory};
    std::filesystem::permissions(directory,std::filesystem::perms::owner_all);
    auto read=[&](const Json& state){auto path=directory/"packet.json";{std::ofstream stream(path);stream<<state.dump();Check(bool(stream),"Could not write preview packet");}return ReadBackendPreview(path);};
    auto state=StateJson();state["localMenuEligibility"]={{"revision",7},{"life","alive"},{"hasMenu",true},{"controlsDisabled",false},{"blocked",false}};
    state["instructionHud"]={{"exists",true},{"creationRevision",2},{"admissionRevision",7},{"creationDesign","AIO"},
        {"creationAlternate",false},{"creationBackground",true},{"creationAccent","#3998FE"},{"creationRainbow",false}};
    state["instructionTheme"]={{"creationRevision",2},{"prefixComplete",true},{"events",Json::array()}};
    auto parsed=runtime::ParseState(state.dump(),GetCurrentProcessId()).snapshot;
    auto preview=read(state);Check(preview.instructionTheme && parsed->instructionTheme
        && preview.instructionTheme->creationRevision==parsed->instructionTheme->creationRevision,"Preview did not copy explicit journal");
    for(const Json& bad:{Json(nullptr),Json(false),Json::array()}) {auto s=state;s["instructionTheme"]=bad;Reject([&]{read(s);});}
    auto bad=state;bad["instructionTheme"]["creationRevision"]=3;Reject([&]{read(bad);});
    bad=state;bad["instructionTheme"]["events"]=Json::array({{{"revision",1},{"target","outline"},{"accent","#ffffff"},
        {"elapsedMilliseconds",std::numeric_limits<std::uint64_t>::max()},{"predecessorGapMilliseconds",nullptr},{"stoppedRainbow",true}}});
    Check(read(bad).instructionTheme->events[0].accent==InstructionRgb{255,255,255},"Preview lost maximum unsigned age or RGB");
    bad["instructionTheme"]["events"][0]["elapsedMilliseconds"]=1.0;Reject([&]{read(bad);});
    state.erase("instructionTheme");Check(!read(state).instructionTheme,"Legacy preview gained authority");
}
}
