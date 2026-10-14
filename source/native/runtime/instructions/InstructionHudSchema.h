#pragma once
#include "../OpeningActionLabels.h"
#include "../../shared/MenuSnapshot.h"
#include <nlohmann/json.hpp>
namespace iw::overlay::runtime {
std::optional<InstructionHud> ParseInstructionHud(const nlohmann::json& value,
    const MenuSnapshot& snapshot, const OpeningActionLabels* labels = nullptr);
}
