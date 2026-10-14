#pragma once

#include "../InstructionHudLifetime.h"
#include "../color/InstructionHudColorHistory.h"
#include <utility>

namespace iw::overlay {

// Keep one immutable observation for the later paint callback
struct InstructionPaintObservation {
    bool lifetimeAdmitted = false;
    bool colorsAdmitted = false;
    std::optional<InstructionHudColors> colors;
};

template<class Prepare, class Paint>
bool RunInstructionRenderPhase(const MenuSnapshot& snapshot, InstructionHudLifetime& lifetime,
    InstructionHudColorHistory& history, std::optional<layout::Color> sample, std::uint64_t now,
    Prepare&& prepare, Paint&& paint) {
    const bool admitted = lifetime.Observe(&snapshot);
    // Footer colors belong to the source HUD even when graphics preparation fails
    const auto colors = history.Update(snapshot, admitted, sample, now);
    const InstructionPaintObservation observation{
        admitted, admitted && !history.IsQuarantined(), colors};
    // The copied observation serves one preparation attempt and one paint attempt
    if (!std::forward<Prepare>(prepare)(admitted)) return false;
    return std::forward<Paint>(paint)(observation);
}

}
