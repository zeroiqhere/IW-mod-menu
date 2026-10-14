#include "InstructionThemeFixtureTests.h"
#include "../../../../native-layout/BackendPreview.h"
#include "../../../../../native/runtime/ControlLabels.h"
#include <fstream>
#include <memory>
#include <set>
#include <stdexcept>
#include <nlohmann/json.hpp>
namespace iw::overlay::tests::instruction_theme {
namespace {
void Require(bool value,const char* message){if(!value)throw std::runtime_error(message);}
bool Equal(layout::Color a,layout::Color b){return a.red==b.red&&a.green==b.green&&a.blue==b.blue&&a.alpha==b.alpha;}
}
void CheckInventory(const std::filesystem::path& directory) {
    std::set<std::string> names;for(const auto& p:Profiles)names.insert("instruction-theme-"+std::string(p.name)+".json");
    unsigned files=0;for(const auto& entry:std::filesystem::directory_iterator(directory)) {
        Require(!entry.is_symlink()&&entry.is_regular_file(),"Theme graph contains a non-regular packet");
        Require(names.contains(entry.path().filename().string()),"Theme graph contains an unexpected packet");++files;
    }
    Require(files==Profiles.size(),"Actual theme graph is incomplete");
}
MenuSnapshot ReadAndCheck(const std::filesystem::path& directory,const Expected& expected) {
    const auto path=directory/("instruction-theme-"+std::string(expected.name)+".json");
    const auto size=std::filesystem::file_size(path);Require(size>0&&size<=32768,"Theme packet exceeds its bound");
    std::ifstream stream(path,std::ios::binary);std::string bytes(static_cast<std::size_t>(size),'\0');
    stream.read(bytes.data(),static_cast<std::streamsize>(size));Require(static_cast<std::size_t>(stream.gcount())==size,"Theme packet read failed");
    const runtime::Bindings bindings{{2},{86,6},{2,38},{1,40},{70},{51},{52},{1},{38},{40},{71},{69},{16},{17},{32}};
    const auto labels=runtime::DescribeOpeningActions(bindings);
    const auto parsed=runtime::ParseState(bytes,42,nullptr,nullptr,nullptr,nullptr,nullptr,std::nullopt,nullptr,nullptr,&labels);
    const auto& state=*parsed.snapshot;const auto preview=ReadBackendPreview(path,&labels);const auto wire=nlohmann::json::parse(bytes);
    Require(state.navigationRevision==expected.navigation&&state.menuRevision==expected.menu&&parsed.lastSequence==expected.sequence,
        "Theme journal altered confirmed navigation or action epochs");
    Require(state.instructionHud&&state.instructionHud->exists==(expected.creation!=0),"Actual theme ownership changed");
    if(expected.creation) {
        Require(state.instructionTheme&&state.instructionTheme->creationRevision==expected.creation
            &&state.instructionHud->creationRevision==expected.creation&&state.instructionHud->admissionRevision==expected.admission
            &&state.instructionTheme->events.size()==expected.events,"Actual theme lost its immutable parent or event inventory");
        Require(preview.instructionTheme&&preview.instructionTheme->creationRevision==expected.creation
            &&preview.instructionTheme->prefixComplete==state.instructionTheme->prefixComplete
            &&preview.instructionTheme->events.size()==expected.events,"Theme readers disagree on explicit authority");
        for(unsigned i=0;i<expected.events;++i) {
            const auto& a=state.instructionTheme->events[i];const auto& b=preview.instructionTheme->events[i];
            Require(a.revision==b.revision&&a.target==b.target&&a.accent==b.accent&&a.elapsedMilliseconds==b.elapsedMilliseconds
                &&a.predecessorGapMilliseconds==b.predecessorGapMilliseconds&&a.stoppedRainbow==b.stoppedRainbow,
                "Theme readers disagree on captured invocation facts");
            Require(wire.at("instructionTheme").at("events").at(i).size()==6,"Theme event exposed additional private data");
        }
        Require(state.instructionTheme->prefixComplete==!expected.name.starts_with("overlap-nine"),"Pruned prefix was misrepresented as complete");
    } else Require(!state.instructionTheme&&!preview.instructionTheme,"Withdrawn owner retained invocation authority");
    Require(state.accent==wire.at("accent").get<std::string>()&&state.smoothRainbowTheme==wire.at("smoothRainbowTheme").get<bool>()
        &&preview.accent==state.accent&&preview.localMenuEligibility==state.localMenuEligibility,"Theme journal changed current facts");
    for(unsigned row=0;row<state.rows.size();++row)
        Require(state.rows[row].value==wire.at("rows").at(row).at("value").get<std::string>()&&state.rows[row].enabled==wire.at("rows").at(row).at("enabled").get<bool>()
            &&preview.rows[row].value==state.rows[row].value&&preview.rows[row].enabled==state.rows[row].enabled,
            "Theme journal rewrote live row values or action permissions");
    if(state.instructionTheme)Require(wire.at("instructionTheme").size()==3,"Theme state exposed private identity or clocks");
    return state;
}
void CheckColors(const Expected& expected,const std::optional<InstructionHudColors>& colors) {
    if(!expected.creation){Require(!colors,"Absent footer retained drawable colors");return;}
    Require(colors&&Equal(colors->outline,expected.outline)&&Equal(colors->background,expected.background),
        "Actual theme replay differs from independent RGB/time oracle");
}
void ActualInstructionThemeJournalsJoinOwnedEpochsAndIndependentLinearColors(const std::filesystem::path& directory) {
    CheckInventory(directory);std::unique_ptr<InstructionHudLifetime> lifetime;std::unique_ptr<InstructionHudColorHistory> history;
    unsigned graph=99;
    for(const auto& expected:Profiles) {
        if(graph!=expected.graph){graph=expected.graph;lifetime=std::make_unique<InstructionHudLifetime>();history=std::make_unique<InstructionHudColorHistory>();}
        const auto state=ReadAndCheck(directory,expected);
        CheckColors(expected,history->Update(state,lifetime->Observe(&state),layout::Color{80,160,240,255},expected.clock));
    }
    // An unobserved pruned chain holds its original channel until the final event actually settles
    InstructionHudLifetime delayedLifetime;InstructionHudColorHistory delayed;
    for(unsigned index:{29U,30U,31U}) {
        const auto state=ReadAndCheck(directory,Profiles[index]);
        const auto colors=delayed.Update(state,delayedLifetime.Observe(&state),layout::Color{80,160,240,255},Profiles[index].clock);
        Require(colors.has_value(),"Delayed truncated observer lost owned footer");
        Require(Equal(colors->outline,index==31?layout::Color{255,0,0,255}:layout::Color{57,152,254,255}),
            "Delayed truncated chain fabricated an interrupted starting color");
    }
}
}
