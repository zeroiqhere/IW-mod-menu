#pragma once

#include <string>
#include "../shared/OverlayProtocol.h"

namespace iw::overlay {

bool StartDx11Hooks(const BootstrapConfig& config, std::string& error);
// Stop drawing without unloading a DLL or invalidating an active trampoline
void StopDx11Hooks() noexcept;

struct HookStatistics {
    std::uint64_t presents = 0;
    std::uint64_t drawn = 0;
    std::uint64_t skipped = 0;
    std::uint64_t resizes = 0;
    std::uint64_t faults = 0;
    HWND boundWindow = nullptr;
    bool enabled = false;
};

HookStatistics ReadHookStatistics() noexcept;

}
