#include "FooterWithdrawalFixtureTests.h"
#include "../../../../native-layout/BackendPreview.h"
#include "../../../../../native/runtime/RuntimeSchema.h"
#include "../../../../../native/runtime/ControlLabels.h"
#include <nlohmann/json.hpp>
#include <algorithm>
#include <fstream>
#include <memory>
#include <set>
#include <stdexcept>
namespace iw::overlay::tests::footer_withdrawal {
namespace {
void Require(bool value,const char* message) { if(!value) throw std::runtime_error(message); }
bool Same(layout::Color a,layout::Color b) {
    return a.red==b.red && a.green==b.green && a.blue==b.blue && a.alpha==b.alpha;
}
}
void CheckInventory(const std::filesystem::path& directory) {
    std::set<std::string> names;for(const auto& item:Profiles) names.insert(item.name);
    unsigned files=0;
    for(const auto& entry:std::filesystem::directory_iterator(directory)) {
        Require(!entry.is_symlink() && entry.is_regular_file(),"Footer graph contains a non-regular packet");
        Require(names.contains(entry.path().filename().string()),"Footer graph contains an unexpected packet");++files;
    }
    Require(files==Profiles.size(),"Actual footer withdrawal graph is incomplete");
}
MenuSnapshot ReadAndCheck(const std::filesystem::path& directory,const Expected& expected) {
    const auto path=directory/expected.name;const auto size=std::filesystem::file_size(path);
    Require(size>0 && size<=32768,"Footer packet exceeds its bound");
    std::ifstream stream(path,std::ios::binary);std::string bytes(static_cast<std::size_t>(size),'\0');
    stream.read(bytes.data(),static_cast<std::streamsize>(size));
    Require(static_cast<std::size_t>(stream.gcount())==size,"Footer packet read failed");
    // Both readers resolve only the exact immutable groups used by the inert controller fixture
    const runtime::Bindings bindings{{2},{86,6},{2,38},{1,40},{70},{51},{52},{1},{38},{40},{71},{69},{16},{17},{32}};
    const auto labels=runtime::DescribeOpeningActions(bindings);
    const auto parsed=runtime::ParseState(bytes,42,nullptr,nullptr,nullptr,nullptr,nullptr,std::nullopt,nullptr,nullptr,&labels);
    const auto preview=ReadBackendPreview(path,&labels);const auto wire=nlohmann::json::parse(bytes);
    Require(parsed.snapshot!=nullptr,"Production reader did not publish a parsed footer snapshot");
    const auto& state=*parsed.snapshot;
    Require(state.isOpen==expected.open && state.navigationRevision==expected.navigation && state.menuRevision==expected.menu
        && parsed.lastSequence==expected.sequence,"Footer graph changed confirmed body or action epochs");
    Require(state.localMenuEligibility==preview.localMenuEligibility,"Footer readers disagree on current actor admission");
    if(expected.admission) Require(state.localMenuEligibility && state.localMenuEligibility->revision==expected.admission
        && state.localMenuEligibility->life==expected.life,"Actual admission or known life changed");
    else Require(!state.localMenuEligibility,"Unverified provider published actor admission");
    Require(state.instructionHud && preview.instructionHud,"Withdrawal lost explicit footer suppression");
    const auto& hud=*state.instructionHud;const auto& other=*preview.instructionHud;
    Require(hud.exists==(expected.creation!=0) && hud.creationRevision.value_or(0)==expected.creation,
        "Unknown observation destroyed or recreated a retained footer");
    if(expected.creation) Require(hud.admissionRevision==expected.admission && hud.creationDesign==expected.design
        && hud.creationAlternate==expected.alternate && hud.creationBackground==true && hud.creationRainbow==expected.rainbow
        && hud.creationAccent==(expected.creation==1 || expected.rainbow ? "#3998FE" : "#FF0000"),
        "Recovery changed captured footer style or mode");
    else Require(!hud.creationRevision && !hud.admissionRevision && !hud.creationDesign && !hud.creationAlternate
        && !hud.creationBackground && !hud.creationAccent && !hud.creationRainbow && hud.instructions.empty(),
        "Canonical absence retained active footer fields");
    Require(hud.exists==other.exists && hud.creationRevision==other.creationRevision && hud.admissionRevision==other.admissionRevision
        && hud.creationDesign==other.creationDesign && hud.creationAlternate==other.creationAlternate
        && hud.creationBackground==other.creationBackground && hud.creationAccent==other.creationAccent
        && hud.creationRainbow==other.creationRainbow && hud.instructions==other.instructions,"Footer readers disagree on immutable creation");
    Require(wire.at("instructionHud").size()==8,"Footer wire exposed extra private identity or saved hints");
    Require(state.instructionTheme.has_value()==(expected.creation!=0)
        && state.instructionTheme.has_value()==preview.instructionTheme.has_value(),"Withdrawn theme did not preserve its separate authority scope");
    if(state.instructionTheme) {
        const auto& theme=*state.instructionTheme;const auto& copy=*preview.instructionTheme;
        Require(theme.creationRevision==expected.creation && theme.prefixComplete==expected.prefix && theme.events.size()==expected.events
            && (theme.events.empty() || theme.events.back().elapsedMilliseconds==expected.latestAge),"Actual fixed invocation ages or prefix changed");
        Require(theme.creationRevision==copy.creationRevision && theme.prefixComplete==copy.prefixComplete && theme.events.size()==copy.events.size(),
            "Footer readers disagree on journal ownership");
        for(unsigned i=0;i<theme.events.size();++i) {
            const auto& a=theme.events[i];const auto& b=copy.events[i];
            Require(a.revision==b.revision && a.target==b.target && a.accent==b.accent
                && a.elapsedMilliseconds==b.elapsedMilliseconds && a.predecessorGapMilliseconds==b.predecessorGapMilliseconds
                && a.stoppedRainbow==b.stoppedRainbow,"Footer readers disagree on captured invocation facts");
        }
    }
    // Presentation cannot rewrite current feature values or grant eligibility to a withdrawn actor
    Require(state.accent==preview.accent && state.accent==wire.at("accent").get<std::string>()
        && state.smoothRainbowTheme==preview.smoothRainbowTheme,"Footer history changed current palette");
    Require(state.rows.size()==preview.rows.size() && state.rows.size()==wire.at("rows").size(),
        "Footer readers disagree on current row inventory");
    for(unsigned i=0;i<state.rows.size();++i) Require(state.rows[i].value==wire.at("rows").at(i).at("value").get<std::string>()
        && state.rows[i].enabled==wire.at("rows").at(i).at("enabled").get<bool>()
        && state.rows[i].value==preview.rows[i].value && state.rows[i].enabled==preview.rows[i].enabled,
        "Footer history changed current row values or action admission");
    return state;
}
void CheckColors(const Expected& expected,const std::optional<InstructionHudColors>& colors) {
    if(!expected.creation) {Require(!colors,"Withdrawn footer retained drawable color authority");return;}
    Require(colors && Same(colors->outline,expected.colors.outline) && Same(colors->background,expected.colors.background),
        "Recovered footer RGB or alpha differs from the independent source/native adapter oracle");
}
void ActualFooterWithdrawalsJoinSuppressedPaintAndRetainedHistory(const std::filesystem::path& directory,std::ostream* dump) {
    CheckInventory(directory);unsigned graph=99;std::uint64_t nativeClock=0;
    std::unique_ptr<InstructionHudLifetime> lifetime;std::unique_ptr<InstructionHudColorHistory> history;
    for(const auto& expected:Profiles) {
        if(graph!=expected.graph) {
            graph=expected.graph;nativeClock=0;lifetime=std::make_unique<InstructionHudLifetime>();history=std::make_unique<InstructionHudColorHistory>();
        }
        const auto state=ReadAndCheck(directory,expected);nativeClock=std::max(nativeClock,expected.clock);
        const bool admitted=lifetime->Observe(&state);Require(admitted==(expected.creation!=0),"Actual lifetime confused withdrawal with destruction");
        const auto colors=history->Update(state,admitted,layout::Color{80,160,240,255},nativeClock);CheckColors(expected,colors);
        if(dump) {
            *dump<<expected.name<<','<<expected.clock<<','<<nativeClock<<','<<expected.creation<<','<<admitted;
            for(auto color:{colors?colors->outline:layout::Color{},colors?colors->background:layout::Color{}})
                *dump<<','<<unsigned(color.red)<<','<<unsigned(color.green)<<','<<unsigned(color.blue)<<','<<unsigned(color.alpha);
            *dump<<'\n';
        }
    }
}
}
