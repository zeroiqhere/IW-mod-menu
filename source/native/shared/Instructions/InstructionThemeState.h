#pragma once
#include "InstructionCreationColor.h"
#include <cstdint>
#include <array>
#include <optional>
#include <vector>
namespace iw::overlay {
enum class InstructionThemeTarget { Outline, Background };
struct InstructionThemeEvent {
    std::uint64_t revision = 0;
    InstructionThemeTarget target = InstructionThemeTarget::Outline;
    InstructionRgb accent{};
    std::uint64_t elapsedMilliseconds = 0;
    std::optional<unsigned> predecessorGapMilliseconds;
    bool stoppedRainbow = false;
};
// Captured targets belong to one actual footer creation, independently of menu redraws
struct InstructionThemeState {
    std::uint64_t creationRevision = 0;
    bool prefixComplete = true;
    std::vector<InstructionThemeEvent> events;
};
inline bool IsValidInstructionThemeState(const InstructionThemeState& state) noexcept {
    if(!state.creationRevision || state.events.size()>8) return false;
    // Pruning removes a prefix only; every retained invocation remains part of the same contiguous tail
    std::array<std::optional<std::uint64_t>,2> ages{};
    std::uint64_t previousRevision=0,previousAge=0;
    for(const auto& event:state.events) {
        if(!event.revision || (event.target!=InstructionThemeTarget::Outline && event.target!=InstructionThemeTarget::Background)) return false;
        if(previousRevision && (event.revision<=previousRevision || event.elapsedMilliseconds>previousAge)) return false;
        if(previousRevision && event.revision-previousRevision!=1) return false;
        const unsigned target=event.target==InstructionThemeTarget::Outline ? 0U : 1U;
        if(event.predecessorGapMilliseconds && *event.predecessorGapMilliseconds>500) return false;
        if(ages[target]) {
            if(!event.predecessorGapMilliseconds) return false;
            // Ordered uint64 ages are subtracted before the one-millisecond clock tolerance is checked
            const auto difference=*ages[target]-event.elapsedMilliseconds;
            const auto gap=*event.predecessorGapMilliseconds;
            if(gap==500) {if(difference<499) return false;}
            else if(difference>gap ? difference-gap>1 : gap-difference>1) return false;
        } else if(state.prefixComplete && event.predecessorGapMilliseconds) return false;
        ages[target]=event.elapsedMilliseconds;previousRevision=event.revision;previousAge=event.elapsedMilliseconds;
    }
    return true;
}

}
