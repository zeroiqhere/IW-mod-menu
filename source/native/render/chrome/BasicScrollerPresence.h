#pragma once
#include "../../shared/MenuSnapshot.h"
namespace iw::overlay {
// Soft locks hide rows or alpha but retain the source's existing scroller object
inline bool HasExistingBasicScroller(const MenuSnapshot& snapshot) noexcept {
    return snapshot.isOpen && snapshot.appearance.design == MenuDesign::Basic
        && snapshot.menuUnlock != MenuUnlockPhase::Rebuild
        && snapshot.menuDesignTransition != MenuDesignTransitionPhase::Rebuild
        && snapshot.menuOpenTransition != MenuOpenTransitionPhase::Rebuild
        && snapshot.menuPreferenceRebuild != MenuPreferenceRebuildPhase::Rebuild;
}
}
