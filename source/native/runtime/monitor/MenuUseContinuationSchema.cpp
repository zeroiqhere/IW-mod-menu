#include "MenuUseContinuationSchema.h"
#include <stdexcept>

namespace iw::overlay::runtime {
MenuUseContinuation ParseMenuUseContinuation(const nlohmann::json& value,
    const MenuSnapshot& snapshot, std::uint64_t acknowledgedSequence) {
    if (!value.is_object() || value.size() != 4 || !value.contains("revision") || !value.contains("sequence")
        || !value.contains("admissionRevision") || !value.contains("kind")) {
        throw std::runtime_error("Use continuation requires exactly four fields");
    }
    for (const auto* field : {"revision", "sequence", "admissionRevision"}) {
        if (!value.at(field).is_number_unsigned() || value.at(field).get<std::uint64_t>() == 0) {
            throw std::runtime_error("Use continuation requires positive unsigned owner fields");
        }
    }
    const auto& kind = value.at("kind");
    if (!kind.is_string() || (kind != "plain" && kind != "boolean")) {
        throw std::runtime_error("Unsupported Use continuation kind");
    }
    MenuUseContinuation receipt{value.at("revision").get<std::uint64_t>(), value.at("sequence").get<std::uint64_t>(),
        value.at("admissionRevision").get<std::uint64_t>(), kind == "plain"
            ? MenuOptionCallbackKind::Plain : MenuOptionCallbackKind::Boolean};
    // The receipt keeps causal identity while ordinary admission and callback locks stay separate
    if (!snapshot.isOpen || !IsKnownMenuSurface(snapshot.menuSurface)
        || !snapshot.localMenuEligibility || snapshot.localMenuEligibility->hasMenu != true
        || (snapshot.localMenuEligibility->life != PlayerLifeState::Alive
            && snapshot.localMenuEligibility->life != PlayerLifeState::Dead)
        || snapshot.localMenuEligibility->revision != receipt.admissionRevision
        || receipt.sequence > acknowledgedSequence || snapshot.rows.empty()
        || snapshot.selectedIndex >= snapshot.rows.size() || snapshot.rows[snapshot.selectedIndex].isSpacer
        || (snapshot.menuOptionCallback && (snapshot.menuOptionCallback->revision != receipt.revision
            || snapshot.menuOptionCallback->kind != receipt.kind))
        || snapshot.menuMonitorWait || snapshot.widthEditor || snapshot.instructionPositionEditor
        || snapshot.menuPositionEditor || snapshot.menuCredits || snapshot.menuUnlock
        || snapshot.menuPreferenceRefresh || snapshot.menuPreferenceRebuild
        || snapshot.menuOpenTransition) {
        throw std::runtime_error("Use continuation requires its exact admitted ordinary invocation");
    }
    return receipt;
}

void ValidateMenuDesignUseContinuation(const MenuSnapshot& snapshot) {
    if (!snapshot.menuDesignTransition || (!snapshot.menuOptionCallback && !snapshot.menuUseContinuation)) return;
    if (!snapshot.menuOptionCallback || !snapshot.menuUseContinuation
        || snapshot.menuOptionCallback->kind != MenuOptionCallbackKind::Plain
        || snapshot.menuOptionCallback->phase != MenuOptionCallbackPhase::Cooldown
        || snapshot.menuUseContinuation->kind != MenuOptionCallbackKind::Plain
        || snapshot.menuOptionCallback->revision != snapshot.menuUseContinuation->revision) {
        throw std::runtime_error("Design transition Use requires its exact plain cooldown receipt");
    }
}
}
