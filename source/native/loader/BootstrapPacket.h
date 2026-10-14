#pragma once

#include "../shared/OverlayProtocol.h"
#include <string_view>

namespace iw::loader {

// Packet validation completes before a remote DLL or thread can be created
iw::overlay::BootstrapConfig MakeBootstrapPacket(DWORD processId, HWND window, std::wstring_view directory);

}
