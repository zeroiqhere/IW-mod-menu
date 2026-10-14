#pragma once
#include "InstructionThemeFixtureExpected.h"
#include "../../../../../native/runtime/RuntimeSchema.h"
#include "../../../../../native/render/instructions/InstructionHudLifetime.h"
#include "../../../../../native/render/instructions/color/InstructionHudColorHistory.h"
#include <filesystem>
namespace iw::overlay::tests::instruction_theme {
void CheckInventory(const std::filesystem::path& directory);
MenuSnapshot ReadAndCheck(const std::filesystem::path& directory,const Expected& expected);
void CheckColors(const Expected& expected,const std::optional<InstructionHudColors>& colors);
void ActualInstructionThemeJournalsJoinOwnedEpochsAndIndependentLinearColors(const std::filesystem::path& directory);
}
