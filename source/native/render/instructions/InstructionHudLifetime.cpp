#include "InstructionHudLifetime.h"
namespace iw::overlay {
bool InstructionHudLifetime::Observe(const MenuSnapshot* snapshot) noexcept {
    // Withdrawal hides drawing but does not invent destruction during focus projection
    if (!snapshot || !HasOwnedInstructionHud(*snapshot)) return false;
    const auto& hud = *snapshot->instructionHud;
    const auto revision = *hud.creationRevision;
    const auto rgb=hud.creationAccent ? ParseInstructionRgb(*hud.creationAccent) : std::nullopt;
    if (highest_ && revision < *highest_) {
        quarantined_ = true;
        return false;
    }
    if (!highest_ || revision > *highest_) {
        // New creation replaces only the independent footer identity, never menu epochs
        highest_ = revision;
        design_ = hud.creationDesign;
        alternate_ = hud.creationAlternate;
        background_ = hud.creationBackground;
        creationRgb_=rgb;
        creationRainbow_=hud.creationRainbow;
        quarantined_ = false;
    } else if (design_ != hud.creationDesign || alternate_ != hud.creationAlternate || background_ != hud.creationBackground
        || creationRgb_ != rgb || creationRainbow_ != hud.creationRainbow) {
        // One creation cannot quietly change its owned widget styles
        quarantined_ = true;
    }
    return !quarantined_;
}
}
