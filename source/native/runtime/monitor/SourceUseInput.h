#pragma once
#include "UseMonitorObservation.h"
#include "../../shared/QuickMenu/LocalMenuEligibility.h"
#include <optional>

namespace iw::overlay::runtime {
// Direct calls receive the same owner bounds as packets before any keys are sampled
void ValidateUseObservation(const UseMonitorObservation* observation, const LocalMenuEligibility* eligibility,
    bool open, bool ordinaryVisit, bool callbackOwnsInput, bool mechanicalWait);

// The input receipt follows one actual write and its original backend-owned action
class SourceUseInput {
public:
    void Reset();
    void Cancel();
    void Observe(const UseMonitorObservation* observation, const LocalMenuEligibility* eligibility,
        std::uint64_t navigation, MenuSurface surface, bool open, bool ordinaryVisit,
        bool pending, bool blocked, std::uint64_t now);
    void Begin(const UseMonitorObservation* observation, const LocalMenuEligibility* eligibility,
        std::uint64_t navigation, MenuSurface surface);
    void CaptureDispatch(std::uint64_t sequence);
    bool Ready() const noexcept { return ready_; }
private:
    struct Scope {
        std::uint64_t navigation, admission;
        MenuSurface surface;
        bool operator==(const Scope&) const = default;
    };
    struct Ticket {
        Scope scope;
        std::uint64_t baseAck = 0, sequence = 0, callbackFloor = 0;
        std::optional<MenuUseContinuation> receipt;
    };
    std::optional<Ticket> ticket_;
    std::optional<std::uint64_t> lastNow_;
    std::uint64_t callbackHighWater_ = 0;
    bool ready_ = false;
};
}
