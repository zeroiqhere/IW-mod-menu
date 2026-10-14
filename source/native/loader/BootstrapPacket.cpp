#include "BootstrapPacket.h"

#include <algorithm>
#include <iterator>
#include <stdexcept>

namespace iw::loader {

iw::overlay::BootstrapConfig MakeBootstrapPacket(DWORD processId, HWND window, std::wstring_view directory) {
    iw::overlay::BootstrapConfig config{};
    if (processId == 0 || window == nullptr) {
        throw std::runtime_error("The bootstrap packet requires an exact process and window");
    }
    // The final slot remains zero so the copied path is always terminated
    if (directory.empty() || directory.size() >= std::size(config.sessionDirectory)
        || directory.find(L'\0') != std::wstring_view::npos) {
        throw std::runtime_error("The private session directory exceeds packet bounds or contains an invalid character");
    }
    config.size = sizeof(config);
    config.version = iw::overlay::ProtocolVersion;
    config.expectedProcessId = processId;
    config.gameWindow = window;
    std::copy(directory.begin(), directory.end(), config.sessionDirectory);
    return config;
}

}
