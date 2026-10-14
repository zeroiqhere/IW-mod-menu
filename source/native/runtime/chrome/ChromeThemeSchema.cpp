#include "ChromeThemeSchema.h"
#include "../../shared/Instructions/InstructionCreationColor.h"
#include <stdexcept>
#include <string_view>
namespace iw::overlay::runtime {
namespace {
void Fields(const nlohmann::json& value, std::initializer_list<std::string_view> names) {
    if (!value.is_object() || value.size() != names.size()) throw std::runtime_error("Chrome theme requires exact fields");
    for (auto name : names) if (!value.contains(std::string(name))) throw std::runtime_error("Chrome theme field is missing");
}
std::uint64_t Unsigned(const nlohmann::json& value, bool positive = false) {
    if (!value.is_number_unsigned()) throw std::runtime_error("Chrome theme requires unsigned integers");
    const auto result = value.get<std::uint64_t>();
    if (positive && !result) throw std::runtime_error("Chrome theme revision must be positive");
    return result;
}
bool Boolean(const nlohmann::json& value) {
    if (!value.is_boolean()) throw std::runtime_error("Chrome theme requires boolean facts");
    return value.get<bool>();
}
ChromeRgb Accent(const nlohmann::json& value) {
    if (!value.is_string()) throw std::runtime_error("Chrome theme accent must be hex RGB");
    const auto rgb = ParseInstructionRgb(value.get<std::string>());
    if (!rgb) throw std::runtime_error("Chrome theme accent must be exact hex RGB");
    return *rgb;
}
MenuDesign Design(const nlohmann::json& value) {
    if (!value.is_string()) throw std::runtime_error("Chrome creation design must be a name");
    if (value == "Apparition") return MenuDesign::Apparition;
    if (value == "Classic") return MenuDesign::Classic;
    if (value == "Native") return MenuDesign::Native;
    if (value == "AIO") return MenuDesign::Aio;
    if (value == "Basic") return MenuDesign::Basic;
    throw std::runtime_error("Unsupported chrome creation design");
}
}
ChromeThemeState ParseChromeTheme(const nlohmann::json& value, const MenuSnapshot& snapshot) {
    Fields(value, {"exists", "creationRevision", "admissionRevision", "creationDesign", "creationAccent", "creationRainbow", "currentRoleMask", "prefixComplete", "events"});
    ChromeThemeState result; result.exists = Boolean(value.at("exists"));
    const auto mask = Unsigned(value.at("currentRoleMask"));
    if (mask > 31) throw std::runtime_error("Chrome role mask exceeds the source roles");
    result.currentRoleMask = static_cast<unsigned>(mask); result.prefixComplete = Boolean(value.at("prefixComplete"));
    const auto& events = value.at("events");
    if (!events.is_array() || events.size() > 8) throw std::runtime_error("Chrome theme journal exceeds its bound");
    if (result.exists) {
        result.creationRevision = Unsigned(value.at("creationRevision"), true);
        result.admissionRevision = Unsigned(value.at("admissionRevision"), true);
        result.creationDesign = Design(value.at("creationDesign")); result.creationAccent = Accent(value.at("creationAccent"));
        result.creationRainbow = Boolean(value.at("creationRainbow"));
        const auto& admission = snapshot.localMenuEligibility;
        // Input locks hide or pause existing chrome without changing its source ownership
        if (!snapshot.isOpen || snapshot.menuChromeRevision != result.creationRevision || snapshot.appearance.design != result.creationDesign || !admission
            || admission->revision != result.admissionRevision || admission->hasMenu != true || admission->life == PlayerLifeState::Unknown
            || snapshot.menuOpenTransition == MenuOpenTransitionPhase::Rebuild || snapshot.menuDesignTransition == MenuDesignTransitionPhase::Rebuild
            || snapshot.menuPreferenceRebuild == MenuPreferenceRebuildPhase::Rebuild)
            throw std::runtime_error("Chrome theme requires matching existing actor-owned chrome");
    } else {
        // Revocation and undrawn rebuild gaps are both valid explicit source absence
        for (const auto* field : {"creationRevision", "admissionRevision", "creationDesign", "creationAccent", "creationRainbow"})
            if (!value.at(field).is_null()) throw std::runtime_error("Absent chrome cannot retain creation facts");
    }
    for (const auto& item : events) {
        Fields(item, {"revision", "targetMask", "accent", "elapsedMilliseconds", "predecessorGapMilliseconds", "stoppedRainbow"});
        ChromeThemeEvent event; event.revision = Unsigned(item.at("revision"), true);
        const auto target = Unsigned(item.at("targetMask"));
        if (target > 31) throw std::runtime_error("Chrome event target exceeds its source roles");
        event.targetMask = static_cast<unsigned>(target); event.accent = Accent(item.at("accent"));
        event.elapsedMilliseconds = Unsigned(item.at("elapsedMilliseconds"));
        const auto& gap = item.at("predecessorGapMilliseconds");
        if (!gap.is_null()) {
            const auto interval = Unsigned(gap); if (interval > 500) throw std::runtime_error("Chrome event gap exceeds 500 ms");
            event.predecessorGapMilliseconds = static_cast<unsigned>(interval);
        }
        event.stoppedRainbow = Boolean(item.at("stoppedRainbow")); result.events.push_back(event);
    }
    // Historical fixed masks belong to creation, even after a width editor destroys title
    if (!IsValidChromeThemeState(result)) throw std::runtime_error("Chrome theme journal is inconsistent");
    return result;
}
}
