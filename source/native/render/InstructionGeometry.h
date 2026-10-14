#pragma once
#include "layout/ApparitionLayout.h"
#include "../shared/MenuSnapshot.h"

namespace iw::overlay {
// Geometry remains separate from the footer's captured Design and paint
struct InstructionGeometry {
    layout::MenuHudPresence presence = layout::MenuHudPresence::Present;
    std::optional<MenuDesign> design = std::nullopt;
};

InstructionGeometry ResolveInstructionGeometry(const MenuSnapshot& snapshot) noexcept;
} // namespace iw::overlay
