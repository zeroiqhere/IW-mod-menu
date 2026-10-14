#pragma once
#include "ChromeThemePanelTests.h"
#include <filesystem>
namespace iw::overlay::tests {
void AddActualChromeThemePanelChecks(std::vector<std::pair<std::string,std::function<void()>>>& checks,const std::filesystem::path& directory);
}
