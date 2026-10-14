#include "MenuMonitorContinuationSchema.h"
#include <stdexcept>

namespace iw::overlay::runtime {
MenuMonitorContinuation ParseMenuMonitorContinuation(const nlohmann::json& value,
    const MenuSnapshot& snapshot, std::uint64_t acknowledgedSequence) {
    // Closed field sets prevent a receipt from acquiring unrelated callback or permission facts
    if (!value.is_object() || value.size() != 4 || !value.contains("revision") || !value.contains("sequence")
        || !value.contains("admissionRevision") || !value.contains("kind")) {
        throw std::runtime_error("Mechanical continuation requires exactly four fields");
    }
    for (const auto* field : {"revision", "sequence", "admissionRevision"}) {
        if (!value.at(field).is_number_unsigned() || value.at(field).get<std::uint64_t>() == 0) {
            throw std::runtime_error("Mechanical continuation requires positive unsigned owner fields");
        }
    }
    const auto& kind = value.at("kind");
    if (!kind.is_string() || (kind != "choice" && kind != "choiceNoop" && kind != "back")) {
        throw std::runtime_error("Unsupported mechanical continuation kind");
    }
    MenuMonitorContinuation receipt{value.at("revision").get<std::uint64_t>(),
        value.at("sequence").get<std::uint64_t>(), value.at("admissionRevision").get<std::uint64_t>(),
        kind == "back" ? MenuMonitorWaitKind::Back : kind == "choiceNoop"
            ? MenuMonitorWaitKind::ChoiceNoop : MenuMonitorWaitKind::Choice};
    // Known admission keeps the causal owner distinct from disabled controls and opening permission
    const auto& admission = snapshot.localMenuEligibility;
    if (!IsKnownMenuSurface(snapshot.menuSurface) || !admission || admission->hasMenu != true
        || (admission->life != PlayerLifeState::Alive && admission->life != PlayerLifeState::Dead)
        || admission->revision != receipt.admissionRevision || receipt.sequence > acknowledgedSequence) {
        throw std::runtime_error("Mechanical continuation requires its current acknowledged admission");
    }
    // Back can retain the actual closed child after Close without rewriting it into a root
    if ((receipt.kind != MenuMonitorWaitKind::Back && (!snapshot.isOpen || snapshot.rows.empty()))
        || (!snapshot.rows.empty() && (snapshot.selectedIndex >= snapshot.rows.size()
            || snapshot.rows[snapshot.selectedIndex].isSpacer))
        || (snapshot.rows.empty() && snapshot.selectedIndex != 0)) {
        throw std::runtime_error("Mechanical continuation requires an ordinary mechanical result");
    }
    // Waiting and ready snapshots retain one invocation while all other source loops stay exclusive
    if ((snapshot.menuMonitorWait && (snapshot.menuMonitorWait->revision != receipt.revision
            || snapshot.menuMonitorWait->kind != receipt.kind))
        || snapshot.menuUseContinuation || snapshot.menuOptionCallback || snapshot.widthEditor
        || snapshot.instructionPositionEditor || snapshot.menuPositionEditor || snapshot.menuCredits
        || snapshot.menuUnlock || snapshot.menuPreferenceRefresh || snapshot.menuPreferenceRebuild
        || snapshot.menuDesignTransition || snapshot.menuOpenTransition) {
        throw std::runtime_error("Mechanical continuation cannot share another callback or editor");
    }
    return receipt;
}
}
