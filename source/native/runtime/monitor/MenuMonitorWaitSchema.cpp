#include "MenuMonitorWaitSchema.h"
#include <nlohmann/json.hpp>
#include <stdexcept>

namespace iw::overlay::runtime {
MenuMonitorWait ParseMenuMonitorWait(const nlohmann::json& value, const MenuSnapshot& snapshot) {
    if (!value.is_object() || value.size() != 2 || !value.contains("revision") || !value.contains("kind")) {
        throw std::runtime_error("Mechanical monitor ownership requires exactly two fields");
    }
    const auto& revision = value.at("revision");
    const auto& kind = value.at("kind");
    if (!revision.is_number_unsigned() || revision.get<std::uint64_t>() == 0 || !kind.is_string()) {
        throw std::runtime_error("Mechanical monitor ownership requires a positive revision and exact kind");
    }
    MenuMonitorWait result{revision.get<std::uint64_t>(), MenuMonitorWaitKind::Choice};
    if (kind == "choice") result.kind = MenuMonitorWaitKind::Choice;
    else if (kind == "back") result.kind = MenuMonitorWaitKind::Back;
    // A non-slider consumes the same outer branch without the slider's inner pause
    else if (kind == "choiceNoop") result.kind = MenuMonitorWaitKind::ChoiceNoop;
    else throw std::runtime_error("Unsupported mechanical monitor wait kind");
    // A wait can retain an admitted closed root without turning its pause into an actor grant
    if (!snapshot.localMenuEligibility || snapshot.localMenuEligibility->hasMenu != true
        || (snapshot.localMenuEligibility->life != PlayerLifeState::Alive
            && snapshot.localMenuEligibility->life != PlayerLifeState::Dead)
        || !IsKnownMenuSurface(snapshot.menuSurface)
        || (result.kind != MenuMonitorWaitKind::Back && (!snapshot.isOpen || snapshot.rows.empty()
            || snapshot.selectedIndex >= snapshot.rows.size() || snapshot.rows[snapshot.selectedIndex].isSpacer))
        || snapshot.widthEditor || snapshot.instructionPositionEditor || snapshot.menuPositionEditor
        || snapshot.menuCredits || snapshot.menuUnlock || snapshot.menuOptionCallback
        || snapshot.menuPreferenceRefresh || snapshot.menuPreferenceRebuild
        || snapshot.menuDesignTransition || snapshot.menuOpenTransition) {
        throw std::runtime_error("Mechanical monitor wait requires an exclusive admitted ordinary visit");
    }
    return result;
}
}
