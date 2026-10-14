#pragma once
#include <windows.h>
#include <cstdint>
#include "../../native/render/RendererDiagnostics.h"

namespace iw::overlay::tests {

// This test-only packet is absent from the retail payload exports
struct FixtureStatistics {
    std::uint32_t size = sizeof(FixtureStatistics);
    std::uint64_t presents = 0, drawn = 0, skipped = 0, resizes = 0, faults = 0;
    HWND boundWindow = nullptr;
    bool enabled = false;
};
using InitializeCall = DWORD (WINAPI*)(void*);
using StatisticsCall = BOOL (WINAPI*)(FixtureStatistics*);
using SnapshotCall = void (WINAPI*)(BOOL);
using RendererStatusCall = BOOL (WINAPI*)(RendererStatus*);

}
