#include "SourceMonitorInput.h"
#include <algorithm>
#include <stdexcept>

namespace iw::overlay::runtime {
namespace {
bool HasOrdinaryOwner(const LocalMenuEligibility *eligibility) {
  return eligibility && IsValidLocalMenuEligibility(*eligibility) &&
         eligibility->hasMenu == true &&
         eligibility->controlsDisabled == false &&
         (eligibility->life == PlayerLifeState::Alive ||
          eligibility->life == PlayerLifeState::Dead);
}
} // namespace
void ValidateMonitorObservation(
    const MonitorContinuationObservation *observation,
    const MenuMonitorWait *wait, const LocalMenuEligibility *eligibility,
    bool open, bool ordinaryVisit) {
  if (!observation || !observation->continuation)
    return;
  const auto &receipt = *observation->continuation;
  // The current ordinary visit bounds copied proof before any keys are sampled
  if (!IsValidMenuMonitorContinuation(receipt) ||
      receipt.sequence > observation->acknowledgedSequence || !ordinaryVisit ||
      !HasOrdinaryOwner(eligibility) ||
      eligibility->revision != receipt.admissionRevision ||
      (!open && receipt.kind != MenuMonitorWaitKind::Back) ||
      (wait &&
       (wait->revision != receipt.revision || wait->kind != receipt.kind))) {
    throw std::invalid_argument("Unsupported causal mechanical continuation");
  }
}
void SourceMonitorInput::Cancel() {
  // Consuming a branch keeps the observed revision floor for its next
  // invocation
  ticket_.reset();
  ready_ = false;
}
void SourceMonitorInput::Reset() {
  Cancel();
  lastNow_.reset();
  waitHighWater_ = 0;
  retired_ = false;
}
void SourceMonitorInput::Observe(
    const MonitorContinuationObservation *observation,
    const MenuMonitorWait *wait, const LocalMenuEligibility *eligibility,
    std::uint64_t navigation, std::optional<std::uint64_t> menuRevision,
    MenuSurface surface, bool open, bool root, bool ordinaryVisit, bool pending,
    bool blocked, std::uint64_t now) {
  ready_ = false;
  retired_ = false;
  const auto retire = [&]() {
    // Lost proof must consume inherited levels, including an armed legacy
    // choice timer
    bool owned = ticket_.has_value();
    Cancel();
    retired_ = owned;
  };
  if (lastNow_ && now < *lastNow_) {
    retire();
    lastNow_ = now;
    return;
  }
  lastNow_ = now;
  if (wait)
    waitHighWater_ = std::max(waitHighWater_, wait->revision);
  if (observation && observation->continuation)
    waitHighWater_ =
        std::max(waitHighWater_, observation->continuation->revision);
  if (!ticket_)
    return;
  if (!observation || !menuRevision || !ordinaryVisit ||
      !HasOrdinaryOwner(eligibility)) {
    retire();
    return;
  }
  const Scope current{navigation, *menuRevision, eligibility->revision, surface,
                      open,       root,          eligibility->life};
  if (!ticket_->sequence || current.admission != ticket_->origin.admission ||
      current.surface != ticket_->origin.surface ||
      current.life != ticket_->origin.life) {
    retire();
    return;
  }
  if (observation->acknowledgedSequence < ticket_->sequence) {
    // An unacknowledged command cannot explain a changed menu result
    if (current != ticket_->origin)
      retire();
    return;
  }
  if (observation->acknowledgedSequence != ticket_->sequence) {
    retire();
    return;
  }
  const auto *receipt = observation->continuation;
  if (!ticket_->receipt) {
    if (!receipt || receipt->sequence != ticket_->sequence ||
        receipt->admissionRevision != current.admission ||
        receipt->revision <= ticket_->waitFloor ||
        (receipt->kind == MenuMonitorWaitKind::Back) != ticket_->back ||
        (!ticket_->back && current != ticket_->origin)) {
      retire();
      return;
    }
    // The exact producer receipt attests only this command's actual resulting
    // parent or close
    ticket_->receipt = *receipt;
    ticket_->result = current;
  } else if (!receipt || *receipt != *ticket_->receipt ||
             current != *ticket_->result) {
    retire();
    return;
  }
  // Closed Back remains observable without granting ordinary or opening
  // permission
  ready_ = open && !wait && !pending && !blocked;
}
void SourceMonitorInput::Begin(
    const MonitorContinuationObservation *observation,
    const MenuMonitorWait *wait, const LocalMenuEligibility *eligibility,
    std::uint64_t navigation, std::optional<std::uint64_t> menuRevision,
    MenuSurface surface, bool open, bool root, bool back) {
  Cancel();
  if (!observation || wait || !menuRevision || !open ||
      !HasOrdinaryOwner(eligibility) || eligibility->blocked)
    return;
  // The original public visit epochs are written with the command before the
  // result may change
  ticket_ = Ticket{{navigation, *menuRevision, eligibility->revision, surface,
                    open, root, eligibility->life},
                   observation->acknowledgedSequence,
                   0,
                   waitHighWater_,
                   back,
                   std::nullopt,
                   std::nullopt};
}
void SourceMonitorInput::CaptureDispatch(std::uint64_t sequence) {
  if (!sequence)
    throw std::invalid_argument(
        "A written mechanical command requires a positive sequence");
  if (!ticket_)
    return;
  if (ticket_->sequence || sequence <= ticket_->baseAck) {
    Cancel();
    throw std::invalid_argument(
        "A mechanical write must advance its original acknowledgment");
  }
  // A failed file write never reaches this capture boundary
  ticket_->sequence = sequence;
}
} // namespace iw::overlay::runtime
