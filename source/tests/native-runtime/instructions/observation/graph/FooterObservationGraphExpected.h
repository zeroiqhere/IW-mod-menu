#pragma once
#include "../../../../../native/render/layout/ApparitionLayout.h"
#include <array>
namespace iw::overlay::tests::footer_observation {
struct SampleOracle {
    layout::Color observed;
    layout::Color fixed180;
    layout::Color fixed430;
};
// Literal channels independently apply the documented 500 ms native linear adapter
inline constexpr std::array<SampleOracle, 2> SampleOracles{{
    {{80,160,240,255},{143,102,154,255},{231,22,34,255}},
    {{120,40,80,255},{169,26,51,255},{236,6,11,255}}
}};
}
