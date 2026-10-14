#pragma once
#include "../MenuOptionCallback.h"
#include "../paint/MenuBooleanPaint.h"
#include "../MenuAppearance.h"
#include "../OpenControls/MainOpeningControls.h"
#include "../OpenControls/OpeningPolicyIdentity.h"
#include "../QuickMenu/LocalMenuEligibility.h"
#include <array>
#include <cstddef>
#include <cstdint>
#include <optional>
#include <string>

namespace iw::overlay {
inline constexpr std::size_t MaximumSpacerScrollRows = 128;
enum class SpacerScrollDirection { Up, Down };
enum class SpacerScrollBranch { StableWindow, FinalWindowShift, InitialWindowShiftThenRebuild };

struct SpacerScrollTopology {
    std::uint32_t rowCount = 0;
    std::array<std::uint64_t, 2> spacerMask{};
    // Minus one means an ordinary row has no typed opening action
    std::array<std::int8_t, MaximumSpacerScrollRows> openingActions = [] {
        std::array<std::int8_t, MaximumSpacerScrollRows> result{};
        result.fill(-1);
        return result;
    }();
    bool operator==(const SpacerScrollTopology &) const = default;
};

struct SpacerScrollPlan {
    SpacerScrollDirection direction = SpacerScrollDirection::Down;
    SpacerScrollBranch branch = SpacerScrollBranch::StableWindow;
    std::uint32_t fromIndex = 0, spacerIndex = 0, targetIndex = 0, spacerCount = 0;
    std::uint32_t capacity = 0, sourceFirst = 0, spacerFirst = 0, targetFirst = 0;
    int durationMilliseconds = 120;
    SpacerScrollTopology topology;
    bool operator==(const SpacerScrollPlan &) const = default;
};

struct SpacerScrollReceipt {
    SpacerScrollPlan plan;
    std::uint64_t sequence = 0, sourceNavigationRevision = 0, targetNavigationRevision = 0;
    std::uint64_t menuRevision = 0, focusRevision = 0;
    // Dispatch, observation and rendering use the same native presentation clock
    std::uint64_t issuedMilliseconds = 0, observedMilliseconds = 0;
    MenuAppearance sourceAppearance;
    std::string sourceAccent = "#3998FE";
    bool sourceRainbow = false, sourceRootMenu = true;
    std::optional<MainOpeningControls> sourceOpeningControls = std::nullopt;
    // Policy history belongs to the same immutable source as the command capture
    std::optional<ClosedMenuOpeningPolicies> sourceOpeningPolicies = std::nullopt;
    // Family and local admission history are private copies from the dispatch source
    std::optional<LocalMenuEligibility> sourceLocalMenuEligibility = std::nullopt;
    MenuSurface sourceMenuSurface = MenuSurface::Main;
    // Callback waits retire private motion before another owner can reuse it
    std::optional<MenuOptionCallback> sourceMenuOptionCallback = std::nullopt;
    // A newer cached checkbox owner cannot reuse this dispatch history
    std::optional<MenuBooleanPaint> sourceMenuBooleanPaint = std::nullopt;
    bool operator==(const SpacerScrollReceipt &) const = default;
};
} // namespace iw::overlay
