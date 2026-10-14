#pragma once
#include "../../../native/shared/MenuSnapshot.h"

namespace iw::overlay::tests::opening {
struct Expected {
    const char* file;
    MenuSurface surface;
    bool open;
    PlayerLifeState life;
    bool controlsDisabled, blocked;
    std::uint64_t admission, navigation, menu, sequence;
    std::size_t selected, rows;
    std::optional<MenuOpenTransitionPhase> transition;
    std::optional<MenuOpening> opening;
    bool hasActorEligibility = true;
};
// These source-backed controller intents are independent from parsed fixture output
inline const Expected Profiles[] = {
    {"opening-closed.json", MenuSurface::Main, false, PlayerLifeState::Alive, false, false, 10, 0, 1, 0, 0, 8, {}, {}},
    {"opening-pending.json", MenuSurface::Main, false, PlayerLifeState::Alive, false, true, 10, 0, 1, 1, 0, 8, {}, {}},
    {"opening-quick-drawn.json", MenuSurface::Quick, true, PlayerLifeState::Alive, true, false, 10, 1, 3, 1, 0, 14, MenuOpenTransitionPhase::Cooldown, MenuOpening{1, 0}},
    {"opening-quick-wait.json", MenuSurface::Quick, true, PlayerLifeState::Alive, true, false, 10, 1, 3, 1, 0, 14, MenuOpenTransitionPhase::Cooldown, MenuOpening{1, 549}},
    {"opening-quick-ready.json", MenuSurface::Quick, true, PlayerLifeState::Alive, false, false, 10, 1, 3, 1, 0, 14, {}, MenuOpening{1, 550}},
    {"opening-quick-scrolled.json", MenuSurface::Quick, true, PlayerLifeState::Alive, false, false, 10, 2, 3, 2, 1, 14, {}, {}},
    {"opening-quick-dead.json", MenuSurface::Quick, true, PlayerLifeState::Dead, false, false, 11, 2, 4, 2, 1, 3, {}, {}},
    {"opening-quick-dead-closed.json", MenuSurface::Quick, false, PlayerLifeState::Dead, false, false, 11, 3, 5, 3, 1, 3, {}, {}},
    {"opening-quick-dead-reopened.json", MenuSurface::Quick, true, PlayerLifeState::Dead, true, false, 11, 4, 6, 4, 1, 3, MenuOpenTransitionPhase::Cooldown, MenuOpening{2, 0}},
    {"opening-main-rebuild.json", MenuSurface::Main, true, PlayerLifeState::Alive, true, false, 10, 1, 2, 1, 0, 8, MenuOpenTransitionPhase::Rebuild, {}},
    {"opening-main-dead-wait.json", MenuSurface::Main, true, PlayerLifeState::Dead, true, false, 11, 1, 2, 1, 0, 8, MenuOpenTransitionPhase::Cooldown, MenuOpening{1, 549}},
    {"opening-main-dead-closed.json", MenuSurface::Main, false, PlayerLifeState::Dead, false, false, 11, 2, 3, 1, 0, 8, {}, {}},
    {"opening-quick-owner-retired.json", MenuSurface::Quick, false, PlayerLifeState::Alive, false, false, 11, 2, 4, 1, 0, 14, {}, {}},
    {"opening-quick-focus-hidden.json", MenuSurface::Quick, false, PlayerLifeState::Alive, true, false, 10, 1, 3, 1, 0, 14, {}, {}},
    {"opening-cleanup-retry.json", MenuSurface::Main, true, PlayerLifeState::Unknown, false, false, 0, 2, 4, 1, 0, 8, {}, {}, false}
};
}
