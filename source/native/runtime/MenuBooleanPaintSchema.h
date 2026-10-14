#pragma once
#include "../shared/MenuSnapshot.h"
#include <nlohmann/json.hpp>

namespace iw::overlay::runtime {
// Both wire readers use one strict display-only parser
MenuBooleanPaint ParseMenuBooleanPaint(const nlohmann::json& value, const MenuSnapshot& snapshot);
}
