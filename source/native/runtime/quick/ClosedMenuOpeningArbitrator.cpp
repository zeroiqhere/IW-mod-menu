#include "ClosedMenuOpeningArbitrator.h"
#include "../OpeningActionBindings.h"
#include <algorithm>
#include <array>
#include <stdexcept>
#include <utility>

namespace iw::overlay::runtime::quick {
namespace {
bool KnownLife(PlayerLifeState life) noexcept {
    return life == PlayerLifeState::Unknown || life == PlayerLifeState::Alive ||
           life == PlayerLifeState::Dead;
}
void ValidatePolicies(const Bindings& bindings, const ClosedMenuOpeningPolicies& policies,
                      bool configuredQuickAvailable) {
    if (!IsValidClosedMenuOpeningPolicies(policies))
        throw std::invalid_argument("Opening policies require supported positive revisions and tuples");
    // Both owners are explicit; an unavailable Quick action never becomes a guessed default
    ValidateMainOpeningBindings(bindings, policies.main);
    if (configuredQuickAvailable)
        ValidateMainOpeningBindings(bindings, policies.quick);
}
bool GroupHeld(const std::vector<int>& group, const std::array<bool, 256>& down) noexcept {
    return std::any_of(group.begin(), group.end(), [&](int key) { return down[key]; });
}
bool ChordHeld(const Bindings& bindings, const MainOpeningControls& policy,
               const std::array<bool, 256>& down) {
    return std::all_of(policy.actions.begin(), policy.actions.end(), [&](OpeningAction action) {
        return GroupHeld(OpeningActionKeys(bindings, action), down);
    });
}
} // namespace
ClosedMenuOpeningArbitrator::ClosedMenuOpeningArbitrator(Bindings bindings) : bindings_(std::move(bindings)) {
    // Reuse production alias and key bounds before collecting the immutable saved groups
    ValidateBindings(bindings_);
    std::array<bool, 256> present{};
    for (unsigned id = 0; id < OpeningActionCount; ++id) {
        for (int key : OpeningActionKeys(bindings_, static_cast<OpeningAction>(id))) {
            if (!present[key]) {
                keys_.push_back(key);
                present[key] = true;
            }
        }
    }
}
std::optional<ClosedMenuOpeningIntent>
ClosedMenuOpeningArbitrator::Poll(const ClosedMenuOpeningContext& context,
                                  const ClosedMenuOpeningPolicies& policies,
                                  const std::function<bool(int)>& isDown, bool configuredQuickAvailable,
                                  ClosedOpeningPolling polling) {
    try {
        // Reject malformed facts and policies before any external key callback can run
        if (!IsValidClosedOpeningPolling(polling))
            throw std::invalid_argument("Unsupported closed opening polling mode");
        if (!KnownLife(context.eligibility.life))
            throw std::invalid_argument("Unknown player life fact");
        ValidatePolicies(bindings_, policies, configuredQuickAvailable);
        if (polling == ClosedOpeningPolling::SourceLevels && !context.foregroundAllowed) {
            // Background sampling cannot read global keys or carry a complete chord back into focus
            Reset();
            return std::nullopt;
        }
        std::array<bool, 256> down{};
        for (int key : keys_)
            down[key] = isDown(key);
        // Sample all aggregates before choosing priority or returning for a blocked context
        const bool mainHeld = ChordHeld(bindings_, policies.main, down);
        const bool quickHeld = configuredQuickAvailable && ChordHeld(bindings_, policies.quick, down);
        const bool deadHeld = GroupHeld(bindings_.aim, down) && GroupHeld(bindings_.jump, down);
        const bool changed = !previousContext_ || *previousContext_ != context || !previousPolicies_ ||
                             *previousPolicies_ != policies ||
                             previousConfiguredQuickAvailable_ != configuredQuickAvailable;
        const bool mainFresh = mainHeld && !mainHeld_;
        const bool quickFresh = quickHeld && !quickHeld_;
        const bool deadFresh = deadHeld && !deadHeld_;
        mainHeld_ = mainHeld;
        quickHeld_ = quickHeld;
        deadHeld_ = deadHeld;
        // Legacy sampling seeds complete holds while source sampling checks current permitted levels
        if (changed) {
            previousContext_ = context;
            previousPolicies_ = policies;
            previousConfiguredQuickAvailable_ = configuredQuickAvailable;
            if (polling == ClosedOpeningPolling::FreshEdges)
                return std::nullopt;
        }
        if (polling == ClosedOpeningPolling::SourceLevels && sourceReleaseRequired_) {
            // A context refresh keeps a host loss barrier until no complete opening chord is held
            if (!mainHeld && !quickHeld && !deadHeld)
                sourceReleaseRequired_ = false;
            return std::nullopt;
        }
        if (!context.foregroundAllowed || context.menuOpen || context.commandPending || context.blocked ||
            !context.eligibility.hasMenu || context.eligibility.controlsDisabled)
            return std::nullopt;
        if (context.eligibility.life == PlayerLifeState::Alive) {
            // Held Main keeps source priority even when its one native edge was already consumed
            if (mainHeld)
                return (polling == ClosedOpeningPolling::SourceLevels || mainFresh)
                    ? std::optional{ClosedMenuOpeningIntent::Main} : std::nullopt;
            if (configuredQuickAvailable && !policies.quickMenuDisabled &&
                (polling == ClosedOpeningPolling::SourceLevels ? quickHeld : quickFresh))
                return ClosedMenuOpeningIntent::Quick;
        } else if (context.eligibility.life == PlayerLifeState::Dead && !policies.quickMenuDisabled &&
                   (polling == ClosedOpeningPolling::SourceLevels ? deadHeld : deadFresh)) {
            // Dead opening uses fixed saved Aim and Jump, independent of either configured tuple
            return ClosedMenuOpeningIntent::Quick;
        }
        // Unknown player life cannot be interpreted as living or dead
        return std::nullopt;
    } catch (...) {
        // Invalid metadata, failed key reads and failed copies cannot leave a deferred completion
        Reset();
        throw;
    }
}
void ClosedMenuOpeningArbitrator::Reset(bool consumeSourceHolds) noexcept {
    previousContext_.reset();
    previousPolicies_.reset();
    mainHeld_ = quickHeld_ = deadHeld_ = false;
    previousConfiguredQuickAvailable_ = true;
    // Refreshing current facts cannot undo a prior loss of foreground or valid metadata
    sourceReleaseRequired_ = sourceReleaseRequired_ || consumeSourceHolds;
}
} // namespace iw::overlay::runtime::quick
