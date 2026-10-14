#pragma once
#include "BasicScrollerHistory.h"
#include "../../shared/MenuSnapshot.h"

namespace iw::overlay {
// Closing graphics observes identity only and never samples a hidden widget color
inline void ObserveClosedBasicScroller(BasicScrollerHistory& history, const MenuSnapshot* snapshot) noexcept {
    const auto revision = snapshot && !snapshot->isOpen ? snapshot->menuChromeRevision : std::nullopt;
    history.Update(revision, false, std::nullopt);
}
}
