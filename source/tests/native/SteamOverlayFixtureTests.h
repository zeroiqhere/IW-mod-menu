#pragma once
#include <filesystem>

namespace iw::overlay::tests {
// Guard checks create no window, graphics device, or Steam connection
int RunSteamOverlayGuards(const std::filesystem::path& logPath);
}
