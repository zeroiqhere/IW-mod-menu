#pragma once
#include "../../shared/MenuSnapshot.h"
#include "../../shared/scroll/SpacerScrollReceipt.h"
#include <optional>

namespace iw::overlay::runtime::scroll {
// Planning reads only bounded immutable menu data and never physical keys
std::optional<SpacerScrollTopology> CaptureTopology(const MenuSnapshot &snapshot) noexcept;
std::optional<SpacerScrollPlan> PlanSpacerScroll(const MenuSnapshot &snapshot,
                                                 SpacerScrollDirection direction) noexcept;
// Renderer callers independently recompute the path instead of trusting its branch label
bool IsValidSpacerScrollPlan(const SpacerScrollPlan &plan) noexcept;
// The event includes its half-wait and its final fade or detached outgoing ghost
std::optional<std::uint64_t> SpacerScrollLifetime(const SpacerScrollPlan &plan) noexcept;
} // namespace iw::overlay::runtime::scroll
