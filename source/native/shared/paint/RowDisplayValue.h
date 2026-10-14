#pragma once
#include "../MenuSnapshot.h"
#include <string_view>

namespace iw::overlay {
inline bool IsBooleanRowValue(std::string_view value) noexcept {
    return value == "ON" || value == "OFF" || value == "UNKNOWN";
}
inline bool IsBooleanPaintContext(const MenuSnapshot& snapshot) noexcept {
    return snapshot.isOpen && IsKnownMenuSurface(snapshot.menuSurface) && snapshot.menuRevision
        && !snapshot.rows.empty() && snapshot.selectedIndex < snapshot.rows.size()
        && !snapshot.rows[snapshot.selectedIndex].isSpacer && !snapshot.widthEditor
        && !snapshot.instructionPositionEditor && !snapshot.menuPositionEditor && !snapshot.menuCredits
        && !snapshot.menuUnlock && !snapshot.menuPreferenceRefresh && !snapshot.menuDesignTransition
        && !snapshot.menuPreferenceRebuild && snapshot.menuOpenTransition != MenuOpenTransitionPhase::Rebuild;
}
inline bool AreBooleanPaintTargetsValid(const MenuSnapshot& snapshot, const MenuBooleanPaint& paint) noexcept {
    if (!IsValidMenuBooleanPaint(paint, snapshot.rows.size())) return false;
    for (std::size_t entry = 0; entry < paint.count; ++entry) {
        const auto& row = snapshot.rows[paint.rows[entry].index];
        if (row.isSpacer || !IsBooleanRowValue(row.value)) return false;
    }
    return true;
}
// The display seam cannot change the current row or its action admission
inline std::string_view ResolveRowDisplayValue(const MenuSnapshot& snapshot, std::size_t index) noexcept {
    if (index >= snapshot.rows.size()) return {};
    const auto& current = snapshot.rows[index].value;
    // A missing current observation always remains unknown, even with cached ON paint
    if (current == "UNKNOWN" || !snapshot.menuBooleanPaint || !IsBooleanPaintContext(snapshot)
        || !AreBooleanPaintTargetsValid(snapshot, *snapshot.menuBooleanPaint)) return current;
    for (std::size_t entry = 0; entry < snapshot.menuBooleanPaint->count; ++entry) {
        const auto& paint = snapshot.menuBooleanPaint->rows[entry];
        if (paint.index == index) return BooleanPaintText(paint.value);
    }
    return current;
}
}
