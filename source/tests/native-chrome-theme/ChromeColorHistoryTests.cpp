#include "../../native/render/chrome/theme/ChromeColorHistory.h"
#include <functional>
#include <iostream>
#include <limits>
#include <stdexcept>
#include <vector>
using namespace iw::overlay;
namespace {
void Require(bool value, const char* message) { if (!value) throw std::runtime_error(message); }
MenuSnapshot Packet(MenuDesign design = MenuDesign::Apparition) {
    MenuSnapshot s; s.isOpen = true; s.appearance.design = design; s.menuChromeRevision = 1;
    s.localMenuEligibility = LocalMenuEligibility{7, PlayerLifeState::Alive, true, false, false};
    s.chromeTheme = ChromeThemeState{true, 1, 7, design, ChromeRgb{0, 0, 255}, false, ChromeCreationMask(design), true, {}};
    return s;
}
ChromeThemeEvent Event(unsigned id, MenuDesign design, ChromeRgb rgb, std::uint64_t age,
    std::optional<unsigned> gap = std::nullopt, bool stopped = false) {
    return {id, ChromeFixedMask(design), rgb, age, gap, stopped};
}
const ChromeColorFrame& Have(const std::optional<ChromeColorFrame>& frame) {
    Require(frame && frame->admitted, "Owned chrome was not admitted"); return *frame;
}
void Color(const ChromeColorFrame& f, ChromeRole role, unsigned r, unsigned g, unsigned b, unsigned a = 255) {
    const auto c = f.Color(role); Require(c.red == r && c.green == g && c.blue == b && c.alpha == a, "Independent source RGB/alpha oracle failed");
}
}
int main() {
    const std::vector<std::pair<const char*, std::function<void()>>> cases{
    {"All five creations preserve literal role RGB and source alpha", [] {
        for (auto design : {MenuDesign::Apparition, MenuDesign::Classic, MenuDesign::Native, MenuDesign::Aio, MenuDesign::Basic}) {
            ChromeColorHistory history; auto s = Packet(design); auto f = history.Update(s, std::nullopt, 1000);
            Require(Have(f).currentRoleMask == ChromeCreationMask(design) && Have(f).knownRoleMask == ChromeCreationMask(design), "Source role membership lost");
            const bool gray = design == MenuDesign::Apparition, black = design == MenuDesign::Basic;
            Color(Have(f), ChromeRole::Scroller, gray ? 55 : 0, gray ? 55 : 0, gray ? 55 : black ? 0 : 255, design == MenuDesign::Native ? 179 : 255);
            if (design != MenuDesign::Aio) Color(Have(f), ChromeRole::Banner, gray ? 55 : 0, gray ? 55 : 0, gray ? 55 : black ? 0 : 255, design == MenuDesign::Native ? 230 : 255);
            const bool white = design == MenuDesign::Classic || design == MenuDesign::Native || design == MenuDesign::Aio;
            Color(Have(f), ChromeRole::Title, white ? 255 : 0, white ? 255 : 0, 255, design == MenuDesign::Native ? 179 : 255);
            if (design == MenuDesign::Native) Color(Have(f), ChromeRole::Separator, 0, 0, 0);
        }
    }},
    {"Fixed retargets retain fractional starts and cap repeated native lead", [] {
        ChromeColorHistory history; auto s = Packet(); s.chromeTheme->events = {Event(1, MenuDesign::Apparition, {255,0,0}, 0)};
        auto f = history.Update(s, std::nullopt, 1000); Color(Have(f), ChromeRole::Title, 0, 0, 255);
        f = history.Update(s, std::nullopt, 1100); Color(Have(f), ChromeRole::Title, 51, 0, 204);
        f = history.Update(s, std::nullopt, 1500); Color(Have(f), ChromeRole::Title, 51, 0, 204);
        s.chromeTheme->events[0].elapsedMilliseconds = 1; f = history.Update(s, std::nullopt, 1501);
        Color(Have(f), ChromeRole::Title, 51, 0, 204);
        s.chromeTheme->events = {Event(1,MenuDesign::Apparition,{255,0,0},500), Event(2,MenuDesign::Apparition,{0,0,255},70,430)};
        f = history.Update(s,std::nullopt,1600); Color(Have(f),ChromeRole::Title,189,0,66);
        ChromeColorHistory late; f = late.Update(s,std::nullopt,9000); Color(Have(f),ChromeRole::Title,189,0,66);
        Color(Have(f), ChromeRole::Scroller, 55, 55, 55);
    }},
    {"Direct Rainbow updates hidden existing roles and never recreates destroyed title", [] {
        ChromeColorHistory history; auto s=Packet(MenuDesign::Basic); s.smoothRainbowTheme=true;
        auto f=history.Update(s,layout::Color{80,160,240,0},1000); Color(Have(f),ChromeRole::Scroller,80,160,240);
        Color(Have(f),ChromeRole::Title,80,160,240); Color(Have(f),ChromeRole::Banner,0,0,0);
        s.chromeTheme->currentRoleMask=3; f=history.Update(s,layout::Color{10,20,30,0},1100);
        Require(Have(f).currentRoleMask==3,"Width title destruction was lost"); Color(Have(f),ChromeRole::Scroller,10,20,30);
        Color(Have(f),ChromeRole::Title,80,160,240);
        s.chromeTheme->currentRoleMask=7; f=history.Update(s,layout::Color{1,2,3,0},1200);
        Require(f && !f->admitted,"Same-ID title resurrection was admitted");
    }},
    {"Receipt mutation role resurrection rollback and retired IDs stay quarantined", [] {
        ChromeColorHistory invalid; auto bad=Packet(); bad.localMenuEligibility->life=static_cast<PlayerLifeState>(99);
        auto denied=invalid.Update(bad,std::nullopt,1000);Require(denied && !denied->admitted,"Invalid typed life was accepted");
        bad.localMenuEligibility->life=PlayerLifeState::Alive;denied=invalid.Update(bad,std::nullopt,1001);
        Require(denied && !denied->admitted,"Corrupt active scope revived the same creation");
        ChromeColorHistory history; auto s=Packet(); history.Update(s,std::nullopt,1000);
        s.chromeTheme->creationAccent=ChromeRgb{255,0,0}; auto f=history.Update(s,std::nullopt,1001);
        Require(f && !f->admitted,"Creation RGB mutation revived authority"); s.chromeTheme->creationAccent=ChromeRgb{0,0,255};
        f=history.Update(Packet(MenuDesign::Basic),std::nullopt,1002); Require(f && !f->admitted,"Quarantine revived old ID");
    }},
    {"Focus omission preserves ownership while actual close requires a newer creation", [] {
        ChromeColorHistory history; auto s=Packet(MenuDesign::Basic); s.smoothRainbowTheme=true;
        history.Update(s,layout::Color{10,20,30,255},1000); auto hidden=s; hidden.isOpen=false; hidden.chromeTheme.reset();
        history.Update(hidden,std::nullopt,1100); s.smoothRainbowTheme=false;
        auto unknown=s; unknown.chromeTheme.reset(); unknown.localMenuEligibility->life=PlayerLifeState::Unknown;
        auto withdrawn=history.Update(unknown,layout::Color{200,201,202,255},1150);
        Require(withdrawn && !withdrawn->admitted,"Unknown life retained visible chrome");
        ChromeColorHistory unseen;withdrawn=unseen.Update(unknown,std::nullopt,1150);
        Require(withdrawn && !withdrawn->admitted,"Unobserved unknown owner fell back to legacy paint");
        auto f=history.Update(s,std::nullopt,1200); Color(Have(f),ChromeRole::Scroller,10,20,30);
        hidden.chromeTheme=ChromeThemeState{}; history.Update(hidden,std::nullopt,1300);
        f=history.Update(s,std::nullopt,1400); Require(f && !f->admitted,"Closed creation revived");
        s.menuChromeRevision=2;s.chromeTheme->creationRevision=2; f=history.Update(s,std::nullopt,1500); Color(Have(f),ChromeRole::Scroller,0,0,0);
        ChromeColorHistory absentFirst; auto first=Packet(MenuDesign::Basic);first.chromeTheme=ChromeThemeState{};
        absentFirst.Update(first,std::nullopt,1000);first.chromeTheme.reset();first.localMenuEligibility->life=PlayerLifeState::Unknown;
        absentFirst.Update(first,std::nullopt,1100);first=Packet(MenuDesign::Basic);
        f=absentFirst.Update(first,std::nullopt,1200);Require(f && !f->admitted,"First absent parent revived at the same ID");
        first.menuChromeRevision=2;first.chromeTheme->creationRevision=2;
        f=absentFirst.Update(first,std::nullopt,1300);Color(Have(f),ChromeRole::Scroller,0,0,0);
    }},
    {"Incomplete and unseen Rainbow starts hold displayed RGB until proved endpoint", [] {
        auto s=Packet(); s.chromeTheme->prefixComplete=false;
        s.chromeTheme->events={Event(9,MenuDesign::Apparition,{255,0,0},250,430)};
        ChromeColorHistory h; auto f=h.Update(s,std::nullopt,1000); Color(Have(f),ChromeRole::Title,0,0,255);
        Require((Have(f).knownRoleMask & 4)==0,"Missing history was labelled known");
        s.chromeTheme->events[0].elapsedMilliseconds=500;f=h.Update(s,std::nullopt,1250);Color(Have(f),ChromeRole::Title,255,0,0);
        s=Packet();s.chromeTheme->events={Event(1,MenuDesign::Apparition,{255,0,0},0)};ChromeColorHistory lost;
        lost.Update(s,std::nullopt,1000); lost.Update(s,std::nullopt,1100);s.chromeTheme->prefixComplete=false;s.chromeTheme->events.clear();
        f=lost.Update(s,std::nullopt,1101);Color(Have(f),ChromeRole::Title,51,0,204);
        s=Packet(MenuDesign::Basic);s.chromeTheme->events={Event(1,MenuDesign::Basic,{255,0,0},70,std::nullopt,true)};
        ChromeColorHistory unseen;f=unseen.Update(s,std::nullopt,1000);Color(Have(f),ChromeRole::Title,0,0,255);
        Require((Have(f).knownRoleMask & 1)==0,"Unseen Rainbow stop fabricated Basic scroller RGB");
        s.chromeTheme->events[0].elapsedMilliseconds=500;f=unseen.Update(s,std::nullopt,1430);Color(Have(f),ChromeRole::Title,255,0,0);
        Require((Have(f).knownRoleMask & 1)==0,"Fixed title endpoint fabricated Basic scroller recovery");
        ChromeColorHistory repeated; s=Packet(MenuDesign::Basic);s.smoothRainbowTheme=true;
        repeated.Update(s,layout::Color{80,160,240,255},1000);s.smoothRainbowTheme=false;
        s.chromeTheme->events={Event(1,MenuDesign::Basic,{255,0,0},500,std::nullopt,true),Event(2,MenuDesign::Basic,{0,0,255},0,500,true)};
        f=repeated.Update(s,std::nullopt,1500);
        Require((Have(f).knownRoleMask & 5)==0,"A prior sample seeded a second unobserved Rainbow interruption");
    }}
    };
    unsigned passed=0; for(const auto& [name,test]:cases) {try {test();++passed;std::cout<<"PASS "<<name<<'\n';}catch(const std::exception& e){std::cerr<<"FAIL "<<name<<": "<<e.what()<<'\n';}}
    std::cout<<passed<<"/"<<cases.size()<<" chrome history checks passed\n"; return passed==cases.size()?0:1;
}
