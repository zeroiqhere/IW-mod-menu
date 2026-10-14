#include "InstructionHudColorHistory.h"
namespace iw::overlay {
namespace {
layout::Color Rgb(const InstructionRgb& rgb, std::uint8_t alpha=255) noexcept {
    return {rgb[0],rgb[1],rgb[2],alpha};
}
layout::Color CreationBackground(MenuDesign design) noexcept {
    if(design==MenuDesign::Classic) return {25,25,25,217};
    if(design==MenuDesign::Apparition) return {42,42,42,255};
    return {0,0,0,255};
}
}
std::optional<InstructionHudColors> InstructionHudColorHistory::Update(
    const MenuSnapshot& snapshot, bool admitted, std::optional<layout::Color> nativeSample, std::uint64_t nowMilliseconds) noexcept {
    // Lifetime admission rejects stale or mutated creations before either retained channel changes
    if(!admitted || !HasOwnedInstructionHud(snapshot)) return std::nullopt;
    const auto& hud=*snapshot.instructionHud;
    if(!hud.creationAccent || !hud.creationRainbow) return std::nullopt;
    const auto captured=ParseInstructionRgb(*hud.creationAccent),current=ParseInstructionRgb(snapshot.accent);
    if(!captured || !current) return std::nullopt;
    const auto revision=*hud.creationRevision;
    if(revision_ && revision<*revision_) return std::nullopt;
    const bool created=!revision_ || revision>*revision_;
    if(created) {
        // A Rainbow-born footer samples this native owner observation even after the mode has stopped
        if(*hud.creationRainbow && !nativeSample) return std::nullopt;
        const auto outline=*hud.creationRainbow ? layout::Color{nativeSample->red,nativeSample->green,nativeSample->blue,255}
            : Rgb(*captured);
        colors_=InstructionHudColors{outline,CreationBackground(*hud.creationDesign)};
        revision_=revision;
        themeTimeline_=InstructionThemeTimeline{};themeAuthority_=snapshot.instructionTheme.has_value();
        quarantined_=false;observedRainbowTarget_.reset();
        // Current accent is a baseline, not evidence of a callback that occurred before observation
        lastAccent_=current;
    }
    if(!colors_) return std::nullopt;
    // A supplied journal cannot silently disappear into the legacy scalar adapter
    if(themeAuthority_!=snapshot.instructionTheme.has_value()) quarantined_=true;
    if(quarantined_) return std::nullopt;
    // Captured targets belong to their invocation rather than the current Design
    if(snapshot.instructionTheme) {
        const auto& theme=*snapshot.instructionTheme;
        if(theme.creationRevision!=revision || (!*hud.creationBackground && !theme.events.empty()) || !IsValidInstructionThemeState(theme)) {
            quarantined_=true;return std::nullopt;
        }
        const auto replay=themeTimeline_.Update(theme,*colors_,observedRainbowTarget_,nowMilliseconds);
        if(!replay) return std::nullopt;
        colors_=replay;
    }
    if(!*hud.creationBackground) return colors_;
    const auto currentDesign=snapshot.menuDesignFooterDesign.value_or(snapshot.appearance.design);
    if(snapshot.smoothRainbowTheme && nativeSample) {
        // The loop targets current design while each existing widget keeps its creation alpha
        auto& target=currentDesign==MenuDesign::Aio ? colors_->background : colors_->outline;
        target.red=nativeSample->red;target.green=nativeSample->green;target.blue=nativeSample->blue;
        if(snapshot.instructionTheme) themeTimeline_.Override(currentDesign==MenuDesign::Aio
            ? InstructionThemeTarget::Background : InstructionThemeTarget::Outline,*nativeSample);
    } else if(!snapshot.instructionTheme && !snapshot.smoothRainbowTheme && !created && current!=lastAccent_) {
        // Observed endpoints route like the source callback without claiming its fade curve or event identity
        auto& target=currentDesign==MenuDesign::Aio ? colors_->background : colors_->outline;
        target.red=(*current)[0];target.green=(*current)[1];target.blue=(*current)[2];
    }
    lastAccent_=current;
    observedRainbowTarget_=snapshot.smoothRainbowTheme && nativeSample
        ? std::optional<InstructionThemeTarget>{currentDesign==MenuDesign::Aio
            ? InstructionThemeTarget::Background : InstructionThemeTarget::Outline} : std::nullopt;
    return colors_;
}
}
