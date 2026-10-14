#pragma once
#include "ChromeThemeFixtureExpected.h"
#include "../../../../../native/render/chrome/theme/ChromeColorHistory.h"
#include <filesystem>
#include <ostream>
namespace iw::overlay::tests::chrome_theme {
void CheckInventory(const std::filesystem::path& directory);
MenuSnapshot ReadAndCheck(const std::filesystem::path& directory,const Expected& expected);
void CheckColors(const Expected& expected,const std::optional<ChromeColorFrame>& colors);
void ActualChromeThemesJoinCreationScopesAndIndependentRoleColors(const std::filesystem::path& directory,std::ostream* dump = nullptr);
}
