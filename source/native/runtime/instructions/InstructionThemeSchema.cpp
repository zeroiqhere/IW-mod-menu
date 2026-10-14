#include "InstructionThemeSchema.h"
#include "../../render/instructions/InstructionHudPresence.h"
#include <stdexcept>
#include <string_view>
namespace iw::overlay::runtime {
namespace {
void Fields(const nlohmann::json& value,std::initializer_list<std::string_view> names) {
    if(!value.is_object() || value.size()!=names.size()) throw std::runtime_error("Instruction theme requires exact fields");
    for(auto name:names) if(!value.contains(std::string(name))) throw std::runtime_error("Instruction theme field is missing");
}
std::uint64_t Unsigned(const nlohmann::json& value,bool positive=false) {
    if(!value.is_number_unsigned()) throw std::runtime_error("Instruction theme requires unsigned integers");
    const auto result=value.get<std::uint64_t>();
    if(positive && !result) throw std::runtime_error("Instruction theme revision must be positive");
    return result;
}
bool Boolean(const nlohmann::json& value) {
    if(!value.is_boolean()) throw std::runtime_error("Instruction theme requires boolean facts");
    return value.get<bool>();
}
}
InstructionThemeState ParseInstructionTheme(const nlohmann::json& value,const MenuSnapshot& snapshot) {
    Fields(value,{"creationRevision","prefixComplete","events"});
    InstructionThemeState result;
    result.creationRevision=Unsigned(value.at("creationRevision"),true);
    result.prefixComplete=Boolean(value.at("prefixComplete"));
    // No journal can authorize an absent or differently admitted footer
    if(!HasOwnedInstructionHud(snapshot) || snapshot.instructionHud->creationRevision!=result.creationRevision
        || !snapshot.instructionHud->creationAccent)
        throw std::runtime_error("Instruction theme requires its matching owned color creation");
    const auto& events=value.at("events");
    if(!events.is_array() || events.size()>8) throw std::runtime_error("Instruction theme journal exceeds its bound");
    for(const auto& item:events) {
        Fields(item,{"revision","target","accent","elapsedMilliseconds","predecessorGapMilliseconds","stoppedRainbow"});
        InstructionThemeEvent event;
        event.revision=Unsigned(item.at("revision"),true);
        if(!item.at("target").is_string()) throw std::runtime_error("Instruction theme target must be a name");
        if(item.at("target")=="outline") event.target=InstructionThemeTarget::Outline;
        else if(item.at("target")=="background") event.target=InstructionThemeTarget::Background;
        else throw std::runtime_error("Unknown instruction theme target");
        if(!item.at("accent").is_string()) throw std::runtime_error("Instruction theme accent must be hex RGB");
        const auto rgb=ParseInstructionRgb(item.at("accent").get<std::string>());
        if(!rgb) throw std::runtime_error("Instruction theme accent must be exact hex RGB");
        event.accent=*rgb;
        event.elapsedMilliseconds=Unsigned(item.at("elapsedMilliseconds"));
        const auto& gap=item.at("predecessorGapMilliseconds");
        if(!gap.is_null()) {
            const auto interval=Unsigned(gap);
            if(interval>500) throw std::runtime_error("Instruction theme predecessor gap exceeds 500 ms");
            event.predecessorGapMilliseconds=static_cast<unsigned>(interval);
        }
        event.stoppedRainbow=Boolean(item.at("stoppedRainbow"));
        result.events.push_back(event);
    }
    // The shared validator also protects direct native callers from false quiet gaps and internal holes
    if(!IsValidInstructionThemeState(result)
        || (snapshot.instructionHud->creationBackground!=true && !result.events.empty()))
        throw std::runtime_error("Instruction theme journal is inconsistent with its owned widgets");
    return result;
}
}
