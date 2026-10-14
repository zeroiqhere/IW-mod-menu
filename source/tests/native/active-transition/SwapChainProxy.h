#pragma once
#include <atomic>
#include <dxgi.h>
#include <memory>

namespace iw::overlay::tests {

enum class ChainFailure { None, ValidationDevice, NullValidationDevice, BindDevice, NullBindDevice, BackBuffer };
struct ChainLifetime {
    bool destroyed = false;
};

// Forward real DXGI behavior except one explicit checked failure used by a
// regression
class SwapChainProxy final : public IDXGISwapChain {
  public:
    SwapChainProxy(IDXGISwapChain* chain, std::shared_ptr<ChainLifetime> lifetime, ChainFailure failure);
    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID id, void** output) override;
    ULONG STDMETHODCALLTYPE AddRef() override;
    ULONG STDMETHODCALLTYPE Release() override;
    HRESULT STDMETHODCALLTYPE SetPrivateData(REFGUID name, UINT size, const void* data) override;
    HRESULT STDMETHODCALLTYPE SetPrivateDataInterface(REFGUID name, const IUnknown* value) override;
    HRESULT STDMETHODCALLTYPE GetPrivateData(REFGUID name, UINT* size, void* data) override;
    HRESULT STDMETHODCALLTYPE GetParent(REFIID id, void** output) override;
    HRESULT STDMETHODCALLTYPE GetDevice(REFIID id, void** output) override;
    HRESULT STDMETHODCALLTYPE Present(UINT interval, UINT flags) override;
    HRESULT STDMETHODCALLTYPE GetBuffer(UINT index, REFIID id, void** output) override;
    HRESULT STDMETHODCALLTYPE SetFullscreenState(BOOL fullscreen, IDXGIOutput* target) override;
    HRESULT STDMETHODCALLTYPE GetFullscreenState(BOOL* fullscreen, IDXGIOutput** target) override;
    HRESULT STDMETHODCALLTYPE GetDesc(DXGI_SWAP_CHAIN_DESC* description) override;
    HRESULT STDMETHODCALLTYPE ResizeBuffers(UINT count, UINT width, UINT height, DXGI_FORMAT format,
                                            UINT flags) override;
    HRESULT STDMETHODCALLTYPE ResizeTarget(const DXGI_MODE_DESC* mode) override;
    HRESULT STDMETHODCALLTYPE GetContainingOutput(IDXGIOutput** output) override;
    HRESULT STDMETHODCALLTYPE GetFrameStatistics(DXGI_FRAME_STATISTICS* statistics) override;
    HRESULT STDMETHODCALLTYPE GetLastPresentCount(UINT* count) override;
    unsigned DeviceReads() const noexcept { return deviceReads_; }

  private:
    ~SwapChainProxy();
    IDXGISwapChain* chain_;
    std::shared_ptr<ChainLifetime> lifetime_;
    ChainFailure failure_;
    std::atomic<ULONG> references_{1};
    unsigned deviceReads_ = 0;
};

} // namespace iw::overlay::tests
