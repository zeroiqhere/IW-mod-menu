#pragma once
#include "../../../shared/Chrome/ChromeThemeState.h"
#include "../../layout/ApparitionLayout.h"
namespace iw::overlay {
struct ChromeColorFrame {
    bool admitted = false;
    unsigned currentRoleMask = 0;
    unsigned knownRoleMask = 0;
    std::array<layout::Color, 5> colors{};
    layout::Color Color(ChromeRole role) const noexcept { return colors[static_cast<unsigned>(role)]; }
};
}
