#pragma once

namespace iw::overlay {
// Local refresh phases pause controls while ordinary rows remain visible
// Pending retains the prior display; cooldown presents the committed display
// No feature write or timer belongs to this portable state
// The backend owns the two phase boundaries
// Selector waits without a redraw, so its existing row opening may continue
enum class MenuPreferenceRefreshPhase { Pending, Cooldown, Selector };
}
