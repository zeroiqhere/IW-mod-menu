#pragma once
namespace iw::overlay {
// IDs follow the source action inventory rather than physical key numbers
enum class OpeningAction {
    ActionSlot1 = 0,
    ActionSlot2,
    ActionSlot3,
    ActionSlot4,
    Melee,
    Aim,
    Attack,
    Sprint,
    Use,
    Frag,
    SecondaryOffhand,
    Stance,
    Jump
};
inline constexpr unsigned OpeningActionCount = 13;
constexpr bool IsKnownOpeningAction(OpeningAction action) noexcept {
    return static_cast<int>(action) >= 0 && static_cast<unsigned>(action) < OpeningActionCount;
}
// Canonical captions stay readable when no private saved key is available
constexpr const char *OpeningActionCaption(OpeningAction action) noexcept {
    constexpr const char *labels[] = {
        "Action Slot 1", "Action Slot 2", "Action Slot 3", "Action Slot 4",     "Melee",  "Aim", "Attack",
        "Sprint",        "Use",           "Frag",          "Secondary Offhand", "Stance", "Jump"};
    return IsKnownOpeningAction(action) ? labels[static_cast<unsigned>(action)] : nullptr;
}

} // namespace iw::overlay
