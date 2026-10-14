#pragma once
#include "MenuAppearance.h"
#include <algorithm>
#include <cstddef>

namespace iw::overlay {
inline constexpr std::size_t MaximumMenuRowsShown = 14;
inline constexpr int MaximumMenuOpeningMilliseconds = 1300;

// Source capacity includes the space reserved for ordinary instructions and the counter
constexpr std::size_t MenuRowCapacity(const MenuAppearance& appearance) noexcept {
    // Stealth fixes the row window before ordinary footer and counter adjustments
    if (appearance.stealthUI) return 5;
    const bool hidden = !appearance.instructionsVisible;
    const bool nonBoxes = appearance.booleanDisplay != "Boxes";
    // AIO and Basic do not draw the counter; other design metrics retain that capability
    const bool counter = appearance.optionCounter && appearance.design != MenuDesign::Aio
        && appearance.design != MenuDesign::Basic;
    return 10 + (hidden ? 1 : 0) + (nonBoxes ? 2 + (hidden ? 1 : 0) : 0) - (counter ? 2 : 0);
}
constexpr std::size_t MenuRowsShown(const MenuAppearance& appearance, std::size_t rowCount) noexcept {
    // Bounding by capacity also handles a caller's largest size without arithmetic overflow
    return std::min(rowCount, MenuRowCapacity(appearance));
}
constexpr int MenuOpeningDurationMilliseconds(const MenuAppearance& appearance, std::size_t rowCount) noexcept {
    const auto shown = MenuRowsShown(appearance, rowCount);
    // Row zero is immediate, so fewer than two rows have no active fade interval
    return shown < 2 ? 0 : static_cast<int>((shown - 1) * 100);
}
}
