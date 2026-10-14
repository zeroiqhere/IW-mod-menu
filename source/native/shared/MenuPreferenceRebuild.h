#pragma once

namespace iw::overlay {
// Layout changes hide rows before redraw and pause controls through cooldown
// The backend owns timing while this value only describes the published view
enum class MenuPreferenceRebuildPhase { Rebuild, Cooldown };
} // namespace iw::overlay
