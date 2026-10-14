#pragma once
#include "../../../../native/shared/MenuOptionCallback.h"
#include "../../../../native/shared/MenuOpenTransition.h"
#include <array>
#include <optional>

namespace iw::overlay::tests::booleanPaint {
struct Expected {
    const char* file;
    const char* current;
    const char* cached;
    const char* display;
    std::uint64_t paintRevision;
    std::optional<MenuOptionCallbackPhase> callback;
    bool openingCooldown;
    bool open;
    bool controlsDisabled;
    bool blocked;
    bool enabled;
    std::size_t selected;
    std::uint64_t navigation;
    std::uint64_t menu;
    std::uint64_t sequence;
};
// The controller graph defines these expectations independently from exported bytes
inline const std::array<Expected, 8> Profiles{{
    {"quick-open.json", "OFF", "OFF", "OFF", 1, {}, true, true, true, false, false, 0, 1, 3, 1},
    {"boolean179-current-on.json", "ON", "OFF", "OFF", 1, MenuOptionCallbackPhase::Pending, false, true, true, false, false, 0, 1, 3, 2},
    {"boolean180-current-on.json", "ON", "ON", "ON", 2, MenuOptionCallbackPhase::Cooldown, false, true, true, false, false, 0, 1, 4, 2},
    {"late180-busy.json", "UNKNOWN", "UNKNOWN", "UNKNOWN", 2, MenuOptionCallbackPhase::Cooldown, false, true, true, true, false, 0, 1, 4, 2},
    {"late430-complete.json", "ON", "UNKNOWN", "UNKNOWN", 2, {}, false, true, false, false, true, 0, 1, 4, 2},
    {"late-scroll.json", "ON", "UNKNOWN", "UNKNOWN", 2, {}, false, true, false, false, true, 1, 2, 4, 3},
    {"late-reopened.json", "ON", "ON", "ON", 3, {}, true, true, true, false, false, 1, 4, 6, 5},
    {"focus-hidden179.json", "ON", nullptr, "ON", 0, {}, false, false, true, false, false, 0, 1, 3, 2}
}};
// The whole-cohort graph uses the same display contract at source row five
// Current UNKNOWN still wins over a retained OFF sample in the native display adapter
inline const std::array<Expected, 8> CohortProfiles{{
    {"cohort-off.json", "OFF", "OFF", "OFF", 1, {}, false, true, false, false, true, 0, 6, 4, 5},
    {"cohort-on179.json", "ON", "OFF", "OFF", 1, MenuOptionCallbackPhase::Pending, false, true, true, false, true, 5, 11, 4, 11},
    {"cohort-on180.json", "ON", "ON", "ON", 2, MenuOptionCallbackPhase::Cooldown, false, true, true, false, true, 5, 11, 5, 11},
    {"cohort-on430.json", "ON", "ON", "ON", 2, {}, false, true, false, false, true, 5, 11, 5, 11},
    {"cohort-unknown179.json", "UNKNOWN", "OFF", "UNKNOWN", 1, MenuOptionCallbackPhase::Pending, false, true, true, true, false, 5, 11, 4, 11},
    {"cohort-unknown180.json", "UNKNOWN", "UNKNOWN", "UNKNOWN", 2, MenuOptionCallbackPhase::Cooldown, false, true, true, true, false, 5, 11, 5, 11},
    {"cohort-complete430.json", "ON", "UNKNOWN", "UNKNOWN", 2, {}, false, true, false, false, true, 5, 11, 5, 11},
    {"cohort-worker-busy.json", "UNKNOWN", "OFF", "UNKNOWN", 1, {}, false, true, false, false, false, 0, 6, 4, 5}
}};

}
