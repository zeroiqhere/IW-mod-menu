#pragma once
#include "../shared/MenuSnapshot.h"
#include "OpeningActionLabels.h"
#include <nlohmann/json.hpp>
namespace iw::overlay::runtime {
MainOpeningControls ParseMainOpeningControls(const nlohmann::json &value,
                                             const OpeningActionLabels *labels = nullptr);
ClosedMenuOpeningPolicies ParseOpeningPolicies(const nlohmann::json &value,
                                               const OpeningActionLabels *labels = nullptr);
bool IsOpeningSelectorRefreshContext(const MenuSnapshot &snapshot) noexcept;
void ParseOpeningRowMetadata(const nlohmann::json &value, MenuRow &row,
                             const OpeningActionLabels *labels = nullptr);
} // namespace iw::overlay::runtime
