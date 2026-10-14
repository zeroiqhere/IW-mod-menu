#include "LocalMenuEligibilitySchema.h"
#include <stdexcept>

namespace iw::overlay::runtime {
namespace {
std::optional<bool> NullableFact(const nlohmann::json& value) {
    // Unknown is preserved explicitly and cannot silently become permission
    if (value.is_null()) return std::nullopt;
    if (!value.is_boolean()) throw std::runtime_error("An admission fact must be a boolean or null");
    return value.get<bool>();
}
}

LocalMenuEligibility ParseLocalMenuEligibility(const nlohmann::json& value) {
    if (!value.is_object() || value.size() != 5 || !value.contains("revision")
        || !value.contains("life") || !value.contains("hasMenu")
        || !value.contains("controlsDisabled") || !value.contains("blocked")) {
        throw std::runtime_error("Local admission requires exactly five fields");
    }
    const auto& revision = value.at("revision");
    if (!revision.is_number_unsigned() || revision.get<std::uint64_t>() == 0) {
        throw std::runtime_error("Local admission requires a positive unsigned revision");
    }
    const auto& life = value.at("life");
    if (!life.is_string()) throw std::runtime_error("Player life must be an exact state name");
    LocalMenuEligibility result;
    result.revision = revision.get<std::uint64_t>();
    // Only the three observed life states cross the wire; no session labels are inferred
    if (life == "unknown") result.life = PlayerLifeState::Unknown;
    else if (life == "alive") result.life = PlayerLifeState::Alive;
    else if (life == "dead") result.life = PlayerLifeState::Dead;
    else throw std::runtime_error("Unsupported player life state");
    result.hasMenu = NullableFact(value.at("hasMenu"));
    result.controlsDisabled = NullableFact(value.at("controlsDisabled"));
    const auto& blocked = value.at("blocked");
    if (!blocked.is_boolean()) throw std::runtime_error("Local blocking must be a boolean");
    result.blocked = blocked.get<bool>();
    return result;
}

MenuSurface ParseMenuSurface(const nlohmann::json& value) {
    // Root identity and captions do not identify a menu family
    if (!value.is_string()) throw std::runtime_error("Menu surface must be an exact state name");
    if (value == "main") return MenuSurface::Main;
    if (value == "quick") return MenuSurface::Quick;
    throw std::runtime_error("Unsupported menu surface");
}
}
