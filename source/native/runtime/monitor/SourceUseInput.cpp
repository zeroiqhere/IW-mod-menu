#include "SourceUseInput.h"
#include <algorithm>
#include <stdexcept>

namespace iw::overlay::runtime {
namespace {
bool HasSourceOwner(const LocalMenuEligibility* eligibility) {
    return eligibility && IsValidLocalMenuEligibility(*eligibility) && eligibility->hasMenu == true
        && (eligibility->life == PlayerLifeState::Alive || eligibility->life == PlayerLifeState::Dead);
}
}
void ValidateUseObservation(const UseMonitorObservation* observation, const LocalMenuEligibility* eligibility,
    bool open, bool ordinaryVisit, bool callbackOwnsInput, bool mechanicalWait) {
    if (!observation) return;
    const auto* callback = observation->callback;
    const auto* receipt = observation->continuation;
    // A callback can pause legacy input without granting a new causal continuation
    if (callback && (!IsValidMenuOptionCallback(*callback) || !open || !callbackOwnsInput)) {
        throw std::invalid_argument("Unsupported callback observation");
    }
    if (!receipt) return;
    if (!IsValidMenuUseContinuation(*receipt) || receipt->sequence > observation->acknowledgedSequence
        || !open || !ordinaryVisit || mechanicalWait || !HasSourceOwner(eligibility)
        || eligibility->revision != receipt->admissionRevision
        || callbackOwnsInput != (callback != nullptr)
        || (callback && (callback->revision != receipt->revision || callback->kind != receipt->kind))) {
        throw std::invalid_argument("Unsupported causal Use continuation");
    }
}
void SourceUseInput::Cancel() {
    // The observed high-water survives expiry and prevents an older callback from being borrowed
    ticket_.reset();
    ready_ = false;
}
void SourceUseInput::Reset() {
    Cancel();
    lastNow_.reset();
    callbackHighWater_ = 0;
}
void SourceUseInput::Observe(const UseMonitorObservation* observation,
    const LocalMenuEligibility* eligibility, std::uint64_t navigation, MenuSurface surface,
    bool open, bool ordinaryVisit, bool pending, bool blocked, std::uint64_t now) {
    ready_ = false;
    if (lastNow_ && now < *lastNow_) {
        // Native rollback consumes inherited input without estimating a backend deadline
        Cancel();
        lastNow_ = now;
        return;
    }
    lastNow_ = now;
    if (!observation || !open || !ordinaryVisit || !HasSourceOwner(eligibility)) {
        Cancel();
        return;
    }
    if (observation->callback) callbackHighWater_ = std::max(callbackHighWater_, observation->callback->revision);
    if (observation->continuation) callbackHighWater_ = std::max(callbackHighWater_, observation->continuation->revision);
    if (!ticket_) return;
    const Scope current{navigation, eligibility->revision, surface};
    if (current != ticket_->scope || ticket_->sequence == 0) {
        // A command not captured after its successful write has no causal receipt to continue
        Cancel();
        return;
    }
    if (observation->acknowledgedSequence < ticket_->sequence) return;
    if (observation->acknowledgedSequence != ticket_->sequence) {
        // Later commands cannot transfer the original held invocation to another action
        Cancel();
        return;
    }
    const auto* receipt = observation->continuation;
    if (!ticket_->receipt) {
        if (!receipt) {
            // Queued validation may publish Busy ACK before starting its actual callback
            if (!blocked || observation->callback) Cancel();
            return;
        }
        if (receipt->sequence != ticket_->sequence || receipt->admissionRevision != current.admission
            || receipt->revision <= ticket_->callbackFloor) {
            Cancel();
            return;
        }
        ticket_->receipt = *receipt;
    } else if (!receipt || *receipt != *ticket_->receipt) {
        // Cancellation or replacement cannot be mistaken for normal callback expiry
        Cancel();
        return;
    }
    // Absence means ready only while the same causal private-action receipt remains published
    ready_ = !observation->callback && !pending && !blocked;
}
void SourceUseInput::Begin(const UseMonitorObservation* observation,
    const LocalMenuEligibility* eligibility, std::uint64_t navigation, MenuSurface surface) {
    Cancel();
    if (!observation || observation->callback || !HasSourceOwner(eligibility)
        || eligibility->controlsDisabled != false || eligibility->blocked) return;
    // The callback counter belongs to the backend; no native clock or row value invents it
    ticket_ = Ticket{{navigation, eligibility->revision, surface}, observation->acknowledgedSequence,
        0, callbackHighWater_, std::nullopt};
}
void SourceUseInput::CaptureDispatch(std::uint64_t sequence) {
    if (!sequence) throw std::invalid_argument("A written Select requires a positive command sequence");
    if (!ticket_) return;
    if (ticket_->sequence || sequence <= ticket_->baseAck) {
        Cancel();
        throw std::invalid_argument("A Select write must advance its original acknowledgment");
    }
    // The worker calls this only after its command file write succeeds
    ticket_->sequence = sequence;
}
}
