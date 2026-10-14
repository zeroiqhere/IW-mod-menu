#include "ChromeColorHistory.h"
#include <algorithm>
#include <cmath>
namespace iw::overlay {
namespace {
bool Same(const ChromeThemeEvent& a, const ChromeThemeEvent& b) noexcept {
    return a.revision == b.revision && a.targetMask == b.targetMask && a.accent == b.accent
        && a.predecessorGapMilliseconds == b.predecessorGapMilliseconds && a.stoppedRainbow == b.stoppedRainbow;
}
bool Owned(const MenuSnapshot& snapshot, const ChromeThemeState& state) noexcept {
    const auto& admission = snapshot.localMenuEligibility;
    return snapshot.isOpen && snapshot.menuChromeRevision == state.creationRevision && admission
        && admission->revision == state.admissionRevision && admission->hasMenu == true
        && (admission->life == PlayerLifeState::Alive || admission->life == PlayerLifeState::Dead)
        && snapshot.appearance.design == state.creationDesign
        && snapshot.menuOpenTransition != MenuOpenTransitionPhase::Rebuild
        && snapshot.menuDesignTransition != MenuDesignTransitionPhase::Rebuild
        && snapshot.menuPreferenceRebuild != MenuPreferenceRebuildPhase::Rebuild;
}
}
ChromeColorHistory::Rgb ChromeColorHistory::At(const Channel& channel, std::uint64_t age) noexcept {
    if (!channel.active) return channel.held;
    if (age >= 500) return channel.target;
    if (!channel.known) return channel.held;
    const auto fraction = static_cast<double>(age) / 500.0;
    Rgb result{};
    for (unsigned i = 0; i < 3; ++i) result[i] = channel.from[i] + (channel.target[i] - channel.from[i]) * fraction;
    return result;
}
layout::Color ChromeColorHistory::Draw(Channel& channel, std::uint64_t lead) noexcept {
    const auto age = std::min<std::uint64_t>(channel.age, 500);
    // Native lead never exceeds one hundred milliseconds beyond the latest owned report
    channel.displayedAge = std::min(age + std::min<std::uint64_t>(100, 500 - age),
        std::max(channel.displayedAge, std::min<std::uint64_t>(age + lead, 500)));
    const auto rgb = !channel.known && channel.age < 500 ? channel.held : At(channel, channel.displayedAge);
    auto byte = [](double value) { return static_cast<std::uint8_t>(std::lround(std::clamp(value, 0.0, 255.0))); };
    return {byte(rgb[0]), byte(rgb[1]), byte(rgb[2]), channel.alpha};
}
std::optional<ChromeColorFrame> ChromeColorHistory::Update(const MenuSnapshot& snapshot,
    std::optional<layout::Color> sample, std::uint64_t now) noexcept {
    if (!snapshot.chromeTheme) {
        const auto& admission = snapshot.localMenuEligibility;
        const bool unknownVisit = snapshot.isOpen && snapshot.menuChromeRevision && *snapshot.menuChromeRevision
            && admission && admission->revision && admission->life == PlayerLifeState::Unknown && admission->hasMenu == true;
        if (!owned_ && !unknownVisit) return std::nullopt;
        owned_ = true;
        // A same-owner unknown observation withdraws painting without inventing source destruction
        const bool retainedUnknown = unknownVisit && (!creation_ || (snapshot.menuChromeRevision == creation_ && snapshot.appearance.design == design_));
        // Focus projection omits the descriptor while closed; it never creates or samples chrome
        if (snapshot.isOpen && !retainedUnknown) retired_ = true;
        return ChromeColorFrame{};
    }
    const auto& state = *snapshot.chromeTheme;
    if (!IsValidChromeThemeState(state)) {
        // A malformed newer receipt cannot recover its own creation after being corrected in place
        if (state.creationRevision && *state.creationRevision && (!creation_ || *state.creationRevision > *creation_)) creation_ = state.creationRevision;
        owned_ = true; retired_ = true; return ChromeColorFrame{};
    }
    if (!state.exists) {
        // Explicit destruction retires a known parent even when its first visible frame was missed
        if (snapshot.menuChromeRevision && *snapshot.menuChromeRevision
            && (!creation_ || *snapshot.menuChromeRevision > *creation_)) creation_ = snapshot.menuChromeRevision;
        owned_ = true; retired_ = true; return ChromeColorFrame{};
    }
    const bool created = !creation_ || *state.creationRevision > *creation_;
    if (!Owned(snapshot, state) || (creation_ && *state.creationRevision < *creation_)) {
        // A corrupt active receipt cannot recover the same positive parent after its facts change
        if (!creation_ || *state.creationRevision > *creation_) creation_ = state.creationRevision;
        if (snapshot.menuChromeRevision && *snapshot.menuChromeRevision
            && (!creation_ || *snapshot.menuChromeRevision > *creation_)) creation_ = snapshot.menuChromeRevision;
        owned_ = true; retired_ = true; return ChromeColorFrame{};
    }
    if (created) {
        // An actual new source creation replaces every prior widget and its callback journal
        creation_ = state.creationRevision; design_ = *state.creationDesign; accent_ = *state.creationAccent;
        creationRainbow_ = *state.creationRainbow; roleMask_ = state.currentRoleMask;
        highest_ = 0; count_ = 0; retired_ = false; owned_ = true; observedRainbowMask_ = 0;
        prefixComplete_ = state.prefixComplete; reportClock_ = lastClock_ = now;
        for (unsigned i = 0; i < 5; ++i) {
            ChromeRgb rgb = accent_; std::uint8_t alpha = 255;
            if ((i == 0 || i == 1) && design_ == MenuDesign::Apparition) rgb = {55,55,55};
            if ((i == 0 || i == 1) && design_ == MenuDesign::Basic) rgb = {0,0,0};
            if (i == 2 && (design_ == MenuDesign::Classic || design_ == MenuDesign::Native || design_ == MenuDesign::Aio)) rgb = {255,255,255};
            if (i == 3 && design_ == MenuDesign::Native) rgb = {0,0,0};
            if (design_ == MenuDesign::Native) alpha = i == 0 || i == 2 ? 179 : i == 1 ? 230 : 255;
            const bool rainbowRole = (ChromeRainbowMask(design_) & (1U << i)) != 0;
            if (creationRainbow_ && rainbowRole && sample) rgb = {sample->red,sample->green,sample->blue};
            auto& channel = channels_[i]; channel = {};
            channel.held = {double(rgb[0]),double(rgb[1]),double(rgb[2])}; channel.from = channel.target = channel.held;
            channel.alpha = alpha; channel.known = !creationRainbow_ || !rainbowRole || sample.has_value();
        }
    }
    if (retired_) return ChromeColorFrame{};
    // Creation facts stay immutable; current admission is a separate mutable proof
    if (design_ != *state.creationDesign || accent_ != *state.creationAccent || creationRainbow_ != *state.creationRainbow
        || (state.currentRoleMask & ~roleMask_) || now < lastClock_ || (!prefixComplete_ && state.prefixComplete)) {
        retired_ = true; return ChromeColorFrame{};
    }
    bool advanced = false;
    for (const auto& event : state.events) for (std::size_t i = 0; i < count_; ++i) if (event.revision == previous_[i].revision) {
        if (!Same(event, previous_[i]) || event.elapsedMilliseconds < previous_[i].elapsedMilliseconds) {
            retired_ = true; return ChromeColorFrame{};
        }
        advanced = advanced || event.elapsedMilliseconds > previous_[i].elapsedMilliseconds;
    }
    if ((!state.events.empty() && state.events.back().revision < highest_)
        || (state.events.empty() && highest_ && state.prefixComplete)) { retired_ = true; return ChromeColorFrame{}; }
    // Width destroys title independently; its historical receipts stay immutable but stop sampling
    const auto removed = roleMask_ & ~state.currentRoleMask;
    for (unsigned i = 0; i < 5; ++i) if (removed & (1U << i)) {
        auto& channel = channels_[i]; channel.held = channel.known ? At(channel,channel.displayedAge) : channel.held; channel.active = false;
    }
    roleMask_ = state.currentRoleMask;
    bool missing = false;
    for (const auto& event : state.events) if (event.revision > highest_) { missing = highest_ && event.revision - highest_ != 1; break; }
    if (missing || (state.events.empty() && !state.prefixComplete)) {
        // Missing history holds the last displayed floating color instead of inventing a start value
        const auto mutableRoles = (ChromeFixedMask(design_) | ChromeRainbowMask(design_)) & roleMask_;
        for (unsigned i = 0; i < 5; ++i) if (mutableRoles & (1U << i)) {
            auto& channel = channels_[i];
            channel.held = channel.known ? At(channel,channel.displayedAge) : channel.held; channel.active = false; channel.known = false;
        }
    }
    // A missing prefix can hide a prior direct-loop write to a role that fixed callbacks never restore
    const auto independentRainbow = ChromeRainbowMask(design_) & ~ChromeFixedMask(design_) & roleMask_;
    if ((created && !state.prefixComplete) || missing || (state.events.empty() && !state.prefixComplete)) {
        for (unsigned i = 0; i < 5; ++i) if (independentRainbow & (1U << i)) channels_[i].known = false;
    }
    bool newEvent = false, priorRainbowConsumed = false;
    for (const auto& event : state.events) {
        const bool retainedRainbow = event.revision > highest_ && !priorRainbowConsumed && !created && !missing;
        if (event.revision > highest_) priorRainbowConsumed = true;
        if (event.revision > highest_ && event.stoppedRainbow && (!retainedRainbow || !observedRainbowMask_)) {
            for (unsigned i = 0; i < 5; ++i) if (independentRainbow & (1U << i)) channels_[i].known = false;
        }
        for (unsigned i = 0; i < 5; ++i) if (event.targetMask & roleMask_ & (1U << i)) {
            auto& channel = channels_[i];
            if (event.revision > highest_) {
                newEvent = true; channel.displayedAge = 0;
                if ((created && !state.prefixComplete) || missing) channel.known = false;
                if (event.stoppedRainbow) {
                    if (!retainedRainbow || !(observedRainbowMask_ & (1U << i))) channel.known = false;
                    channel.from = channel.held;
                } else if (channel.active) {
                    // The exact predecessor gap retains fractional RGB through interrupted fades
                    const auto gap = event.predecessorGapMilliseconds.value_or(0);
                    channel.from = At(channel,gap); if (gap >= 500) channel.known = true;
                } else channel.from = channel.held;
                channel.target = {double(event.accent[0]),double(event.accent[1]),double(event.accent[2])}; channel.active = true;
            }
            if (channel.active) channel.age = event.elapsedMilliseconds;
        }
    }
    for (auto& channel : channels_) if (channel.active && channel.age >= 500) { channel.held = channel.target; channel.known = true; }
    if (newEvent || advanced || created) reportClock_ = now;
    // Rainbow is a direct native sample adapter, with no claim about source coroutine phase
    if (snapshot.smoothRainbowTheme && sample) {
        const auto mask = ChromeRainbowMask(design_) & roleMask_;
        for (unsigned i = 0; i < 5; ++i) if (mask & (1U << i)) {
            auto& channel = channels_[i]; channel.held = {double(sample->red),double(sample->green),double(sample->blue)};
            channel.from = channel.target = channel.held; channel.active = false; channel.known = true;
        }
        observedRainbowMask_ = mask;
    } else if (!snapshot.smoothRainbowTheme) observedRainbowMask_ = 0;
    prefixComplete_ = state.prefixComplete; lastClock_ = now;
    if (!state.events.empty()) highest_ = std::max(highest_,state.events.back().revision);
    count_ = state.events.size(); for (std::size_t i = 0; i < count_; ++i) previous_[i] = state.events[i];
    ChromeColorFrame result; result.admitted = true; result.currentRoleMask = roleMask_;
    const auto lead = std::min<std::uint64_t>(now - reportClock_,100);
    for (unsigned i = 0; i < 5; ++i) {
        result.colors[i] = Draw(channels_[i],(roleMask_ & (1U << i)) ? lead : 0);
        if (channels_[i].known && (roleMask_ & (1U << i))) result.knownRoleMask |= 1U << i;
    }
    return result;
}
}
