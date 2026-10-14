#pragma once
#include <array>
#include <cstddef>
#include <cstdint>
#include <optional>
#include "../shared/MenuDesign.h"
#include "../shared/MenuOpening.h"
#include "../shared/paint/MenuBooleanPaint.h"
#include "../shared/MenuRowCapacity.h"
#include "../shared/scroll/SpacerScrollReceipt.h"

namespace iw::overlay {
inline constexpr std::size_t MaximumScrollRows = MaximumMenuRowsShown + 1;
// Direct fields avoid a hash collision carrying motion into a different layout
struct ScrollLayout {
    float viewportWidth = 0, viewportHeight = 0, bodyWidth = 0, bodyHeight = 0;
    std::size_t capacity = 0;
    unsigned booleanDisplay = 0;
    std::uint32_t fixedAccent = 0;
    bool boxesLeft = false, coloredCursor = true, spotlightCursor = true, largeCursor = false, rainbow = false;
    // Origin changes cannot carry an old cursor path into a moved panel
    float bodyX = 0, bodyY = 0;
    std::int32_t menuX = 0, menuY = 85;
    // Equal body sizes do not make two menu designs the same presentation context
    MenuDesign design = MenuDesign::Apparition;
    bool operator==(const ScrollLayout&) const = default;
};
struct ScrollRequest {
    bool active = false;
    std::optional<std::uint64_t> menuRevision, focusRevision;
    std::size_t rowCount = 0, first = 0, shown = 0, selected = 0;
    int durationMilliseconds = 120;
    ScrollLayout layout;
    std::optional<MenuOpening> opening;
    // The receipt is borrowed only during Update; stored frames retain bounded identities
    const SpacerScrollReceipt* spacerReceipt = nullptr;
    std::optional<std::uint64_t> navigationRevision;
    SpacerScrollTopology topology;
    // Policy identity is copied so an older render cannot prove a newer dispatch
    std::optional<OpeningPolicyIdentity> openingPolicyIdentity;
    // Actor admission and family cannot be borrowed from another rendered source
    std::optional<LocalMenuEligibility> localMenuEligibility;
    MenuSurface menuSurface = MenuSurface::Main;
    // A frame cannot borrow a different callback owner or phase
    std::optional<MenuOptionCallback> menuOptionCallback = std::nullopt;
    // Cached display identity never replaces the current feature rows
    std::optional<MenuBooleanPaint> menuBooleanPaint = std::nullopt;
};
struct ScrollRow {
    std::size_t index = 0;
    float position = 0, selection = 0, visibility = 1;
    bool outgoing = false;
    // Explicit alpha overrides the legacy selection and visibility product
    std::optional<float> opacity = std::nullopt;
    // IW capability decoration preserves its own legacy opacity endpoint
    std::optional<float> decorationVisibility = std::nullopt;
};
// Only row identities and presentation weights are retained, never row text or feature values
struct ScrollFrame {
    std::array<ScrollRow, MaximumScrollRows> rows{};
    std::size_t count = 0, rowCount = 0, first = 0, shown = 0, selected = 0;
    float scroller = 0;
    bool transitioning = false;
    MenuDesign design = MenuDesign::Apparition;
    std::optional<std::uint64_t> menuRevision, focusRevision;
    std::optional<MenuOpening> opening;
    std::optional<double> openingElapsed;
    // Supplied frames cannot carry weights into a different viewport or panel origin
    ScrollLayout layout;
    // Private evidence separates counter paint from confirmed action selection
    std::optional<std::uint64_t> spacerSequence, spacerElapsed;
    std::optional<std::size_t> counterSelection;
    // Receipt frames belong to the exact policy observed before dispatch
    std::optional<OpeningPolicyIdentity> spacerPolicyIdentity;
    // Every supplied frame echoes its own copied admission context
    std::optional<LocalMenuEligibility> localMenuEligibility;
    MenuSurface menuSurface = MenuSurface::Main;
    // A frame cannot borrow a different callback owner or phase
    std::optional<MenuOptionCallback> menuOptionCallback = std::nullopt;
    // Cached display identity never replaces the current feature rows
    std::optional<MenuBooleanPaint> menuBooleanPaint = std::nullopt;
};
ScrollFrame StaticScrollFrame(const ScrollRequest& request) noexcept;
}
