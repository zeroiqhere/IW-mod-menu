#include "MenuPositionEditorLayout.h"

namespace iw::overlay::layout {
MenuPositionTextLayout MakeMenuPositionTextLayout(const Hud& hud) noexcept {
    // Thirteen native units keep the seventh hint separate from the fixed offset anchor
    // The script background estimate does not establish the engine font line advance
    return {{hud.body.x + 4 * hud.scale, hud.body.y + 22 * hud.scale},
        {hud.body.x + 4 * hud.scale, hud.body.y + 113 * hud.scale}, 13 * hud.scale};
}
}
