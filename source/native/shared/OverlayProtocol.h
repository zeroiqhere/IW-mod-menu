#pragma once

#include <windows.h>
#include <cstdint>
#include "MenuSnapshot.h"

namespace iw::overlay {

// This packet is copied before the bootstrap helper releases its remote buffer
inline constexpr std::uint32_t ProtocolVersion = 1;
struct BootstrapConfig {
    std::uint32_t size;
    std::uint32_t version;
    DWORD expectedProcessId;
    HWND gameWindow;
    wchar_t sessionDirectory[1024];
};

bool RuntimeStart(const BootstrapConfig& config, std::string& error);
std::shared_ptr<const MenuSnapshot> GetOverlaySnapshot();
void RuntimeStop();

}
