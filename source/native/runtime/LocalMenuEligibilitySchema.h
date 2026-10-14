#pragma once
#include "../shared/QuickMenu/LocalMenuEligibility.h"
#include <nlohmann/json.hpp>

namespace iw::overlay::runtime {
// Runtime and offline preview readers share the same strict admission boundary
LocalMenuEligibility ParseLocalMenuEligibility(const nlohmann::json& value);
MenuSurface ParseMenuSurface(const nlohmann::json& value);
}
