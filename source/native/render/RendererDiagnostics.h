#pragma once
#include <cstdint>

namespace iw::overlay {

// These cached values describe drawing and contain no engine or player data
struct RendererStatus {
    bool initialized = false;
    std::uint64_t drawnFrames = 0;
    std::uint64_t faults = 0;
    std::uint32_t featureLevel = 0;
    std::uint32_t width = 0;
    std::uint32_t height = 0;
};

// The worker reads atomics without waiting for the render lock or touching D3D11
RendererStatus ReadRendererStatus() noexcept;

}
