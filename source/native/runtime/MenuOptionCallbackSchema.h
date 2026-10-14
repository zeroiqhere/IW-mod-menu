#pragma once
#include "../shared/MenuSnapshot.h"
#include <nlohmann/json.hpp>

namespace iw::overlay::runtime {
// Both readers use the same strict descriptor and ordinary-menu boundary
MenuOptionCallback ParseMenuOptionCallback(const nlohmann::json& value, const MenuSnapshot& snapshot);
}
