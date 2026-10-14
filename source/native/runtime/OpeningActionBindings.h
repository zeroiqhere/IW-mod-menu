#pragma once
#include "NativeInputRouter.h"
#include <stdexcept>
namespace iw::overlay::runtime {
// Every logical action resolves to its exact immutable saved group
inline const std::vector<int> &OpeningActionKeys(const Bindings &bindings, OpeningAction action) {
    switch (action) {
    case OpeningAction::ActionSlot1:
        return bindings.moveUp;
    case OpeningAction::ActionSlot2:
        return bindings.moveDown;
    case OpeningAction::ActionSlot3:
        return bindings.previousChoice;
    case OpeningAction::ActionSlot4:
        return bindings.nextChoice;
    case OpeningAction::Melee:
        return bindings.melee;
    case OpeningAction::Aim:
        return bindings.aim;
    case OpeningAction::Attack:
        return bindings.attack;
    case OpeningAction::Sprint:
        return bindings.sprint;
    case OpeningAction::Use:
        return bindings.select;
    case OpeningAction::Frag:
        return bindings.frag;
    case OpeningAction::SecondaryOffhand:
        return bindings.secondaryOffhand;
    case OpeningAction::Stance:
        return bindings.stance;
    case OpeningAction::Jump:
        return bindings.jump;
    }
    throw std::invalid_argument("Unknown opening action");
}
inline void ValidateMainOpeningBindings(const Bindings &bindings, const MainOpeningControls &controls) {
    if (!IsValidMainOpeningControls(controls)) {
        throw std::invalid_argument("Unsupported main opening policy");
    }
    for (auto action : controls.actions) {
        if (OpeningActionKeys(bindings, action).empty()) {
            throw std::invalid_argument("Opening action has no saved binding");
        }
    }
}
} // namespace iw::overlay::runtime
