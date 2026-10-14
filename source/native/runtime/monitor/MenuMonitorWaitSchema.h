#pragma once
#include "../../shared/MenuSnapshot.h"
#include <nlohmann/json.hpp>

namespace iw::overlay::runtime {
MenuMonitorWait ParseMenuMonitorWait(const nlohmann::json& value, const MenuSnapshot& snapshot);
}
