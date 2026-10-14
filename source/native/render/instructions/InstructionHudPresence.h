#pragma once
#include "../../shared/MenuSnapshot.h"
#include "../../shared/Instructions/InstructionCreationColor.h"
namespace iw::overlay {
inline bool HasOwnedInstructionHud(const MenuSnapshot& snapshot) noexcept {
    const auto& hud = snapshot.instructionHud;
    const auto& admission = snapshot.localMenuEligibility;
    // Known copied permission and life admit only this exact observed footer scope
    return hud && hud->exists && hud->creationRevision && *hud->creationRevision != 0
        && hud->admissionRevision && *hud->admissionRevision != 0 && hud->creationDesign
        && IsKnownMenuDesign(*hud->creationDesign) && hud->creationAlternate && hud->creationBackground
        && admission && admission->revision == *hud->admissionRevision && admission->hasMenu == true
        && (admission->life == PlayerLifeState::Alive || admission->life == PlayerLifeState::Dead)
        && snapshot.appearance.instructionsVisible
        && ((!hud->creationAccent && !hud->creationRainbow)
            || (hud->creationAccent && hud->creationRainbow && ParseInstructionRgb(*hud->creationAccent)));
}
inline bool HasDrawableOverlayContent(const MenuSnapshot& snapshot) noexcept {
    // Closed drawing is footer-only; legacy packets retain their old closed behavior
    return snapshot.isOpen || HasOwnedInstructionHud(snapshot);
}
}
