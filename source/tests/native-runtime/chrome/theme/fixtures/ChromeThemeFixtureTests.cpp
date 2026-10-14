#include "ChromeThemeFixtureTests.h"
#include "../../../../native-layout/BackendPreview.h"
#include "../../../../../native/runtime/RuntimeSchema.h"
#include <fstream>
#include <nlohmann/json.hpp>
#include <algorithm>
#include <memory>
#include <set>
#include <stdexcept>
namespace iw::overlay::tests::chrome_theme {
namespace {
void Require(bool value,const char* message) { if (!value) throw std::runtime_error(message); }
bool Same(layout::Color a,layout::Color b) { return a.red==b.red && a.green==b.green && a.blue==b.blue && a.alpha==b.alpha; }
}
void CheckInventory(const std::filesystem::path& directory) {
    std::set<std::string> names;for (const auto& p:Profiles) names.insert(std::string(p.name));
    unsigned files=0;
    for (const auto& entry:std::filesystem::directory_iterator(directory)) {
        Require(!entry.is_symlink() && entry.is_regular_file(),"Chrome graph contains a non-regular packet");
        Require(names.contains(entry.path().filename().string()),"Chrome graph contains an unexpected packet");++files;
    }
    Require(files==Profiles.size(),"Chrome graph is incomplete");
}
MenuSnapshot ReadAndCheck(const std::filesystem::path& directory,const Expected& expected) {
    const auto path=directory/std::string(expected.name);const auto size=std::filesystem::file_size(path);
    Require(size>0 && size<=32768,"Chrome packet exceeds its bound");
    std::ifstream stream(path,std::ios::binary);std::string bytes(static_cast<std::size_t>(size),'\0');
    stream.read(bytes.data(),static_cast<std::streamsize>(size));Require(static_cast<std::size_t>(stream.gcount())==size,"Chrome packet read failed");
    const auto parsed=runtime::ParseState(bytes,42);const auto preview=ReadBackendPreview(path);const auto wire=nlohmann::json::parse(bytes);
    const auto& state=*parsed.snapshot;
    Require(state.navigationRevision==expected.navigation && state.menuRevision==expected.menu && parsed.lastSequence==expected.sequence,
        "Chrome receipt changed confirmed navigation/menu/action epochs");
    Require(state.chromeTheme.has_value()==expected.descriptor && preview.chromeTheme.has_value()==expected.descriptor,"Readers lost explicit source ownership");
    if (state.chromeTheme) {
        const auto& a=*state.chromeTheme;const auto& b=*preview.chromeTheme;
        Require(a.exists==(expected.creation!=0) && a.creationRevision.value_or(0)==expected.creation && a.currentRoleMask==expected.roles
            && a.events.size()==expected.events && a.prefixComplete==expected.prefixComplete,"Actual source chrome chronology changed");
        Require(a.exists==b.exists && a.creationRevision==b.creationRevision && a.admissionRevision==b.admissionRevision
            && a.creationDesign==b.creationDesign && a.creationAccent==b.creationAccent && a.creationRainbow==b.creationRainbow
            && a.currentRoleMask==b.currentRoleMask && a.prefixComplete==b.prefixComplete && a.events.size()==b.events.size(),"Chrome readers disagree on owned creation");
        for (unsigned i=0;i<a.events.size();++i) {
            const auto& x=a.events[i];const auto& y=b.events[i];
            Require(x.revision==y.revision && x.targetMask==y.targetMask && x.accent==y.accent && x.elapsedMilliseconds==y.elapsedMilliseconds
                && x.predecessorGapMilliseconds==y.predecessorGapMilliseconds && x.stoppedRainbow==y.stoppedRainbow,"Readers disagree on immutable invocation facts");
            Require(wire.at("chromeTheme").at("events").at(i).size()==6,"Chrome invocation exposed extra private data");
        }
        Require(wire.at("chromeTheme").size()==9,"Chrome creation exposed extra private data");
    }
    Require(state.accent==wire.at("accent").get<std::string>() && state.smoothRainbowTheme==wire.at("smoothRainbowTheme").get<bool>()
        && state.accent==preview.accent && state.localMenuEligibility==preview.localMenuEligibility,"Presentation changed current color or actor admission");
    for (unsigned i=0;i<state.rows.size();++i) Require(state.rows[i].value==wire.at("rows").at(i).at("value").get<std::string>()
        && state.rows[i].enabled==wire.at("rows").at(i).at("enabled").get<bool>() && state.rows[i].value==preview.rows[i].value
        && state.rows[i].enabled==preview.rows[i].enabled,"Chrome history rewrote current row values or action permission");
    return state;
}
void CheckColors(const Expected& expected,const std::optional<ChromeColorFrame>& frame) {
    if (!expected.creation) { Require(!frame || !frame->admitted,"Withdrawn chrome remained drawable"); return; }
    Require(frame && frame->admitted && frame->currentRoleMask==expected.roles && frame->knownRoleMask==expected.known,"Actual history lost source ownership/known membership");
    for (unsigned i=0;i<5;++i) if (expected.roles & (1U<<i)) Require(Same(frame->colors[i],expected.colors[i]),"Actual RGB/alpha differs from independent source/linear oracle");
}
void ActualChromeThemesJoinCreationScopesAndIndependentRoleColors(const std::filesystem::path& directory,std::ostream* dump) {
    CheckInventory(directory);std::unique_ptr<ChromeColorHistory> history;unsigned graph=99;std::uint64_t nativeClock=0;
    for (const auto& expected:Profiles) {
        if (graph!=expected.graph) { graph=expected.graph;history=std::make_unique<ChromeColorHistory>();nativeClock=0; }
        // Backend-only clock rollback cannot make the renderer's independent monotonic clock regress
        nativeClock=std::max(nativeClock,expected.clock);const auto state=ReadAndCheck(directory,expected);
        const auto colors=history->Update(state,layout::Color{80,160,240,255},nativeClock);CheckColors(expected,colors);
        if (dump && colors) for (unsigned role=0;role<5;++role) {
            const auto c=colors->colors[role];*dump<<expected.name<<','<<role<<','<<unsigned(c.red)<<','<<unsigned(c.green)<<','<<unsigned(c.blue)<<','<<unsigned(c.alpha)
                <<','<<((colors->knownRoleMask&(1U<<role))!=0)<<','<<colors->admitted<<'\n';
        }
    }
    // A delayed first observation cannot label an unobserved Basic Rainbow stop as known history
    ChromeColorHistory delayed;const auto found=std::find_if(Profiles.begin(),Profiles.end(),[](const auto& p){return p.name=="chrome-theme-delayed-fixed-stop.json";});
    Require(found!=Profiles.end(),"Delayed first-observation fixture missing");const auto& expected=*found;
    const auto state=ReadAndCheck(directory,expected);const auto colors=delayed.Update(state,layout::Color{80,160,240,255},expected.clock);
    Require(colors && colors->admitted && !(colors->knownRoleMask & 5),"Late first receipt invented an interrupted Rainbow seed");
}
}
