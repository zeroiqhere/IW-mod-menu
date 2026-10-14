#include "SpacerScrollPlan.h"
#include "../../shared/MenuRowCapacity.h"
#include "../../shared/scroll/MenuScrollWindow.h"
#include <algorithm>

namespace iw::overlay::runtime::scroll {
namespace {
bool SpacerAt(const SpacerScrollTopology &topology, std::uint32_t index) noexcept {
    return index < topology.rowCount &&
           (topology.spacerMask[index / 64] & (std::uint64_t{1} << (index % 64)));
}
bool ValidTopology(const SpacerScrollTopology &topology) noexcept {
    if (topology.rowCount == 0 || topology.rowCount > MaximumSpacerScrollRows) {
        return false;
    }
    for (std::uint32_t index = 0; index < MaximumSpacerScrollRows; ++index) {
        const bool spacer = (topology.spacerMask[index / 64] & (std::uint64_t{1} << (index % 64))) != 0;
        const auto action = topology.openingActions[index];
        if (action < -1 || action >= static_cast<int>(OpeningActionCount) || (spacer && action != -1) ||
            (index >= topology.rowCount && (spacer || action != -1))) {
            return false;
        }
    }
    return true;
}
bool OrdinaryRoute(const MenuSnapshot &snapshot) noexcept {
    return snapshot.isOpen && !snapshot.widthEditor && !snapshot.instructionPositionEditor &&
           !snapshot.menuPositionEditor && !snapshot.menuCredits && !snapshot.menuUnlock &&
           !snapshot.menuPreferenceRefresh && !snapshot.menuDesignTransition &&
           !snapshot.menuOpenTransition && !snapshot.menuPreferenceRebuild &&
           !snapshot.menuDesignFooterDesign;
}
bool SupportedAppearance(const MenuAppearance &appearance) noexcept {
    return IsKnownMenuDesign(appearance.design) && appearance.menuWidth >= 200 &&
           appearance.menuWidth <= 500 &&
           (appearance.booleanDisplay == "Boxes" || appearance.booleanDisplay == "Text" ||
            appearance.booleanDisplay == "Text Color") &&
           (appearance.boolBoxLocation == "Left" || appearance.boolBoxLocation == "Right") &&
           !(appearance.alternateInstructions && appearance.adaptiveInstructions) &&
           !(appearance.stealthUI && (appearance.booleanDisplay == "Boxes" || appearance.optionCounter)) &&
           !((appearance.design == MenuDesign::Classic || appearance.design == MenuDesign::Native) &&
             appearance.coloredCursor) &&
           !((appearance.design == MenuDesign::Aio || appearance.design == MenuDesign::Basic) &&
             appearance.optionCounter);
}
std::optional<SpacerScrollPlan> Build(const SpacerScrollTopology &topology, std::uint32_t from,
                                      std::uint32_t capacity, int duration,
                                      SpacerScrollDirection direction) noexcept {
    if (!ValidTopology(topology) || from >= topology.rowCount || SpacerAt(topology, from) ||
        !(capacity == 5 || (capacity >= 8 && capacity <= 14)) || duration < 100 || duration > 250 ||
        duration % 10 ||
        (direction != SpacerScrollDirection::Up && direction != SpacerScrollDirection::Down)) {
        return std::nullopt;
    }
    SpacerScrollPlan plan;
    plan.topology = topology;
    plan.fromIndex = from;
    plan.capacity = capacity;
    plan.durationMilliseconds = duration;
    plan.direction = direction;
    plan.sourceFirst = static_cast<std::uint32_t>(*MenuScrollWindowFirst(topology.rowCount, capacity, from));
    const int step = direction == SpacerScrollDirection::Up ? -1 : 1;
    int index = static_cast<int>(from) + step;
    bool changedBlankWindow = false;
    // Nonwrapped paths retain the exact recursive order without assuming a final-distance shortcut
    while (index >= 0 && index < static_cast<int>(topology.rowCount) && SpacerAt(topology, index)) {
        const auto first = *MenuScrollWindowFirst(topology.rowCount, capacity, index);
        if (plan.spacerCount == 0) {
            plan.spacerIndex = static_cast<std::uint32_t>(index);
            plan.spacerFirst = static_cast<std::uint32_t>(first);
        }
        ++plan.spacerCount;
        changedBlankWindow = changedBlankWindow || first != plan.sourceFirst;
        index += step;
    }
    if (!plan.spacerCount || index < 0 || index >= static_cast<int>(topology.rowCount)) {
        return std::nullopt;
    }
    plan.targetIndex = static_cast<std::uint32_t>(index);
    plan.targetFirst = static_cast<std::uint32_t>(*MenuScrollWindowFirst(topology.rowCount, capacity, index));
    if (changedBlankWindow) {
        // A shifted invalid step rebuilds after one half-wait instead of recursively skipping again
        const auto distance = plan.spacerFirst > plan.sourceFirst ? plan.spacerFirst - plan.sourceFirst
                                                                  : plan.sourceFirst - plan.spacerFirst;
        if (plan.spacerCount != 1 || distance != 1) {
            return std::nullopt;
        }
        plan.branch = SpacerScrollBranch::InitialWindowShiftThenRebuild;
    } else {
        const auto distance = plan.targetFirst > plan.sourceFirst ? plan.targetFirst - plan.sourceFirst
                                                                  : plan.sourceFirst - plan.targetFirst;
        if (distance > 1) {
            return std::nullopt;
        }
        plan.branch = distance ? SpacerScrollBranch::FinalWindowShift : SpacerScrollBranch::StableWindow;
    }
    return plan;
}
} // namespace
std::optional<SpacerScrollTopology> CaptureTopology(const MenuSnapshot &snapshot) noexcept {
    if (snapshot.rows.empty() || snapshot.rows.size() > MaximumSpacerScrollRows) {
        return std::nullopt;
    }
    SpacerScrollTopology topology;
    topology.rowCount = static_cast<std::uint32_t>(snapshot.rows.size());
    for (std::uint32_t index = 0; index < topology.rowCount; ++index) {
        const auto &row = snapshot.rows[index];
        if (row.openingAction) {
            if (!IsKnownOpeningAction(*row.openingAction)) {
                return std::nullopt;
            }
            topology.openingActions[index] = static_cast<std::int8_t>(*row.openingAction);
        }
        if (row.isSpacer) {
            if (!row.label.empty() || !row.value.empty() || row.enabled || row.openingAction) {
                return std::nullopt;
            }
            topology.spacerMask[index / 64] |= std::uint64_t{1} << (index % 64);
        }
    }
    return topology;
}
std::optional<SpacerScrollPlan> PlanSpacerScroll(const MenuSnapshot &snapshot,
                                                 SpacerScrollDirection direction) noexcept {
    if (!OrdinaryRoute(snapshot) || !snapshot.menuRevision || !snapshot.presentationFocusRevision ||
        !SupportedAppearance(snapshot.appearance) || snapshot.selectedIndex >= snapshot.rows.size()) {
        return std::nullopt;
    }
    const auto topology = CaptureTopology(snapshot);
    if (!topology) {
        return std::nullopt;
    }
    return Build(*topology, static_cast<std::uint32_t>(snapshot.selectedIndex),
                 static_cast<std::uint32_t>(MenuRowCapacity(snapshot.appearance)),
                 snapshot.appearance.scrollAnimationMilliseconds, direction);
}
bool IsValidSpacerScrollPlan(const SpacerScrollPlan &plan) noexcept {
    const auto recomputed =
        Build(plan.topology, plan.fromIndex, plan.capacity, plan.durationMilliseconds, plan.direction);
    return recomputed && *recomputed == plan;
}
std::optional<std::uint64_t> SpacerScrollLifetime(const SpacerScrollPlan &plan) noexcept {
    if (!IsValidSpacerScrollPlan(plan)) {
        return std::nullopt;
    }
    const auto duration = static_cast<std::uint64_t>(plan.durationMilliseconds);
    return plan.branch == SpacerScrollBranch::InitialWindowShiftThenRebuild
               ? duration
               : duration + duration / 2 * plan.spacerCount;
}
} // namespace iw::overlay::runtime::scroll
