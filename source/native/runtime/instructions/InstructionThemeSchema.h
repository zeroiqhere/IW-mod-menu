#pragma once
#include "../../shared/MenuSnapshot.h"
#include <nlohmann/json.hpp>
namespace iw::overlay::runtime {
InstructionThemeState ParseInstructionTheme(const nlohmann::json& value, const MenuSnapshot& snapshot);
}
