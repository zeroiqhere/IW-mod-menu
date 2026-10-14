#include "InstructionGeometry.h"

namespace iw::overlay {
InstructionGeometry ResolveInstructionGeometry(const MenuSnapshot& snapshot) noexcept {
    // Initial opening already has a committed Design and needs no target override
    if (snapshot.menuOpenTransition == MenuOpenTransitionPhase::Rebuild) {
        return {layout::MenuHudPresence::Absent, std::nullopt};
    }
    // Local layout rebuilds retain the captured Design and use the absent HUD footer anchor
    if (snapshot.menuPreferenceRebuild == MenuPreferenceRebuildPhase::Rebuild) {
        return {layout::MenuHudPresence::Absent, std::nullopt};
    }
    // A legacy rebuild has no proof of the target, so its captured geometry stays intact
    if (snapshot.menuDesignTransition == MenuDesignTransitionPhase::Rebuild && snapshot.menuDesignFooterDesign &&
        IsKnownMenuDesign(*snapshot.menuDesignFooterDesign)) {
        return {layout::MenuHudPresence::Absent, snapshot.menuDesignFooterDesign};
    }
    // Cooldown and settled menus never inherit an earlier gap descriptor
    return {};
}
} // namespace iw::overlay
