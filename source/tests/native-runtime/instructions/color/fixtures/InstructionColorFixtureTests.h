#pragma once
#include "InstructionColorFixtureExpected.h"
#include "../../../../../native/runtime/RuntimeSchema.h"
#include "../../../../../native/render/instructions/InstructionHudLifetime.h"
#include "../../../../../native/render/instructions/color/InstructionHudColorHistory.h"
#include <filesystem>
namespace iw::overlay::tests::instruction_color {
void CheckInventory(const std::filesystem::path& directory);
std::string ReadPacket(const std::filesystem::path& path);
MenuSnapshot ReadAndCheck(const std::filesystem::path& directory,const Expected& expected);
void CheckPaintChannels(const Expected& expected,const std::optional<InstructionHudColors>& colors,bool journal=false);
void ActualInstructionColorsJoinImmutableCreationAndCurrentTargetChannels(const std::filesystem::path& directory);
}
