#include "MenuOptionCallbackSchema.h"
#include <nlohmann/json.hpp>
#include <stdexcept>

namespace iw::overlay::runtime {
MenuOptionCallback ParseMenuOptionCallback(const nlohmann::json& value, const MenuSnapshot& snapshot) {
    if (!value.is_object() || value.size() != 3 || !value.contains("revision")
        || !value.contains("kind") || !value.contains("phase")) {
        throw std::runtime_error("Callback ownership requires exactly three fields");
    }
    const auto& revision = value.at("revision");
    const auto& kind = value.at("kind");
    const auto& phase = value.at("phase");
    if (!revision.is_number_unsigned() || revision.get<std::uint64_t>() == 0
        || !kind.is_string() || !phase.is_string()) {
        throw std::runtime_error("Callback ownership requires a positive revision and exact state names");
    }
    MenuOptionCallback result;
    result.revision = revision.get<std::uint64_t>();
    // The descriptor reports cadence, never a successful feature result
    if (kind == "boolean") result.kind = MenuOptionCallbackKind::Boolean;
    else if (kind == "plain") result.kind = MenuOptionCallbackKind::Plain;
    else throw std::runtime_error("Unsupported callback kind");
    if (phase == "pending") result.phase = MenuOptionCallbackPhase::Pending;
    else if (phase == "cooldown") result.phase = MenuOptionCallbackPhase::Cooldown;
    else throw std::runtime_error("Unsupported callback phase");
    if (!IsValidMenuOptionCallback(result)) throw std::runtime_error("Plain callbacks cannot own a boolean pending phase");
    // Root and child callbacks share the same populated ordinary-menu route
    if (!snapshot.isOpen || !IsKnownMenuSurface(snapshot.menuSurface) || snapshot.rows.empty()
        || snapshot.selectedIndex >= snapshot.rows.size() || snapshot.rows[snapshot.selectedIndex].isSpacer
        || snapshot.widthEditor || snapshot.instructionPositionEditor || snapshot.menuPositionEditor
        || snapshot.menuCredits || snapshot.menuUnlock || snapshot.menuPreferenceRefresh
        || (snapshot.menuDesignTransition && (result.kind != MenuOptionCallbackKind::Plain
            || result.phase != MenuOptionCallbackPhase::Cooldown))
        || snapshot.menuOpenTransition || snapshot.menuPreferenceRebuild) {
        throw std::runtime_error("Invalid menu callback context");
    }
    return result;
}
}
