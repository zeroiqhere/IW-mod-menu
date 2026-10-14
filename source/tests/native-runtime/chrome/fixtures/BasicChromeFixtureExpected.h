#pragma once
#include "../../../../native/render/layout/ApparitionLayout.h"
#include <array>
#include <cstdint>

namespace iw::overlay::tests::chrome {
// Each entry names a real controller packet and an independently checked creation boundary
struct ExpectedChrome {
    const char* file;
    std::uint64_t clock, chrome, navigation, menu, sequence;
    std::size_t selected, rows;
    bool open, rainbow, exists;
    layout::Color color;
};
inline constexpr layout::Color Black{0, 0, 0, 255};
inline constexpr layout::Color First{242, 145, 0, 255};
inline constexpr layout::Color Middle{242, 172, 0, 255};
inline constexpr layout::Color Last{242, 208, 0, 255};
inline constexpr std::array<ExpectedChrome, 16> ChromeProfiles{{
    {"basic-black.json",1000,2,25,11,26,0,37,true,false,true,Black},
    {"basic-rainbow0.json",1000,2,61,11,63,36,37,true,true,true,First},
    {"basic-rainbow180.json",1180,2,61,12,63,36,37,true,true,true,Middle},
    {"basic-rainbow430.json",1430,2,61,12,63,36,37,true,true,true,Last},
    {"basic-fixed0.json",1430,2,62,12,65,0,37,true,false,true,Last},
    {"basic-fixed180.json",1610,2,62,13,65,0,37,true,false,true,Last},
    {"basic-fixed430.json",1860,2,62,13,65,0,37,true,false,true,Last},
    {"basic-parent.json",1860,2,64,15,66,5,7,true,false,true,Last},
    {"basic-closed.json",1860,2,65,16,67,5,7,false,false,false,Last},
    {"basic-reopening0.json",1860,2,66,17,68,5,7,true,false,false,Last},
    {"basic-reopened50.json",1910,3,66,17,68,5,7,true,false,true,Black},
    {"basic-width-locked.json",2460,3,72,19,73,0,0,true,false,true,Black},
    {"basic-unlock0.json",2460,3,74,21,74,2,7,true,false,false,Black},
    {"basic-unlock-late150.json",2610,4,74,21,74,2,7,true,false,true,Black},
    {"basic-owner-retired.json",2610,4,75,22,74,2,7,false,false,false,Black},
    {"basic-owner-reopened50.json",2660,5,76,23,75,2,7,true,false,true,Black},
}};
}
