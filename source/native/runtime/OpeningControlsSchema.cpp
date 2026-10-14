#include "OpeningControlsSchema.h"
#include <stdexcept>
namespace iw::overlay::runtime {
namespace {
OpeningAction Action(const nlohmann::json &value) {
    if (!value.is_number_unsigned() || value.get<std::uint64_t>() >= OpeningActionCount) {
        throw std::runtime_error("An opening action requires a supported integer ID");
    }
    return static_cast<OpeningAction>(value.get<unsigned>());
}
} // namespace
MainOpeningControls ParseMainOpeningControls(const nlohmann::json &value, const OpeningActionLabels *labels) {
    // A supplied descriptor is strict even while its menu is closed
    if (!value.is_object() || value.size() != 2 || !value.contains("revision") ||
        !value.contains("actions") || !value.at("revision").is_number_unsigned() ||
        !value.at("actions").is_array() || value.at("actions").empty() || value.at("actions").size() > 3) {
        throw std::runtime_error("Invalid main opening descriptor");
    }
    MainOpeningControls controls;
    controls.revision = value.at("revision").get<std::uint64_t>();
    for (const auto &action : value.at("actions")) {
        controls.actions.push_back(Action(action));
    }
    if (!IsValidMainOpeningControls(controls)) {
        throw std::runtime_error("Unsupported main opening tuple");
    }
    // Private runtime callers prove every configured group before publishing even a closed snapshot
    if (labels) {
        for (auto action : controls.actions) {
            if ((*labels)[static_cast<unsigned>(action)].empty()) {
                throw std::runtime_error("Policy claims an unverified opening action");
            }
        }
    }
    return controls;
}
ClosedMenuOpeningPolicies ParseOpeningPolicies(const nlohmann::json &value, const OpeningActionLabels *labels) {
    // A supplied pair owns every required field and cannot fall back to a default Quick tuple
    if (!value.is_object() || value.size() != 4 || !value.contains("revision") ||
        !value.contains("main") || !value.contains("quick") || !value.contains("quickDisabled") ||
        !value.at("revision").is_number_unsigned() || !value.at("quickDisabled").is_boolean()) {
        throw std::runtime_error("Invalid dual opening descriptor");
    }
    ClosedMenuOpeningPolicies policies;
    policies.revision = value.at("revision").get<std::uint64_t>();
    // Both tuples use the same shape and private availability boundary as legacy Main
    policies.main = ParseMainOpeningControls(value.at("main"), labels);
    policies.quick = ParseMainOpeningControls(value.at("quick"), labels);
    policies.quickMenuDisabled = value.at("quickDisabled").get<bool>();
    if (!IsValidClosedMenuOpeningPolicies(policies)) {
        throw std::runtime_error("Unsupported dual opening policies");
    }
    return policies;
}
bool IsOpeningSelectorRefreshContext(const MenuSnapshot &snapshot) noexcept {
    // Only the complete source Menu selector may wait without rebuilding its rows
    if (!snapshot.isOpen || snapshot.isRootMenu || snapshot.selectedIndex != 0
        || !snapshot.mainOpeningControls || (snapshot.rows.size() != 16 && snapshot.rows.size() != 17)
        || snapshot.rows[0].label != "Menu" || snapshot.rows[1].label != "Bind Slot"
        || !snapshot.rows[2].isSpacer) return false;
    const unsigned first = snapshot.rows.size() == 17 ? 4 : 3;
    if (first == 4 && (snapshot.rows[3].label != "None" || snapshot.rows[3].isSpacer
        || snapshot.rows[3].openingAction)) return false;
    // Private key captions do not change the typed action order or capability truth
    for (unsigned id = 0; id < OpeningActionCount; ++id)
        if (snapshot.rows[first + id].isSpacer
            || snapshot.rows[first + id].openingAction != static_cast<OpeningAction>(id)) return false;
    return true;
}
void ParseOpeningRowMetadata(const nlohmann::json &value, MenuRow &row, const OpeningActionLabels *labels) {
    if (value.contains("isSpacer")) {
        if (!value.at("isSpacer").is_boolean()) {
            throw std::runtime_error("A spacer flag must be a boolean");
        }
        row.isSpacer = value.at("isSpacer").get<bool>();
    }
    if (value.contains("openingAction")) {
        row.openingAction = Action(value.at("openingAction"));
    }
    if (row.isSpacer && (!row.label.empty() || !row.value.empty() || row.enabled || row.openingAction)) {
        throw std::runtime_error("A spacer must be blank and disabled without an opening action");
    }
    if (!row.openingAction) {
        return;
    }
    const auto action = *row.openingAction;
    // Syntax-only callers keep logical captions instead of inventing physical key names
    row.label = OpeningActionCaption(action);
    if (!labels) {
        return;
    }
    const auto &saved = (*labels)[static_cast<unsigned>(action)];
    if (saved.empty()) {
        if (row.enabled || row.value != "UNAVAILABLE") {
            throw std::runtime_error("Row claims an unverified opening action");
        }
        return;
    }
    row.label = saved;
}
} // namespace iw::overlay::runtime
