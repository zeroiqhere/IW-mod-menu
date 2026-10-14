#pragma once
#include "FooterWithdrawalFixtureExpected.h"
#include "../../../../../native/render/instructions/InstructionHudLifetime.h"
#include "../../../../../native/render/instructions/color/InstructionHudColorHistory.h"
#include <filesystem>
#include <ostream>
namespace iw::overlay::tests::footer_withdrawal {
void CheckInventory(const std::filesystem::path& directory);
MenuSnapshot ReadAndCheck(const std::filesystem::path& directory,const Expected& expected);
void CheckColors(const Expected& expected,const std::optional<InstructionHudColors>& colors);
void ActualFooterWithdrawalsJoinSuppressedPaintAndRetainedHistory(const std::filesystem::path& directory,std::ostream* dump=nullptr);
}
