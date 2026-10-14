#include "instructions/InstructionHudPresence.h"
#include "Dx11Hooks.h"
#include "Renderer.h"
#include "RendererDiagnostics.h"

#include <array>
#include <atomic>
#include <mutex>
#include <new>
#include <MinHook.h>
#include <d3d11.h>

namespace iw::overlay {
namespace {

using PresentCall = HRESULT (STDMETHODCALLTYPE*)(IDXGISwapChain*, UINT, UINT);
using ResizeCall = HRESULT (STDMETHODCALLTYPE*)(IDXGISwapChain*, UINT, UINT, UINT, DXGI_FORMAT, UINT);

// Hook state remains resident because the host can still hold a trampoline at process exit
struct HookState {
    BootstrapConfig config{};
    std::wstring title;
    std::mutex mutex;
    Renderer renderer;
    PresentCall present = nullptr;
    ResizeCall resize = nullptr;
    bool drawing = false;
    unsigned resizing = 0;
    HookStatistics statistics;
    std::uint64_t presentedFrames = 0;
    std::atomic<bool> initialized{false};
    std::atomic<std::uint64_t> drawn{0}, faults{0};
    std::atomic<std::uint32_t> featureLevel{0}, width{0}, height{0};
};
std::atomic<HookState*> resident{nullptr};

bool ReadTitle(HWND window, std::wstring& title, UINT timeout = 2) {
    std::array<wchar_t, 512> buffer{};
    DWORD_PTR length = 0;
    // A bounded message prevents a busy window thread from trapping the render thread
    if (!SendMessageTimeoutW(window, WM_GETTEXT, buffer.size(), reinterpret_cast<LPARAM>(buffer.data()),
            SMTO_ABORTIFHUNG | SMTO_BLOCK, timeout, &length) || length == 0 || length >= buffer.size() - 1) {
        return false;
    }
    title.assign(buffer.data(), static_cast<std::size_t>(length));
    return true;
}

bool IsTarget(HookState& state, IDXGISwapChain* chain) {
    DXGI_SWAP_CHAIN_DESC description{};
    if (FAILED(chain->GetDesc(&description)) || description.OutputWindow != state.config.gameWindow
        || GetForegroundWindow() != state.config.gameWindow) { return false; }
    DWORD process = 0;
    if (!GetWindowThreadProcessId(description.OutputWindow, &process)
        || process != state.config.expectedProcessId || process != GetCurrentProcessId()) { return false; }
    std::wstring current;
    return ReadTitle(description.OutputWindow, current) && current == state.title;
}

HRESULT STDMETHODCALLTYPE PresentHook(IDXGISwapChain* chain, UINT interval, UINT flags) {
    HookState* state = resident.load(std::memory_order_acquire);
    // Hooks are enabled only after both original function pointers are stored
    if (!state || !state->present) { return E_UNEXPECTED; }
    bool drawn = false;
    IDXGISwapChain* retiredChain = nullptr;
    {
        std::lock_guard lock(state->mutex);
        ++state->statistics.presents;
        if (state->resizing == 0 && (flags & DXGI_PRESENT_TEST) == 0) {
            try {
                auto snapshot = state->drawing ? GetOverlaySnapshot() : nullptr;
                if (!snapshot || !HasDrawableOverlayContent(*snapshot)) {
                    // Cleanup stays on the owner thread; its old chain remains alive through Present
                    retiredChain = state->renderer.CloseOnPresentThread(snapshot.get());
                    state->statistics.boundWindow = state->renderer.BoundWindow();
                } else if (IsTarget(*state, chain)) {
                    drawn = state->renderer.Draw(chain, *snapshot, retiredChain);
                    state->statistics.boundWindow = state->renderer.BoundWindow();
                }
            } catch (...) {
                // A renderer failure disables drawing but cannot prevent the host's Present call
                ++state->statistics.faults;
                state->drawing = false;
                state->statistics.enabled = false;
            }
        }
        if (drawn) { ++state->statistics.drawn; }
        else { ++state->statistics.skipped; }
    }
    HRESULT result = state->present(chain, interval, flags);
    if (retiredChain) { retiredChain->Release(); }
    {
        std::lock_guard lock(state->mutex);
        if (drawn && result == S_OK) { ++state->presentedFrames; }
        if (result == DXGI_ERROR_DEVICE_REMOVED || result == DXGI_ERROR_DEVICE_RESET) {
            if (state->drawing) { ++state->statistics.faults; }
            state->drawing = false;
            state->statistics.enabled = false;
        }
        // Readiness follows a completed successful Present, rather than a submitted draw call
        state->featureLevel.store(state->renderer.FeatureLevel(), std::memory_order_relaxed);
        state->width.store(state->renderer.Width(), std::memory_order_relaxed);
        state->height.store(state->renderer.Height(), std::memory_order_relaxed);
        state->faults.store(state->statistics.faults, std::memory_order_relaxed);
        state->drawn.store(state->presentedFrames, std::memory_order_release);
        state->initialized.store(state->drawing && state->resizing == 0 && state->renderer.Initialized(), std::memory_order_release);
    }
    return result;
}

HRESULT STDMETHODCALLTYPE ResizeHook(IDXGISwapChain* chain, UINT count, UINT width, UINT height,
        DXGI_FORMAT format, UINT flags) {
    HookState* state = resident.load(std::memory_order_acquire);
    if (!state || !state->resize) { return E_UNEXPECTED; }
    {
        std::lock_guard lock(state->mutex);
        ++state->statistics.resizes;
        ++state->resizing;
        state->initialized.store(false, std::memory_order_release);
        // Drop only the renderer's reference before the host replaces its buffers
        state->renderer.BeforeResize(chain);
    }
    HRESULT result = state->resize(chain, count, width, height, format, flags);
    {
        std::lock_guard lock(state->mutex);
        --state->resizing;
    }
    return result;
}

LRESULT CALLBACK ScratchProcedure(HWND window, UINT message, WPARAM word, LPARAM data) {
    return DefWindowProcW(window, message, word, data);
}

class ScratchDevice final {
public:
    ~ScratchDevice() {
        if (chain) { chain->Release(); }
        if (context) { context->Release(); }
        if (device) { device->Release(); }
        if (window) { DestroyWindow(window); }
        if (atom) { UnregisterClassW(MAKEINTATOM(atom), instance); }
    }
    bool Create(std::string& error) {
        instance = GetModuleHandleW(nullptr);
        WNDCLASSW registration{};
        registration.lpfnWndProc = ScratchProcedure;
        registration.hInstance = instance;
        registration.lpszClassName = L"IWOverlayHiddenDx11Bootstrap";
        atom = RegisterClassW(&registration);
        if (!atom) { error = "The hidden D3D11 bootstrap class could not be registered"; return false; }
        // This window is never shown and never hosts menu content
        window = CreateWindowExW(WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW, MAKEINTATOM(atom), L"",
            WS_POPUP, 0, 0, 8, 8, nullptr, nullptr, instance, nullptr);
        if (!window) { error = "The hidden D3D11 bootstrap window could not be created"; return false; }
        DXGI_SWAP_CHAIN_DESC description{};
        description.BufferDesc.Width = 8;
        description.BufferDesc.Height = 8;
        description.BufferDesc.Format = DXGI_FORMAT_R8G8B8A8_UNORM;
        description.SampleDesc.Count = 1;
        description.BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT;
        description.BufferCount = 1;
        description.OutputWindow = window;
        description.Windowed = TRUE;
        description.SwapEffect = DXGI_SWAP_EFFECT_DISCARD;
        const D3D_FEATURE_LEVEL requested = D3D_FEATURE_LEVEL_11_0;
        D3D_FEATURE_LEVEL actual{};
        HRESULT result = D3D11CreateDeviceAndSwapChain(nullptr, D3D_DRIVER_TYPE_HARDWARE, nullptr, 0,
            &requested, 1, D3D11_SDK_VERSION, &description, &chain, &device, &actual, &context);
        if (FAILED(result) || actual != requested) {
            error = "A verified Direct3D 11.0 bootstrap device is unavailable"; return false;
        }
        if (FAILED(chain->Present(0, DXGI_PRESENT_TEST))) {
            error = "The D3D11 bootstrap driver cannot test its hidden swapchain"; return false;
        }
        return true;
    }
    IDXGISwapChain* chain = nullptr;
private:
    HINSTANCE instance = nullptr;
    ATOM atom = 0;
    HWND window = nullptr;
    ID3D11Device* device = nullptr;
    ID3D11DeviceContext* context = nullptr;
};

bool ExecutableAddress(void* address) {
    MEMORY_BASIC_INFORMATION region{};
    if (!address || VirtualQuery(address, &region, sizeof(region)) != sizeof(region)
        || region.State != MEM_COMMIT || (region.Protect & PAGE_GUARD)) { return false; }
    DWORD access = region.Protect & 0xff;
    return access == PAGE_EXECUTE || access == PAGE_EXECUTE_READ
        || access == PAGE_EXECUTE_READWRITE || access == PAGE_EXECUTE_WRITECOPY;
}

}

bool StartDx11Hooks(const BootstrapConfig& config, std::string& error) {
    if (resident.load(std::memory_order_acquire)) { error = "Overlay hooks already have a resident owner"; return false; }
    auto* state = new (std::nothrow) HookState;
    if (!state) { error = "Overlay hook state could not be allocated"; return false; }
    state->config = config;
    if (!ReadTitle(config.gameWindow, state->title, 50)) {
        delete state; error = "The selected window title could not be checked"; return false;
    }
    ScratchDevice scratch;
    if (!scratch.Create(error)) { delete state; return false; }
    // These slots belong to the documented IDXGISwapChain COM interface
    void** methods = *reinterpret_cast<void***>(scratch.chain);
    void* presentTarget = methods[8];
    void* resizeTarget = methods[13];
    if (!ExecutableAddress(presentTarget) || !ExecutableAddress(resizeTarget)) {
        delete state; error = "The D3D11 method addresses are not executable"; return false;
    }
    MH_STATUS status = MH_Initialize();
    // This checks only this linked MinHook instance, not Steam or another module's hooks
    if (status != MH_OK) { delete state; error = "This overlay's MinHook instance could not be initialized"; return false; }
    status = MH_CreateHook(presentTarget, reinterpret_cast<void*>(&PresentHook), reinterpret_cast<void**>(&state->present));
    if (status != MH_OK) {
        MH_Uninitialize(); delete state; error = "The D3D11 Present method cannot be hooked safely"; return false;
    }
    status = MH_CreateHook(resizeTarget, reinterpret_cast<void*>(&ResizeHook), reinterpret_cast<void**>(&state->resize));
    if (status != MH_OK) {
        MH_RemoveHook(presentTarget); MH_Uninitialize(); delete state;
        error = "The D3D11 ResizeBuffers method cannot be hooked safely"; return false;
    }
    // Publish stable trampoline pointers before any thread can enter a hook
    resident.store(state, std::memory_order_release);
    if (MH_QueueEnableHook(presentTarget) != MH_OK || MH_QueueEnableHook(resizeTarget) != MH_OK
        || MH_ApplyQueued() != MH_OK) {
        error = "D3D11 hook activation failed; the payload must remain resident";
        return false;
    }
    {
        std::lock_guard lock(state->mutex);
        state->drawing = true;
        state->statistics.enabled = true;
    }
    return true;
}

void StopDx11Hooks() noexcept {
    if (auto* state = resident.load(std::memory_order_acquire)) {
        std::lock_guard lock(state->mutex);
        state->drawing = false;
        state->statistics.enabled = false;
        state->initialized.store(false, std::memory_order_release);
    }
}

HookStatistics ReadHookStatistics() noexcept {
    if (auto* state = resident.load(std::memory_order_acquire)) {
        std::lock_guard lock(state->mutex);
        return state->statistics;
    }
    return {};
}

RendererStatus ReadRendererStatus() noexcept {
    if (auto* state = resident.load(std::memory_order_acquire)) {
        RendererStatus result;
        result.initialized = state->initialized.load(std::memory_order_acquire);
        result.drawnFrames = state->drawn.load(std::memory_order_acquire);
        result.faults = state->faults.load(std::memory_order_relaxed);
        result.featureLevel = state->featureLevel.load(std::memory_order_relaxed);
        result.width = state->width.load(std::memory_order_relaxed);
        result.height = state->height.load(std::memory_order_relaxed);
        return result;
    }
    return {};
}

}
