#pragma once

namespace iw::overlay {
// Rebuild hides the panel; cooldown shows the new design with controls paused
// The backend owns the fifty and two-hundred-fifty millisecond boundaries
enum class MenuDesignTransitionPhase { Rebuild, Cooldown };
}
