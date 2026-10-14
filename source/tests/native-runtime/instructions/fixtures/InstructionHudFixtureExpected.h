#pragma once
#include "../../../../native/shared/MenuSnapshot.h"
#include <array>
namespace iw::overlay::tests::instructions {
struct ExpectedInstructionHud {
    const char* name;
    unsigned subgraph;
    std::uint64_t creation, admission, navigation, menu, chrome, sequence;
    std::size_t rows, selected;
    bool open, quick, exists, controlsDisabled, hasMenu;
    PlayerLifeState life;
    MenuDesign creationDesign;
    bool alternate, background;
};
inline constexpr auto Alive = PlayerLifeState::Alive;
inline constexpr auto Dead = PlayerLifeState::Dead;
inline constexpr auto Unknown = PlayerLifeState::Unknown;
inline constexpr auto App = MenuDesign::Apparition;
inline constexpr auto Classic = MenuDesign::Classic;
// Constants were independently checked against the actual command graph and source creation boundaries
inline constexpr std::array<ExpectedInstructionHud,35> InstructionProfiles{{
    {"alive-closed",0,1,10,0,1,0,0,8,0,false,false,true,false,true,Alive,App,false,true},
    {"main-rebuild0",0,1,10,1,2,0,1,8,0,true,false,true,true,true,Alive,App,false,true},
    {"main-drawn50",0,1,10,1,2,1,1,8,0,true,false,true,true,true,Alive,App,false,true},
    {"main-ready600",0,1,10,1,2,1,1,8,0,true,false,true,false,true,Alive,App,false,true},
    {"main-closed",0,1,10,2,3,1,2,8,0,false,false,true,false,true,Alive,App,false,true},
    {"quick-open0",0,1,10,3,5,2,3,14,0,true,true,true,true,true,Alive,App,false,true},
    {"quick-ready550",0,1,10,3,5,2,3,14,0,true,true,true,false,true,Alive,App,false,true},
    {"quick-dead",0,2,11,3,6,2,3,3,0,true,true,true,false,true,Dead,App,false,true},
    {"dead-closed",0,2,11,4,7,2,4,3,0,false,true,true,false,true,Dead,App,false,true},
    {"alive-again-closed",0,2,12,4,8,2,4,14,0,false,true,true,false,true,Alive,App,false,true},
    {"permission-lost",0,0,13,4,9,2,4,1,0,false,true,false,false,false,Alive,App,false,true},
    {"permission-restored",0,3,14,4,10,2,4,14,0,false,true,true,false,true,Alive,App,false,true},
    {"owner-replaced",0,4,15,4,10,2,4,14,0,false,true,true,false,true,Alive,App,false,true},
    {"design-rebuild0",0,4,15,19,16,3,19,12,0,true,false,true,true,true,Alive,App,false,true},
    {"design-drawn50",0,5,15,19,17,4,19,12,0,true,false,true,true,true,Alive,Classic,false,true},
    {"design-ready250",0,5,15,19,17,4,19,12,0,true,false,true,false,true,Alive,Classic,false,true},
    {"alternate",0,6,15,29,21,4,28,6,1,true,false,true,true,true,Alive,Classic,true,true},
    {"background-off",0,7,15,30,22,4,30,6,2,true,false,true,true,true,Alive,Classic,true,false},
    {"disabled",0,0,15,34,24,4,35,6,0,true,false,false,true,true,Alive,Classic,true,false},
    {"re-enabled",0,8,15,34,26,4,36,6,0,true,false,true,true,true,Alive,Classic,true,false},
    {"width-soft-lock",0,8,15,43,31,4,43,0,0,true,false,true,true,true,Alive,Classic,true,false},
    {"width-return0",0,8,15,45,33,4,44,7,2,true,false,true,true,true,Alive,Classic,true,false},
    {"width-return150",0,8,15,45,33,5,44,7,2,true,false,true,false,true,Alive,Classic,true,false},
    {"focus-hidden",0,0,15,45,33,5,44,7,2,false,false,false,false,true,Alive,Classic,true,false},
    {"dead-closed-quick-disabled",1,2,11,21,8,1,20,12,5,false,false,true,false,true,Dead,App,false,true},
    {"design-death25",2,2,11,15,6,1,15,12,0,true,false,true,true,true,Dead,Classic,false,true},
    {"design-death-drawn50",2,3,11,15,7,2,15,12,0,true,false,true,true,true,Dead,Classic,false,true},
    {"design-unknown25",3,0,11,15,6,1,15,12,0,true,false,false,true,true,Unknown,Classic,false,true},
    {"design-recovered30",3,1,12,15,6,1,15,12,0,true,false,true,true,true,Alive,App,false,true},
    {"design-recovered-drawn50",3,2,12,15,7,2,15,12,0,true,false,true,true,true,Alive,Classic,false,true},
    {"design-permission-lost25",4,0,11,16,7,1,15,12,0,false,false,false,false,false,Alive,Classic,false,true},
    {"design-permission-restored30",4,2,12,16,7,1,15,12,0,false,false,true,false,true,Alive,Classic,false,true},
    {"design-permission-canceled50",4,2,12,16,7,1,15,12,0,false,false,true,false,true,Alive,Classic,false,true},
    {"design-owner-replaced25",5,2,11,16,7,1,15,12,0,false,false,true,false,true,Alive,Classic,false,true},
    {"design-owner-canceled50",5,2,11,16,7,1,15,12,0,false,false,true,false,true,Alive,Classic,false,true},
}};
}
