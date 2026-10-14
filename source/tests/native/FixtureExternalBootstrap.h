#pragma once
#include <windows.h>
#include <filesystem>
#include <functional>
#include <ostream>

namespace iw::overlay::tests {

// An external helper loads only the test DLL into this explicitly waiting fixture
HMODULE WaitForExternalBootstrap(HWND window, const std::filesystem::path& log,
    std::ostream& output, const std::function<void()>& pumpFrame);

}
