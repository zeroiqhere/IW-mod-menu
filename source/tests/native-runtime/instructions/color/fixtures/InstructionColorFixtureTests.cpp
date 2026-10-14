#include "InstructionColorFixturePaint.h"
#include "InstructionColorFixtureTests.h"
#include "../../../../native-layout/BackendPreview.h"
#include "../../../../../native/runtime/ControlLabels.h"
#include <fstream>
#include <set>
#include <stdexcept>
#include <nlohmann/json.hpp>
namespace iw::overlay::tests::instruction_color {
namespace {
void Require(bool value,const char* message) { if(!value) throw std::runtime_error(message); }
void Rgb(layout::Color color,unsigned red,unsigned green,unsigned blue,unsigned alpha=255) {
    Require(color.red==red && color.green==green && color.blue==blue && color.alpha==alpha,
        "Actual color history disagrees with independent widget RGB and alpha");
}
}
void CheckInventory(const std::filesystem::path& directory) {
    std::set<std::string> names;
    for(const auto& item:Profiles) names.insert("instruction-color-"+std::string(item.name)+".json");
    unsigned files=0;
    for(const auto& entry:std::filesystem::directory_iterator(directory)) {
        Require(!entry.is_symlink() && entry.is_regular_file(),"Instruction color graph contains a non-regular packet");
        Require(names.contains(entry.path().filename().string()),"Instruction color graph contains an unexpected packet");++files;
    }
    Require(files==Profiles.size(),"Actual instruction color graph is incomplete");
}
std::string ReadPacket(const std::filesystem::path& path) {
    const auto size=std::filesystem::file_size(path);Require(size>0 && size<=32768,"Instruction color packet exceeds its bound");
    std::ifstream file(path,std::ios::binary);std::string bytes(static_cast<std::size_t>(size),'\0');
    file.read(bytes.data(),static_cast<std::streamsize>(size));Require(static_cast<std::size_t>(file.gcount())==size,"Instruction color packet read failed");
    return bytes;
}
MenuSnapshot ReadAndCheck(const std::filesystem::path& directory,const Expected& expected) {
    const auto path=directory/("instruction-color-"+std::string(expected.name)+".json");const auto bytes=ReadPacket(path);
    const runtime::Bindings bindings{{2},{86,6},{2,38},{1,40},{70},{51},{52},{1},{38},{40},{71},{69},{16},{17},{32}};
    const auto labels=runtime::DescribeOpeningActions(bindings);
    const auto parsed=runtime::ParseState(bytes,42,nullptr,nullptr,nullptr,nullptr,nullptr,std::nullopt,nullptr,nullptr,&labels);
    const auto& state=*parsed.snapshot;const auto preview=ReadBackendPreview(path,&labels);const auto wire=nlohmann::json::parse(bytes);
    Require(state.instructionHud && state.instructionHud->exists==(expected.creation!=0)
        && state.navigationRevision==expected.navigation && state.menuRevision==expected.menu
        && parsed.lastSequence==expected.sequence && state.isOpen==expected.open
        && state.rows.size()==expected.rows && state.selectedIndex==expected.selected,
        "Actual creation color changed body epochs or selection");
    Require(state.localMenuEligibility && state.localMenuEligibility->revision==expected.admission
        && state.localMenuEligibility==preview.localMenuEligibility,"Actual creation color changed current actor admission");
    const auto& hud=*state.instructionHud;
    if(expected.creation) {
        const auto accent=expected.creation==2 ? "#3998FE" : expected.creation<=4 ? "#FF0000" : "#00FF00";
        Require(hud.creationRevision==expected.creation && hud.admissionRevision==expected.admission
            && hud.creationAccent==accent && hud.creationRainbow==(expected.creation==4 || expected.creation==11)
            && hud.creationDesign==(expected.creation==11?MenuDesign::Classic:MenuDesign::Aio)
            && hud.creationAlternate==(expected.creation==3) && hud.creationBackground==(expected.creation!=5),
            "Actual immutable creation pair or style changed before recreation");
    } else Require(!hud.creationAccent && !hud.creationRainbow,"Owned absence retained color metadata");
    Require(preview.instructionHud && preview.instructionHud->creationAccent==hud.creationAccent
        && preview.instructionHud->creationRainbow==hud.creationRainbow && preview.instructionHud->creationRevision==hud.creationRevision
        && preview.instructionHud->instructions==hud.instructions && preview.accent==state.accent
        && preview.smoothRainbowTheme==state.smoothRainbowTheme,"Actual instruction color readers disagree");
    Require(wire.at("instructionHud").size()==8 && !wire.at("instructionHud").contains("actor")
        && !wire.at("instructionHud").contains("instructions"),"Color wire exposed private identity or bindings");
    for(unsigned row=0;row<state.rows.size();++row)
        Require(state.rows[row].value==wire.at("rows").at(row).at("value").get<std::string>()
            && state.rows[row].enabled==wire.at("rows").at(row).at("enabled").get<bool>()
            && preview.rows[row].value==state.rows[row].value && preview.rows[row].enabled==state.rows[row].enabled,
            "Color metadata rewrote current values or action permissions");
    return state;
}
void CheckPaintChannels(const Expected& expected,const std::optional<InstructionHudColors>& colors,bool journal) {
    if(!expected.creation) {Require(!colors,"Absent owner retained drawable historical colors");return;}
    Require(colors.has_value(),"Actual paired creation lost historical channels");
    const auto wanted=ExpectedPaint(expected,journal);
    Rgb(colors->outline,wanted.outline.red,wanted.outline.green,wanted.outline.blue,wanted.outline.alpha);
    Rgb(colors->background,wanted.background.red,wanted.background.green,wanted.background.blue,wanted.background.alpha);
}
void ActualInstructionColorsJoinImmutableCreationAndCurrentTargetChannels(const std::filesystem::path& directory) {
    CheckInventory(directory);InstructionHudLifetime lifetime;InstructionHudColorHistory history;
    for(const auto& expected:Profiles) {
        const auto state=ReadAndCheck(directory,expected);
        const auto sample=expected.name.starts_with("design-out") ? layout::Color{121,242,0,255} : layout::Color{242,0,0,255};
        CheckPaintChannels(expected,history.Update(state,lifetime.Observe(&state),sample),state.instructionTheme.has_value());
    }
    // A delayed first observation uses creation mode, never the current stopped mode or fallback accent
    InstructionHudLifetime delayedLifetime;InstructionHudColorHistory delayedHistory;
    const auto state=ReadAndCheck(directory,Profiles[17]);
    const auto colors=delayedHistory.Update(state,delayedLifetime.Observe(&state),layout::Color{10,20,30,255});
    Require(colors.has_value(),"Delayed Rainbow creation lost its first-owner sample");
    Rgb(colors->outline,10,20,30);Rgb(colors->background,0,state.instructionTheme?255:0,0);
}
}
