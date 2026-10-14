#pragma once
#include <array>
#include <cstdint>
#include <string_view>
namespace iw::overlay::tests::instruction_color {
struct Expected {
    std::string_view name;
    std::uint64_t creation,admission,navigation,menu,sequence;
    unsigned rows,selected;
    bool open;
};
inline constexpr std::array<Expected,31> Profiles{{
    {"aio-created-blue",2,10,15,7,17,12,0,true},
    {"fixed-red0",2,10,25,11,26,37,0,true},
    {"fixed-red180",2,10,25,12,26,37,0,true},
    {"fixed-red430",2,10,25,12,26,37,0,true},
    {"fixed-red1000",2,10,25,12,26,37,0,true},
    {"parent",2,10,27,14,27,7,5,true},
    {"closed",2,10,28,15,28,7,5,false},
    {"reopen0",2,10,29,16,29,7,5,true},
    {"reopen50",2,10,29,16,29,7,5,true},
    {"reopen600",2,10,29,16,29,7,5,true},
    {"alternate-red",3,10,38,18,38,6,1,true},
    {"rainbow0",3,10,79,23,78,37,36,true},
    {"rainbow430",3,10,79,24,78,37,36,true},
    {"rainbow-recreated",4,10,89,28,87,6,1,true},
    {"fixed-green0",4,10,96,33,93,37,1,true},
    {"fixed-green180",4,10,96,34,93,37,1,true},
    {"fixed-green430",4,10,96,34,93,37,1,true},
    {"fixed-green1000",4,10,96,34,93,37,1,true},
    {"background-disabled",5,10,107,38,103,6,2,true},
    {"background-recreated",6,10,107,39,104,6,2,true},
    {"disabled",0,10,111,41,109,6,0,true},
    {"re-enabled",7,10,111,43,110,6,0,true},
    {"dead-closed",8,11,112,45,111,6,0,false},
    {"alive-again",8,12,112,45,111,6,0,false},
    {"permission-lost",0,13,112,45,111,6,0,false},
    {"permission-restored",9,14,112,45,111,6,0,false},
    {"owner-replaced",10,15,112,45,111,6,0,false},
    {"gap-rainbow",10,15,153,51,151,37,36,true},
    {"design-out0",10,15,158,55,157,12,0,true},
    {"design-out49",10,15,158,55,157,12,0,true},
    {"design-out50",11,15,158,56,157,12,0,true},
}};
}
