#pragma once
#include "../../shared/MenuSnapshot.h"
#include <nlohmann/json.hpp>

namespace iw::overlay::runtime {
MenuMonitorContinuation ParseMenuMonitorContinuation(const nlohmann::json& value,
    const MenuSnapshot& snapshot, std::uint64_t acknowledgedSequence);
}
