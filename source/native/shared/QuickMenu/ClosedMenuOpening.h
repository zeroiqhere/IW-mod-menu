#pragma once
#include "../OpenControls/MainOpeningControls.h"

namespace iw::overlay {
// Player life is an explicit caller fact, separate from the game process lifetime
// Unknown facts cannot authorize either menu opening
enum class PlayerLifeState { Unknown, Alive, Dead };
struct PlayerMenuEligibility {
    PlayerLifeState life = PlayerLifeState::Unknown;
    bool hasMenu = false;
    bool controlsDisabled = false;
    bool operator==(const PlayerMenuEligibility &) const = default;
};

// These facts describe the observed menu boundary, not a guessed engine state
struct ClosedMenuOpeningContext {
    PlayerMenuEligibility eligibility;
    bool foregroundAllowed = false;
    bool menuOpen = false;
    bool commandPending = false;
    bool blocked = false;
    bool operator==(const ClosedMenuOpeningContext &) const = default;
};
struct ClosedMenuOpeningPolicies {
    MainOpeningControls main{1, {OpeningAction::Aim, OpeningAction::Melee}};
    MainOpeningControls quick{1, {OpeningAction::Aim, OpeningAction::SecondaryOffhand}};
    bool quickMenuDisabled = false;
    // One owner revision identifies the complete dual-policy admission transaction
    std::uint64_t revision = 1;
    bool operator==(const ClosedMenuOpeningPolicies &) const = default;
};
// Shape validation is shared by wire parsing and private input preparation
inline bool IsValidClosedMenuOpeningPolicies(const ClosedMenuOpeningPolicies &policies) noexcept {
    return policies.revision != 0 && IsValidMainOpeningControls(policies.main) && IsValidMainOpeningControls(policies.quick);
}
// An intent is local input only and cannot perform a game or menu mutation
enum class ClosedMenuOpeningIntent { Main, Quick };
} // namespace iw::overlay
