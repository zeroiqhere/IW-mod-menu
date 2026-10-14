#include "Renderer.h"

namespace iw::overlay {

bool Renderer::PrepareSwapChain(IDXGISwapChain* chain, IDXGISwapChain*& retiredChain) noexcept {
    if (!chain_ || chain == chain_) {
        return true;
    }
    // The presenting owner is checked before any candidate COM or context work
    if (GetCurrentThreadId() != thread_ || retiredChain) {
        return false;
    }
    DXGI_SWAP_CHAIN_DESC description{};
    if (FAILED(chain->GetDesc(&description)) || description.OutputWindow != window_) {
        return false;
    }
    ID3D11Device* candidate = nullptr;
    if (FAILED(chain->GetDevice(__uuidof(ID3D11Device), reinterpret_cast<void**>(&candidate))) || !candidate) {
        return false;
    }
    // Unsupported candidates leave the previous active renderer intact
    const bool supported = candidate->GetFeatureLevel() == D3D_FEATURE_LEVEL_11_0;
    const bool sameDevice = candidate == device_;
    candidate->Release();
    if (!supported) {
        return false;
    }

    retiredChain = chain_;
    chain_ = nullptr;
    if (sameDevice) {
        // Keep fonts and backend resources when only the back buffer owner changes
        if (target_) {
            target_->Release();
            target_ = nullptr;
        }
        chain_ = chain;
        chain_->AddRef();
    } else {
        // Reset keeps the original thread identity and does not release the retired
        // chain
        Reset();
    }
    return true;
}

} // namespace iw::overlay
