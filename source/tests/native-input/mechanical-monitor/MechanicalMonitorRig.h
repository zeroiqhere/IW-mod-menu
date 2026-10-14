#pragma once
#include "NativeInputRouter.h"
#include <set>
#include <stdexcept>

namespace iw::overlay::runtime::tests {
inline void RequireMonitor(bool value, const char *reason) {
  if (!value)
    throw std::runtime_error(reason);
}
struct MechanicalMonitorRig {
  Bindings bindings{{2},  {86}, {2, 38}, {1, 40}, {70}, {51}, {52}, {1},
                    {38}, {40}, {71},    {69},    {16}, {17}, {32}};
  NativeInputRouter router{bindings};
  std::set<int> held;
  bool focused = true, open = true, pending = false, quick = false,
       root = false, modern = true;
  std::uint64_t now = 0, navigation = 7, menu = 20, ack = 10;
  InputMode mode = InputMode::Menu;
  LocalMenuEligibility eligibility{4, PlayerLifeState::Alive, true, false,
                                   false};
  MenuSurface surface = MenuSurface::Main;
  std::optional<MenuMonitorContinuation> receipt;
  std::optional<MenuMonitorWait> wait;
  std::optional<Input> Poll() {
    UseMonitorObservation use{nullptr, nullptr, ack};
    MonitorContinuationObservation mechanical{receipt ? &*receipt : nullptr,
                                              ack};
    return router.Poll(
        focused, open, pending, [&](int key) { return held.contains(key); },
        now, navigation, quick, root, mode, 120, menu, nullptr, 0, 0, nullptr,
        &eligibility, surface, wait ? &*wait : nullptr, &use,
        modern ? &mechanical : nullptr);
  }
  void Choice(MenuMonitorWaitKind kind = MenuMonitorWaitKind::ChoiceNoop,
              bool previous = false, bool captured = true) {
    Poll();
    held = {previous ? 51 : 52};
    RequireMonitor(Poll() ==
                       (previous ? Input::PreviousChoice : Input::NextChoice),
                   "Original choice was not admitted");
    if (captured)
      router.CaptureMonitorDispatch(11);
    ack = 11;
    receipt = MenuMonitorContinuation{1, 11, 4, kind};
    wait = MenuMonitorWait{1, kind};
    RequireMonitor(!Poll(), "Mechanical wait did not pause its command");
  }
  void Back(bool captured = true) {
    Poll();
    held = {86};
    RequireMonitor(Poll() == Input::Back,
                   "Original child Back was not admitted");
    if (captured)
      router.CaptureMonitorDispatch(11);
    // The same accepted Back owns its actual resulting parent, without a nav+1
    // rule
    ack = 11;
    navigation = 18;
    menu = 32;
    receipt = MenuMonitorContinuation{1, 11, 4, MenuMonitorWaitKind::Back};
    wait = MenuMonitorWait{1, MenuMonitorWaitKind::Back};
    RequireMonitor(!Poll(), "Own Back parent escaped its caller wait");
  }
  void Ready(std::uint64_t timestamp) {
    wait.reset();
    now = timestamp;
  }
};
} // namespace iw::overlay::runtime::tests
