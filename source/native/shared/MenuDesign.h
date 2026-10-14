#pragma once

namespace iw::overlay {
// The five source designs are bounded before reaching layout or rendering
// Aio maps only to the exact source spelling AIO in serialized packets
enum class MenuDesign { Apparition, Classic, Native, Aio, Basic };

// Local enum inputs need the same bounded domain as parsed packet names
constexpr bool IsKnownMenuDesign(MenuDesign design) noexcept {
    switch (design) {
    case MenuDesign::Apparition:
    case MenuDesign::Classic:
    case MenuDesign::Native:
    case MenuDesign::Aio:
    case MenuDesign::Basic:
        return true;
    default:
        return false;
    }
}
} // namespace iw::overlay
