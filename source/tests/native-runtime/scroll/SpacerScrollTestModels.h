#pragma once
#include "../TestSupport.h"
#include "scroll/SpacerScrollReceiptTracker.h"
namespace iw::overlay::tests::spacer {
inline MenuSnapshot Source(bool stealth = false, bool counter = false, std::size_t selected = 1) {
    MenuSnapshot model;
    model.isOpen = true;
    model.isRootMenu = false;
    model.navigationRevision = 10;
    model.menuRevision = 6;
    model.presentationFocusRevision = 8;
    model.selectedIndex = selected;
    model.appearance.optionCounter = counter;
    model.appearance.stealthUI = stealth;
    if (stealth) {
        model.appearance.booleanDisplay = "Text";
    }
    // The private models contain no game state and preserve the production spacer index
    for (int row = 0; row < 16; ++row) {
        model.rows.push_back({"Ordinary row", "OFF", true});
    }
    model.rows[2] = {"", "", false, true};
    return model;
}
inline MenuSnapshot Target(const MenuSnapshot &source, std::size_t selected) {
    auto target = source;
    target.selectedIndex = selected;
    ++target.navigationRevision;
    return target;
}
} // namespace iw::overlay::tests::spacer
