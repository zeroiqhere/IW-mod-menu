#pragma once
#include "../../shared/MenuSnapshot.h"
#include <nlohmann/json.hpp>

namespace iw::overlay::runtime {
// Both real readers use the same strict copied receipt boundary
MenuUseContinuation ParseMenuUseContinuation(const nlohmann::json& value,
    const MenuSnapshot& snapshot, std::uint64_t acknowledgedSequence);
void ValidateMenuDesignUseContinuation(const MenuSnapshot& snapshot);
}
