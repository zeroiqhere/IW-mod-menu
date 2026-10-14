#pragma once

namespace iw::overlay {
// Normal opening creates the HUD before its separate monitor input wait ends
// Modal return uses MenuUnlockPhase and keeps its existing shorter lifetime
enum class MenuOpenTransitionPhase { Rebuild, Cooldown };
}
