#pragma once
#include "../../../native/render/layout/ApparitionLayout.h"
#include <array>
namespace iw::overlay::tests::palette {
struct SourceColor { const char* name; const char* hex; layout::Color rgb; };
// Literal RGB bytes are copied from pinned source and never pass through the production hex parser
inline constexpr std::array<SourceColor, 36> SourcePalette{{
    {"Red", "#FF0000", {255, 0, 0, 255}},
    {"Green", "#00FF00", {0, 255, 0, 255}},
    {"Blue", "#0000FF", {0, 0, 255, 255}},
    {"Black", "#000000", {0, 0, 0, 255}},
    {"White", "#FFFFFF", {255, 255, 255, 255}},
    {"Gray", "#808080", {128, 128, 128, 255}},
    {"Dodger Blue", "#3998FE", {57, 152, 254, 255}},
    {"Ocean Blue", "#0064C8", {0, 100, 200, 255}},
    {"Deep Blue", "#00008B", {0, 0, 139, 255}},
    {"Midnight Blue", "#191970", {25, 25, 112, 255}},
    {"Sky Blue", "#87CEFA", {135, 206, 250, 255}},
    {"Cyan", "#00FFFF", {0, 255, 255, 255}},
    {"Aqua", "#00FFC8", {0, 255, 200, 255}},
    {"Teal", "#008080", {0, 128, 128, 255}},
    {"Pink", "#FF6EFF", {255, 110, 255, 255}},
    {"AIO Pink", "#FF96FF", {255, 150, 255, 255}},
    {"Hot Pink", "#FF1493", {255, 20, 147, 255}},
    {"Rose", "#FF66CC", {255, 102, 204, 255}},
    {"Fuchsia", "#FF00FF", {255, 0, 255, 255}},
    {"Purple", "#8000FF", {128, 0, 255, 255}},
    {"Lavender", "#C8A2FF", {200, 162, 255, 255}},
    {"Violet", "#EE82EE", {238, 130, 238, 255}},
    {"Indigo", "#4B0082", {75, 0, 130, 255}},
    {"Plasma Purple", "#C800FF", {200, 0, 255, 255}},
    {"Neon Purple", "#AA00FF", {170, 0, 255, 255}},
    {"Crimson", "#DC143C", {220, 20, 60, 255}},
    {"Fire Red", "#FF1E1E", {255, 30, 30, 255}},
    {"Ruby", "#E0115F", {224, 17, 95, 255}},
    {"Orange", "#FF8000", {255, 128, 0, 255}},
    {"Deep Orange", "#FF5000", {255, 80, 0, 255}},
    {"Yellow", "#FFFF00", {255, 255, 0, 255}},
    {"Gold", "#FFD700", {255, 215, 0, 255}},
    {"Mint", "#98FF98", {152, 255, 152, 255}},
    {"Lime", "#96FF00", {150, 255, 0, 255}},
    {"Toxic Green", "#00FF64", {0, 255, 100, 255}},
    {"Emerald", "#00C957", {0, 201, 87, 255}},}};
}
