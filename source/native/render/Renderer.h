#include "instructions/InstructionHudLifetime.h"
#include "instructions/color/InstructionHudColorHistory.h"
#pragma once

#include <d3d11.h>
#include <dxgi.h>
#include "../shared/OverlayProtocol.h"
#include "WidthAnimation.h"
#include "chrome/BasicScrollerHistory.h"
#include "chrome/theme/ChromeColorHistory.h"
#include "ScrollPresentation.h"
#include "credits/CreditsPresentationClock.h"

struct ImGuiContext;

namespace iw::overlay {

// One renderer remains bound to one swap chain and one presenting thread
class Renderer final {
public:
    Renderer() = default;
    Renderer(const Renderer&) = delete;
    Renderer& operator=(const Renderer&) = delete;
    ~Renderer();

    // The caller supplies an empty retirement slot and releases it after the original Present
    bool Draw(IDXGISwapChain* chain, const MenuSnapshot& snapshot, IDXGISwapChain*& retiredChain);
    void BeforeResize(IDXGISwapChain* chain) noexcept;
    // Return the old owned chain reference for release after the original Present finishes
    IDXGISwapChain* CloseOnPresentThread(const MenuSnapshot* closedSnapshot = nullptr) noexcept;
    HWND BoundWindow() const noexcept { return window_; }
    bool Initialized() const noexcept { return backendReady_ && target_ != nullptr; }
    std::uint32_t FeatureLevel() const noexcept { return featureLevel_; }
    std::uint32_t Width() const noexcept { return width_; }
    std::uint32_t Height() const noexcept { return height_; }

private:
    bool PrepareSwapChain(IDXGISwapChain* chain, IDXGISwapChain*& retiredChain) noexcept;
    bool Bind(IDXGISwapChain* chain);
    bool CreateTarget(IDXGISwapChain* chain);
    void Reset() noexcept;

    // Animation belongs to the same presenting thread as the private drawing context
    // Source chrome colors outlive row redraws and graphics resource resets
    BasicScrollerHistory basicScrollerHistory_;
    ChromeColorHistory chromeColorHistory_;
    // Logical footer ownership survives graphics reset and stays on the presenting thread
    InstructionHudLifetime instructionHudLifetime_;
    InstructionHudColorHistory instructionHudColorHistory_;
    WidthAnimation widthAnimation_;
    ScrollPresentation scrollPresentation_;
    credits::PresentationClock creditsClock_;
    IDXGISwapChain* chain_ = nullptr;
    ID3D11Device* device_ = nullptr;
    ID3D11DeviceContext* context_ = nullptr;
    ID3D11RenderTargetView* target_ = nullptr;
    ImGuiContext* imgui_ = nullptr;
    HWND window_ = nullptr;
    DWORD thread_ = 0;
    bool backendReady_ = false;
    LARGE_INTEGER previousFrame_{};
    LARGE_INTEGER frequency_{};
    std::uint32_t featureLevel_ = 0, width_ = 0, height_ = 0;
};

}
