#include "InstructionHudSchema.h"
#include "../../shared/Instructions/InstructionCreationColor.h"
#include <algorithm>
#include <stdexcept>

namespace iw::overlay::runtime {
namespace {
std::uint64_t PositiveRevision(const nlohmann::json& value) {
    if (!value.is_number_unsigned() || value.get<std::uint64_t>() == 0)
        throw std::runtime_error("Instruction ownership requires a positive unsigned revision");
    return value.get<std::uint64_t>();
}
MenuDesign CreationDesign(const nlohmann::json& value) {
    if (!value.is_string()) throw std::runtime_error("Instruction creation design must be a name");
    if (value == "Apparition") return MenuDesign::Apparition;
    if (value == "Classic") return MenuDesign::Classic;
    if (value == "Native") return MenuDesign::Native;
    if (value == "AIO") return MenuDesign::Aio;
    if (value == "Basic") return MenuDesign::Basic;
    throw std::runtime_error("Unsupported instruction creation design");
}
std::string Label(const OpeningActionLabels* labels, unsigned action) {
    return labels ? labels->at(action) : std::string{};
}
std::string Chord(const MainOpeningControls& policy, const OpeningActionLabels* labels) {
    std::string result;
    for (auto action : policy.actions) {
        const auto label = Label(labels, static_cast<unsigned>(action));
        if (label.empty()) return {};
        if (!result.empty()) result += " + ";
        result += label;
    }
    return result;
}
std::string Availability(const LocalMenuEligibility& eligibility) {
    if (eligibility.blocked) return "controls paused";
    if (eligibility.controlsDisabled != false) return "controls unavailable";
    return {};
}
std::string Hint(std::string chord, std::string name, const std::string& reason) {
    if (chord.empty()) return name + " unavailable (saved controls not bound)";
    return chord + ": " + (reason.empty() ? "Open " + name : name + " unavailable (" + reason + ")");
}
void ClosedHints(InstructionHud& hud, const MenuSnapshot& snapshot, const OpeningActionLabels* labels) {
    const auto& eligibility = *snapshot.localMenuEligibility;
    auto reason = Availability(eligibility);
    if (eligibility.life == PlayerLifeState::Dead) {
        const auto aim = Label(labels, 5), jump = Label(labels, 12);
        if (aim.empty() || jump.empty()) {
            hud.instructions = {"Quick Menu unavailable (saved " + std::string(aim.empty() ? "Aim" : "Jump") + " not bound)"};
            return;
        }
        // The source help ignores DisableQM; the native hint discloses the actual routing gate
        if (snapshot.openingPolicies && snapshot.openingPolicies->quickMenuDisabled) reason = "disabled";
        if (!snapshot.mainOpeningControls) reason = "opening policy unavailable";
        hud.instructions = {Hint(aim + " + " + jump, "Quick Menu", reason)};
        return;
    }
    if (snapshot.mainOpeningControls) hud.instructions.push_back(Hint(Chord(*snapshot.mainOpeningControls, labels), "Main Menu", reason));
    else hud.instructions.push_back("Main Menu unavailable (opening policy unavailable)");
    if (!snapshot.openingPolicies) {
        hud.instructions.push_back("Quick Menu unavailable (opening policy unavailable)");
        return;
    }
    // Living source help omits a disabled Quick route rather than advertising its configured tuple
    if (snapshot.openingPolicies->quickMenuDisabled) return;
    auto quickReason = reason;
    if (!snapshot.mainOpeningControls) quickReason = "opening policy unavailable";
    // A held Main tuple wins when every Main action is also in the configured Quick tuple
    const auto& policies = *snapshot.openingPolicies;
    if (quickReason.empty() && std::all_of(policies.main.actions.begin(), policies.main.actions.end(), [&](auto action) {
        return std::find(policies.quick.actions.begin(), policies.quick.actions.end(), action) != policies.quick.actions.end();
    })) quickReason = "Main chord has priority";
    hud.instructions.push_back(Hint(Chord(policies.quick, labels), "Quick Menu", quickReason));
}
void DeadOpenHints(InstructionHud& hud, const MenuSnapshot& snapshot, const OpeningActionLabels* labels) {
    auto pair = [&](unsigned a, unsigned b, const char* purpose) {
        const auto first = Label(labels, a), second = Label(labels, b);
        return first.empty() || second.empty() ? std::string(purpose) + " unavailable (saved controls not bound)"
            : first + " | " + second + ": " + purpose;
    };
    hud.instructions.push_back(pair(6, 5, "Scroll"));
    hud.instructions.push_back(pair(2, 3, "Previous / next choice"));
    const auto use = Label(labels, 8), jump = Label(labels, 12);
    hud.instructions.push_back(use.empty() ? "Select unavailable (saved Use not bound)" : use + ": Select");
    hud.instructions.push_back(jump.empty() ? "Exit unavailable (saved Jump not bound)"
        : snapshot.menuSurface != MenuSurface::Quick ? jump + ": Exit unavailable (Main surface)" : jump + ": Back / close");
}
}
std::optional<InstructionHud> ParseInstructionHud(const nlohmann::json& value,
    const MenuSnapshot& snapshot, const OpeningActionLabels* labels) {
    // Null retains the legacy contract; an explicit owned absence suppresses existing help
    if (value.is_null()) return std::nullopt;
    if (!value.is_object() || value.size() < 6 || value.size() > 8 || !value.contains("exists")
        || !value.contains("creationRevision") || !value.contains("admissionRevision")
        || !value.contains("creationDesign") || !value.contains("creationAlternate") || !value.contains("creationBackground")
        || !value.at("exists").is_boolean())
        throw std::runtime_error("Instruction HUD requires six fields and an optional paired creation color");
    for(const auto& item:value.items()) {
        const auto& key=item.key();
        if(key!="exists" && key!="creationRevision" && key!="admissionRevision" && key!="creationDesign"
            && key!="creationAlternate" && key!="creationBackground" && key!="creationAccent" && key!="creationRainbow")
            throw std::runtime_error("Unknown instruction HUD field");
    }
    const bool hasAccent=value.contains("creationAccent") && !value.at("creationAccent").is_null();
    const bool hasRainbow=value.contains("creationRainbow") && !value.at("creationRainbow").is_null();
    if(hasAccent!=hasRainbow) throw std::runtime_error("Instruction creation accent and mode must be paired");
    InstructionHud result; result.exists = value.at("exists").get<bool>();
    if (!result.exists) {
        for (const auto* key : {"creationRevision", "admissionRevision", "creationDesign", "creationAlternate", "creationBackground"})
            if (!value.at(key).is_null()) throw std::runtime_error("Absent instructions cannot retain active creation fields");
        if(hasAccent || hasRainbow) throw std::runtime_error("Absent instructions cannot retain creation color");
        return result;
    }
    if(hasAccent) {
        if(!value.at("creationAccent").is_string() || !value.at("creationRainbow").is_boolean())
            throw std::runtime_error("Instruction creation color requires hex RGB and a boolean mode");
        auto accent=value.at("creationAccent").get<std::string>();
        if(!ParseInstructionRgb(accent)) throw std::runtime_error("Instruction creation accent must be exact hex RGB");
        // Normalize wire spelling once; retained ownership compares the parsed RGB channels
        for(char& byte:accent) if(byte>='a' && byte<='f') byte=static_cast<char>(byte-'a'+'A');
        result.creationAccent=std::move(accent);
        result.creationRainbow=value.at("creationRainbow").get<bool>();
    }
    result.creationRevision = PositiveRevision(value.at("creationRevision"));
    result.admissionRevision = PositiveRevision(value.at("admissionRevision"));
    result.creationDesign = CreationDesign(value.at("creationDesign"));
    if (!value.at("creationAlternate").is_boolean() || !value.at("creationBackground").is_boolean())
        throw std::runtime_error("Instruction creation styles must be booleans");
    result.creationAlternate = value.at("creationAlternate").get<bool>();
    result.creationBackground = value.at("creationBackground").get<bool>();
    const auto& eligibility = snapshot.localMenuEligibility;
    if (!eligibility || eligibility->revision != result.admissionRevision || eligibility->hasMenu != true
        || eligibility->life == PlayerLifeState::Unknown || !snapshot.appearance.instructionsVisible)
        throw std::runtime_error("Instruction ownership requires matching known local admission");
    // Physical key captions are private data copied from the same immutable saved groups as routing
    if (!snapshot.isOpen) ClosedHints(result, snapshot, labels);
    else if (!Availability(*eligibility).empty()) {
        // Native input consumes blocked ordinary actions even while source footer ownership remains
        result.instructions = {"Menu controls unavailable", Availability(*eligibility)};
    } else if (eligibility->life == PlayerLifeState::Dead) DeadOpenHints(result, snapshot, labels);
    return result;
}
}
