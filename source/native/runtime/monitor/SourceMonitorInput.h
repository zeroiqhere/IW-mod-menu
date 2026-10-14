#pragma once
#include "../../shared/QuickMenu/LocalMenuEligibility.h"
#include "MonitorContinuationObservation.h"
#include <optional>

namespace iw::overlay::runtime {
// Direct router calls follow the same ordinary visit bounds as parsed packets
void ValidateMonitorObservation(
    const MonitorContinuationObservation *observation,
    const MenuMonitorWait *wait, const LocalMenuEligibility *eligibility,
    bool open, bool ordinaryVisit);

// Only a written mechanical command and its same producer receipt can resume
// levels
class SourceMonitorInput {
public:
  void Reset();
  void Cancel();
  void Observe(const MonitorContinuationObservation *observation,
               const MenuMonitorWait *wait,
               const LocalMenuEligibility *eligibility,
               std::uint64_t navigation,
               std::optional<std::uint64_t> menuRevision, MenuSurface surface,
               bool open, bool root, bool ordinaryVisit, bool pending,
               bool blocked, std::uint64_t now);
  void Begin(const MonitorContinuationObservation *observation,
             const MenuMonitorWait *wait,
             const LocalMenuEligibility *eligibility, std::uint64_t navigation,
             std::optional<std::uint64_t> menuRevision, MenuSurface surface,
             bool open, bool root, bool back);
  void CaptureDispatch(std::uint64_t sequence);
  bool Ready() const noexcept { return ready_; }
  // Lost causal proof retires inherited levels instead of falling back to a
  // local timer
  bool Retired() const noexcept { return retired_; }

private:
  struct Scope {
    std::uint64_t navigation, menuRevision, admission;
    MenuSurface surface;
    bool open, root;
    PlayerLifeState life;
    bool operator==(const Scope &) const = default;
  };
  struct Ticket {
    Scope origin;
    std::uint64_t baseAck, sequence, waitFloor;
    bool back;
    std::optional<MenuMonitorContinuation> receipt;
    std::optional<Scope> result;
  };
  std::optional<Ticket> ticket_;
  std::optional<std::uint64_t> lastNow_;
  std::uint64_t waitHighWater_ = 0;
  bool ready_ = false;
  bool retired_ = false;
};
} // namespace iw::overlay::runtime
