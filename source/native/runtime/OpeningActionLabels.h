#pragma once
#include "../shared/OpenControls/OpeningAction.h"
#include <array>
#include <string>
namespace iw::overlay::runtime {
// Empty entries mean no exact saved group was proved and never imply a default key
using OpeningActionLabels = std::array<std::string, OpeningActionCount>;
} // namespace iw::overlay::runtime
