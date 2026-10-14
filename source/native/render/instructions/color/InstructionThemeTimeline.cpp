#include "InstructionThemeTimeline.h"
#include <algorithm>
#include <cmath>
namespace iw::overlay {
namespace {
unsigned Target(InstructionThemeTarget target) noexcept {return target==InstructionThemeTarget::Outline ? 0U : 1U;}
bool Same(const InstructionThemeEvent& a,const InstructionThemeEvent& b) noexcept {
    return a.revision==b.revision && a.target==b.target && a.accent==b.accent
        && a.predecessorGapMilliseconds==b.predecessorGapMilliseconds && a.stoppedRainbow==b.stoppedRainbow;
}
}
InstructionThemeTimeline::Rgb InstructionThemeTimeline::At(const Channel& channel,std::uint64_t elapsed) noexcept {
    if(!channel.active) return channel.held;
    if(elapsed>=500) return channel.target;
    if(!channel.known) return channel.held;
    const double fraction=static_cast<double>(elapsed)/500.0;
    Rgb result{};
    for(unsigned i=0;i<3;++i) result[i]=channel.from[i]+(channel.target[i]-channel.from[i])*fraction;
    return result;
}
layout::Color InstructionThemeTimeline::Draw(Channel& channel,std::uint64_t lead) noexcept {
    // Only the bounded duration is converted; target bytes are rounded after all interruptions
    const auto elapsed=std::min<std::uint64_t>(channel.age,500);
    const auto proposed=std::min<std::uint64_t>(elapsed+lead,500);
    const auto bound=std::min<std::uint64_t>(elapsed+100,500);
    channel.displayedAge=std::min(bound,std::max(channel.displayedAge,proposed));
    const auto rgb=!channel.known && channel.age<500 ? channel.held : At(channel,channel.displayedAge);
    auto byte=[](double value){return static_cast<std::uint8_t>(std::lround(std::clamp(value,0.0,255.0)));};
    return {byte(rgb[0]),byte(rgb[1]),byte(rgb[2]),channel.alpha};
}
std::optional<InstructionHudColors> InstructionThemeTimeline::Update(const InstructionThemeState& state,
    InstructionHudColors seed,std::optional<InstructionThemeTarget> observedRainbowTarget,std::uint64_t now) noexcept {
    if(!IsValidInstructionThemeState(state)) {
        if(creation_ && state.creationRevision<=*creation_) quarantined_=true;
        return std::nullopt;
    }
    const bool created=!creation_ || state.creationRevision>*creation_;
    if(creation_ && state.creationRevision<*creation_) {quarantined_=true;return std::nullopt;}
    if(created) {
        creation_=state.creationRevision;highest_=0;count_=0;quarantined_=false;
        prefixComplete_=state.prefixComplete;reportClock_=now;lastClock_=now;
        const std::array<layout::Color,2> initial{seed.outline,seed.background};
        for(unsigned i=0;i<2;++i) {
            const auto color=initial[i];channels_[i]={};channels_[i].held={double(color.red),double(color.green),double(color.blue)};
            channels_[i].from=channels_[i].target=channels_[i].held;channels_[i].alpha=color.alpha;
        }
    }
    if(quarantined_) return std::nullopt;
    // A restarted clock or a recovered complete prefix cannot revive the same creation
    if(now<lastClock_ || (!prefixComplete_ && state.prefixComplete)) {quarantined_=true;return std::nullopt;}
    // Compare immutable receipts before any channel or sampled color changes
    bool advanced=false;
    for(const auto& event:state.events) for(std::size_t i=0;i<count_;++i) if(event.revision==previous_[i].revision) {
        if(!Same(event,previous_[i]) || event.elapsedMilliseconds<previous_[i].elapsedMilliseconds) {
            quarantined_=true;return std::nullopt;
        }
        advanced=advanced || event.elapsedMilliseconds>previous_[i].elapsedMilliseconds;
    }
    if(!state.events.empty() && state.events.back().revision<highest_) {quarantined_=true;return std::nullopt;}
    if(state.events.empty() && highest_ && state.prefixComplete) {quarantined_=true;return std::nullopt;}
    // A contiguous tail must bridge the last owned invocation, even when its old prefix was pruned
    bool bridgeMissing=false;
    for(const auto& event:state.events) if(event.revision>highest_) {
        bridgeMissing=highest_ && event.revision-highest_!=1;break;
    }
    const bool missing=highest_ && !state.events.empty() && bridgeMissing;
    if((state.events.empty() && !state.prefixComplete) || missing) {
        // A lost prefix retires the active segment, never substitutes an invented old start RGB
        for(auto& channel:channels_) {channel.held=channel.known ? At(channel,channel.displayedAge) : channel.held;channel.active=false;channel.known=false;}
    }
    bool newEvent=false;
    std::array<bool,2> seen{};
    for(const auto& event:state.events) {
        const auto index=Target(event.target);auto& channel=channels_[index];
        if(event.revision>highest_) {
            newEvent=true;channel.displayedAge=0;
            const bool unknownPrefix=(!state.prefixComplete && !seen[index] && (created || !highest_)) || missing;
            if(unknownPrefix) channel.known=false;
            if(event.stoppedRainbow) {
                // Only a retained observed native sample can establish an interrupted Rainbow start
                if(!observedRainbowTarget || *observedRainbowTarget!=event.target || created || missing) channel.known=false;
                channel.from=channel.held;
            } else if(channel.active) {
                // The same-target gap preserves fractional RGB at the actual interruption boundary
                const auto elapsed=event.predecessorGapMilliseconds.value_or(0);
                channel.from=At(channel,elapsed);
                if(elapsed>=500) channel.known=true;
            } else channel.from=channel.held;
            channel.target={double(event.accent[0]),double(event.accent[1]),double(event.accent[2])};
            channel.active=true;
        }
        // A report's age belongs to the latest invocation for this target
        if(channel.active) channel.age=event.elapsedMilliseconds;
        seen[index]=true;
    }
    // Only the final invocation can prove its endpoint; interrupted older entries never settle retroactively
    for(auto& channel:channels_) if(channel.active && channel.age>=500) {channel.held=channel.target;channel.known=true;}
    if(newEvent || advanced || created) reportClock_=now;
    // Fixed arrays retain only the bounded window and never allocate in the render path
    prefixComplete_=state.prefixComplete;lastClock_=now;
    if(!state.events.empty()) highest_=std::max(highest_,state.events.back().revision);
    count_=state.events.size();for(std::size_t i=0;i<count_;++i)previous_[i]=state.events[i];
    const auto lead=std::min<std::uint64_t>(now-reportClock_,100);
    return InstructionHudColors{Draw(channels_[0],lead),Draw(channels_[1],lead)};
}
void InstructionThemeTimeline::Override(InstructionThemeTarget target,layout::Color sample) noexcept {
    if(!creation_ || quarantined_) return;
    auto& channel=channels_[Target(target)];
    // The native direct-loop adapter supersedes this channel only and preserves creation alpha
    channel.held={double(sample.red),double(sample.green),double(sample.blue)};
    channel.from=channel.target=channel.held;channel.active=false;channel.known=true;
}
}
