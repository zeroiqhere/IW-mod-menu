#include "SwapChainProxy.h"
#include <utility>

namespace iw::overlay::tests {

SwapChainProxy::SwapChainProxy(IDXGISwapChain* chain, std::shared_ptr<ChainLifetime> lifetime, ChainFailure failure)
    : chain_(chain), lifetime_(std::move(lifetime)), failure_(failure) {
    chain_->AddRef();
}
SwapChainProxy::~SwapChainProxy() {
    chain_->Release();
    lifetime_->destroyed = true;
}
HRESULT SwapChainProxy::QueryInterface(REFIID id, void** output) {
    if (!output) {
        return E_POINTER;
    }
    *output = nullptr;
    if (id != __uuidof(IUnknown) && id != __uuidof(IDXGIObject) && id != __uuidof(IDXGIDeviceSubObject) &&
        id != __uuidof(IDXGISwapChain)) {
        return E_NOINTERFACE;
    }
    // Each base interface retains this wrapper's single COM identity
    *output = static_cast<IDXGISwapChain*>(this);
    AddRef();
    return S_OK;
}
// References keep the forwarding wrapper alive through deferred retirement
ULONG SwapChainProxy::AddRef() { return ++references_; }
ULONG SwapChainProxy::Release() {
    const auto remaining = --references_;
    if (remaining == 0) {
        delete this;
    }
    return remaining;
}
HRESULT SwapChainProxy::SetPrivateData(REFGUID name, UINT size, const void* data) {
    return chain_->SetPrivateData(name, size, data);
}
HRESULT SwapChainProxy::SetPrivateDataInterface(REFGUID name, const IUnknown* value) {
    return chain_->SetPrivateDataInterface(name, value);
}
HRESULT SwapChainProxy::GetPrivateData(REFGUID name, UINT* size, void* data) {
    return chain_->GetPrivateData(name, size, data);
}
HRESULT SwapChainProxy::GetParent(REFIID id, void** output) { return chain_->GetParent(id, output); }
HRESULT SwapChainProxy::GetDevice(REFIID id, void** output) {
    ++deviceReads_;
    // Validation succeeds once; the subsequent binding read fails without
    // guessing a driver feature level
    if (failure_ == ChainFailure::ValidationDevice || (failure_ == ChainFailure::BindDevice && deviceReads_ > 1)) {
        if (output) {
            *output = nullptr;
        }
        return E_FAIL;
    }
    if (failure_ == ChainFailure::NullValidationDevice ||
        (failure_ == ChainFailure::NullBindDevice && deviceReads_ > 1)) {
        if (output) {
            *output = nullptr;
        }
        return S_OK;
    }
    return chain_->GetDevice(id, output);
}
// Present always reaches the real owned swapchain, including controlled
// renderer failures
HRESULT SwapChainProxy::Present(UINT interval, UINT flags) { return chain_->Present(interval, flags); }
HRESULT SwapChainProxy::GetBuffer(UINT index, REFIID id, void** output) {
    if (failure_ == ChainFailure::BackBuffer) {
        if (output) {
            *output = nullptr;
        }
        return E_FAIL;
    }
    return chain_->GetBuffer(index, id, output);
}
HRESULT SwapChainProxy::SetFullscreenState(BOOL fullscreen, IDXGIOutput* target) {
    return chain_->SetFullscreenState(fullscreen, target);
}
HRESULT SwapChainProxy::GetFullscreenState(BOOL* fullscreen, IDXGIOutput** target) {
    return chain_->GetFullscreenState(fullscreen, target);
}
HRESULT SwapChainProxy::GetDesc(DXGI_SWAP_CHAIN_DESC* description) { return chain_->GetDesc(description); }
// Resize and output queries remain real DXGI operations without injected
// failures
HRESULT SwapChainProxy::ResizeBuffers(UINT count, UINT width, UINT height, DXGI_FORMAT format, UINT flags) {
    return chain_->ResizeBuffers(count, width, height, format, flags);
}
HRESULT SwapChainProxy::ResizeTarget(const DXGI_MODE_DESC* mode) { return chain_->ResizeTarget(mode); }
HRESULT SwapChainProxy::GetContainingOutput(IDXGIOutput** output) { return chain_->GetContainingOutput(output); }
HRESULT SwapChainProxy::GetFrameStatistics(DXGI_FRAME_STATISTICS* statistics) {
    return chain_->GetFrameStatistics(statistics);
}
HRESULT SwapChainProxy::GetLastPresentCount(UINT* count) { return chain_->GetLastPresentCount(count); }

} // namespace iw::overlay::tests
