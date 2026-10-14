#pragma once
#include "NativeInputRouter.h"
#include "quick/ClosedMenuOpeningArbitrator.h"
#include <array>
#include <set>
#include <stdexcept>
#include <utility>

namespace iw::overlay::runtime::tests::closedLevel {
inline void Require(bool value, const char *reason) {
  if (!value)
    throw std::runtime_error(reason);
}
inline Bindings SavedBindings() {
  // These owned test groups describe explicit saved actions without desktop
  // input
  return {{2},  {86}, {2, 38}, {1, 40}, {70}, {51}, {52}, {1},
          {38}, {40}, {71},    {69},    {16}, {17}, {32}};
}
struct ArbitrationRig {
  quick::ClosedMenuOpeningArbitrator arbitrator{SavedBindings()};
  ClosedMenuOpeningContext context{
      {PlayerLifeState::Alive, true, false}, true, false, false, false};
  ClosedMenuOpeningPolicies policies;
  ClosedOpeningPolling mode = ClosedOpeningPolling::SourceLevels;
  std::set<int> held;
  std::optional<ClosedMenuOpeningIntent> Poll() {
    return arbitrator.Poll(
        context, policies, [&](int key) { return held.contains(key); }, true,
        mode);
  }
};
struct RouterRig {
  NativeInputRouter router;
  explicit RouterRig(Bindings bindings = SavedBindings())
      : router(std::move(bindings)) {}
  ClosedMenuOpeningPolicies policies;
  LocalMenuEligibility admission{4, PlayerLifeState::Alive, true, false, false};
  ClosedOpeningPolling mode = ClosedOpeningPolling::SourceLevels;
  InputMode inputMode = InputMode::Menu;
  std::set<int> held;
  std::array<unsigned, 256> reads{};
  bool foreground = true, open = false, pending = false, hasAdmission = true,
       hasPolicy = true;
  std::uint64_t now = 0, navigation = 7, menu = 20, ack = 10;
  std::optional<MenuMonitorWait> wait;
  std::optional<MenuMonitorContinuation> receipt;
  std::optional<Input> Poll() {
    reads.fill(0);
    UseMonitorObservation use{nullptr, nullptr, ack};
    MonitorContinuationObservation mechanical{receipt ? &*receipt : nullptr,
                                              ack};
    // Every physical key is read once, while later arbitration uses the same
    // cached sample
    return router.Poll(
        foreground, open, pending,
        [&](int key) {
          ++reads[key];
          return held.contains(key);
        },
        now, navigation, false, true, inputMode, 120, menu,
        hasPolicy ? &policies.main : nullptr, 0, 0,
        hasPolicy ? &policies : nullptr, hasAdmission ? &admission : nullptr,
        MenuSurface::Main, wait ? &*wait : nullptr, &use, &mechanical, mode);
  }
};
void RegisterArbitrationChecks(void (*add)(const char *, void (*)()));
void RegisterRouterChecks(void (*add)(const char *, void (*)()));
} // namespace iw::overlay::runtime::tests::closedLevel
