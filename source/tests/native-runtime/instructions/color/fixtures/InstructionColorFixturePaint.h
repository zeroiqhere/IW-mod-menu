#pragma once
#include "InstructionColorFixtureExpected.h"
#include "../../../../../native/render/instructions/color/InstructionHudColors.h"
namespace iw::overlay::tests::instruction_color {
inline InstructionHudColors ExpectedPaint(const Expected& expected,bool journal) {
    const auto outline=expected.creation==2 ? layout::Color{57,152,254,255}
        : expected.creation==3 ? layout::Color{255,0,0,255}
        : expected.creation==4 ? layout::Color{242,0,0,255}
        : expected.name.starts_with("design-out") ? layout::Color{121,242,0,255} : layout::Color{0,255,0,255};
    auto background=expected.creation==11 ? layout::Color{25,25,25,217}
        : expected.name=="aio-created-blue" || expected.name=="alternate-red"
            || (expected.creation>=5 && !expected.name.starts_with("design-out") && expected.name!="gap-rainbow")
            ? layout::Color{0,0,0,255}
        : expected.name.starts_with("fixed-green") ? layout::Color{0,255,0,255}
        : expected.name.starts_with("fixed-red") || expected.creation==2 ? layout::Color{255,0,0,255}
        : layout::Color{242,0,0,255};
    // Legacy packets retain their accepted endpoint adapter; explicit receipts own the new duration
    if(journal) {
        if(expected.name=="fixed-red0") background={0,0,0,255};
        if(expected.name=="fixed-red180") background={92,0,0,255};
        if(expected.name=="fixed-red430") background={219,0,0,255};
        if(expected.name=="fixed-green0") background={242,0,0,255};
        if(expected.name=="fixed-green180") background={155,92,0,255};
        if(expected.name=="fixed-green430") background={34,219,0,255};
    }
    return {outline,background};
}
}
