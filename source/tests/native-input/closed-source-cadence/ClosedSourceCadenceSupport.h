#pragma once
#include "../closed-level-opening/ClosedLevelOpeningSupport.h"
#include <limits>

namespace iw::overlay::runtime::tests::closedCadence {
// Reuse the same saved groups and typed caller facts as the opening regressions
using closedLevel::Require;
using closedLevel::RouterRig;
void RegisterTimingChecks(void (*add)(const char *, void (*)()));
void RegisterPauseChecks(void (*add)(const char *, void (*)()));
void RegisterRecoveryChecks(void (*add)(const char *, void (*)()));
}
