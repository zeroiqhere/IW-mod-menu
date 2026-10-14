#pragma once
#include "../closed-level-opening/ClosedLevelOpeningSupport.h"

namespace iw::overlay::runtime::tests::ordinarySource {
using closedLevel::Require;

struct RouterRig : closedLevel::RouterRig {
  bool quickExitEnabled = false;
  bool rootMenu = true;
  bool failRead = false;
  int animationMilliseconds = 120;
  unsigned skippedUp = 0;
  unsigned skippedDown = 0;
  MenuSurface surface = MenuSurface::Main;
  std::optional<MenuUseContinuation> useContinuation;
  std::optional<MenuOptionCallback> callback;

  std::optional<Input> Poll() {
    reads.fill(0);
    UseMonitorObservation use{useContinuation ? &*useContinuation : nullptr,
                              callback ? &*callback : nullptr, ack};
    MonitorContinuationObservation mechanical{receipt ? &*receipt : nullptr,
                                              ack};
    // One owned sample feeds every router predicate
    return router.Poll(
        foreground, open, pending,
        [&](int key) {
          ++reads[key];
          if (failRead)
            throw std::runtime_error("saved action reader failed");
          return held.contains(key);
        },
        now, navigation, quickExitEnabled, rootMenu, inputMode,
        animationMilliseconds, menu,
        hasPolicy ? &policies.main : nullptr, skippedUp, skippedDown,
        hasPolicy ? &policies : nullptr,
        hasAdmission ? &admission : nullptr, surface,
        wait ? &*wait : nullptr, &use, &mechanical, mode);
  }
};

void RegisterCurrentLevelChecks(void (*add)(const char *, void (*)()));
void RegisterTimingChecks(void (*add)(const char *, void (*)()));
void RegisterScrollChecks(void (*add)(const char *, void (*)()));
void RegisterBackendWaitChecks(void (*add)(const char *, void (*)()));
void RegisterRecoveryChecks(void (*add)(const char *, void (*)()));
void RegisterGestureChecks(void (*add)(const char *, void (*)()));
} // namespace iw::overlay::runtime::tests::ordinarySource
