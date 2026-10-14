#include "ActiveTransitionChecks.h"
#include "active-transition/RendererFailureChecks.h"

#include <atomic>
#include <stdexcept>
#include <thread>

namespace iw::overlay::tests {
namespace {

template <class T> void Release(T*& value) {
    if (value) {
        value->Release();
        value = nullptr;
    }
}

FixtureStatistics Statistics(StatisticsCall read) {
    FixtureStatistics result{};
    if (!read(&result)) {
        throw std::runtime_error("Active transition statistics could not be read");
    }
    return result;
}

RendererStatus Diagnostics(RendererStatusCall read) {
    RendererStatus result{};
    if (!read(&result)) {
        throw std::runtime_error("Active transition diagnostics could not be read");
    }
    return result;
}

bool SameDeviceChain(HWND window, ID3D11Device* device, IDXGISwapChain** output) {
    IDXGIDevice* dxgiDevice = nullptr;
    IDXGIAdapter* adapter = nullptr;
    IDXGIFactory* factory = nullptr;
    auto result = device->QueryInterface(__uuidof(IDXGIDevice), reinterpret_cast<void**>(&dxgiDevice));
    if (SUCCEEDED(result)) {
        result = dxgiDevice->GetAdapter(&adapter);
    }
    if (SUCCEEDED(result)) {
        result = adapter->GetParent(__uuidof(IDXGIFactory), reinterpret_cast<void**>(&factory));
    }
    DXGI_SWAP_CHAIN_DESC description{};
    description.BufferDesc.Width = 960;
    description.BufferDesc.Height = 720;
    description.BufferDesc.Format = DXGI_FORMAT_R8G8B8A8_UNORM;
    description.SampleDesc.Count = 1;
    description.BufferCount = 1;
    description.BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT;
    description.OutputWindow = window;
    description.Windowed = TRUE;
    description.SwapEffect = DXGI_SWAP_EFFECT_DISCARD;
    if (SUCCEEDED(result)) {
        result = factory->CreateSwapChain(device, &description, output);
    }
    Release(factory);
    Release(adapter);
    Release(dxgiDevice);
    return SUCCEEDED(result) && *output;
}

void Trace(std::ostream& log, const std::string& text) {
    log << "TRACE " << text << '\n';
    log.flush();
}

HRESULT LoggedPresent(IDXGISwapChain* chain, HWND window, StatisticsCall statistics, RendererStatusCall diagnostics,
                      std::ostream& log) {
    const auto before = Statistics(statistics);
    log << "INFO Before owned Present foreground " << reinterpret_cast<std::uintptr_t>(GetForegroundWindow())
        << " target " << reinterpret_cast<std::uintptr_t>(window) << " enabled " << before.enabled << '\n';
    log.flush();
    const auto result = chain->Present(0, 0);
    const auto after = Statistics(statistics);
    const auto status = Diagnostics(diagnostics);
    log << "INFO After owned Present result " << std::hex << result << std::dec << " draws " << before.drawn << "->"
        << after.drawn << " faults " << after.faults << " initialized " << status.initialized << " enabled "
        << after.enabled << " foreground " << reinterpret_cast<std::uintptr_t>(GetForegroundWindow()) << " bound "
        << reinterpret_cast<std::uintptr_t>(after.boundWindow) << '\n';
    log.flush();
    return result;
}

HRESULT ForeignThreadPresent(IDXGISwapChain* chain, std::ostream& log) {
    std::atomic<bool> finished{false};
    HRESULT result = E_PENDING;
    std::thread worker([&] {
        result = chain->Present(0, 0);
        finished.store(true, std::memory_order_release);
    });
    const auto deadline = GetTickCount64() + 5000;
    // Keep the owned window responsive while DXGI waits on its message thread
    while (!finished.load(std::memory_order_acquire)) {
        MSG message{};
        while (PeekMessageW(&message, nullptr, 0, 0, PM_REMOVE)) {
            TranslateMessage(&message);
            DispatchMessageW(&message);
        }
        if (GetTickCount64() >= deadline) {
            Trace(log, "Other-thread original Present exceeded the owned test deadline");
            ExitProcess(4);
        }
        Sleep(1);
    }
    worker.join();
    return result;
}

} // namespace

void CheckActiveTransitions(HWND window, ATOM windowClass, IDXGISwapChain*& chain, ID3D11Device*& device,
                            ID3D11DeviceContext*& context, const ChainFactory& create, StatisticsCall statistics,
                            RendererStatusCall diagnostics, SnapshotCall snapshot, InitializeCall stop,
                            const TransitionCheck& check, std::ostream& log) {
    auto before = Statistics(statistics);
    auto result = LoggedPresent(chain, window, statistics, diagnostics, log);
    check(SUCCEEDED(result) && Statistics(statistics).drawn == before.drawn + 1,
          "Active replacement begins with a real menu draw");

    IDXGISwapChain* replacement = nullptr;
    ID3D11Device* replacementDevice = nullptr;
    ID3D11DeviceContext* replacementContext = nullptr;
    check(create(window, &replacement, &replacementDevice, &replacementContext),
          "A new owned device and swapchain use the exact target HWND");
    before = Statistics(statistics);
    result = ForeignThreadPresent(replacement, log);
    check(SUCCEEDED(result) && Statistics(statistics).drawn == before.drawn,
          "Another presenting thread cannot adopt the active replacement");
    result = LoggedPresent(chain, window, statistics, diagnostics, log);
    check(SUCCEEDED(result) && Statistics(statistics).drawn == before.drawn + 1,
          "A rejected thread leaves the original active renderer usable");

    before = Statistics(statistics);
    Trace(log, "Before same-owner active replacement Present");
    result = LoggedPresent(replacement, window, statistics, diagnostics, log);
    Trace(log, "After same-owner active replacement Present");
    check(SUCCEEDED(result) && Statistics(statistics).drawn == before.drawn + 1 &&
              Statistics(statistics).presents == before.presents + 1 && Diagnostics(diagnostics).initialized,
          "An open menu draws the new same-HWND swapchain without closing");

    // Release only fixture-owned old resources after the observed handoff
    context->ClearState();
    context->Flush();
    Release(chain);
    Release(context);
    Release(device);
    chain = replacement;
    device = replacementDevice;
    context = replacementContext;

    for (unsigned cycle = 0; cycle < 3; ++cycle) {
        IDXGISwapChain* sameDevice = nullptr;
        check(SameDeviceChain(window, device, &sameDevice),
              "A replacement swapchain shares the current owned D3D11 device");
        before = Statistics(statistics);
        result = LoggedPresent(sameDevice, window, statistics, diagnostics, log);
        const auto after = Statistics(statistics);
        log << "INFO Same-device cycle " << cycle << " result " << std::hex << result << std::dec << " draws "
            << before.drawn << "->" << after.drawn << " presents " << before.presents << "->" << after.presents
            << " faults " << after.faults << " initialized " << Diagnostics(diagnostics).initialized << " foreground "
            << reinterpret_cast<std::uintptr_t>(GetForegroundWindow()) << " target "
            << reinterpret_cast<std::uintptr_t>(window) << '\n';
        log.flush();
        check(SUCCEEDED(result) && Statistics(statistics).drawn == before.drawn + 1 &&
                  Statistics(statistics).presents == before.presents + 1 && Diagnostics(diagnostics).initialized,
              "Same-device replacement keeps the active menu drawing and original "
              "Present intact");
        Release(chain);
        chain = sameDevice;
    }

    HWND foreign =
        CreateWindowExW(WS_EX_NOACTIVATE, MAKEINTATOM(windowClass), L"Other active fixture window", WS_OVERLAPPEDWINDOW,
                        0, 0, 200, 200, nullptr, nullptr, GetModuleHandleW(nullptr), nullptr);
    IDXGISwapChain* foreignChain = nullptr;
    ID3D11Device* foreignDevice = nullptr;
    ID3D11DeviceContext* foreignContext = nullptr;
    check(foreign && create(foreign, &foreignChain, &foreignDevice, &foreignContext),
          "A foreign owned HWND provides an independent swapchain");
    before = Statistics(statistics);
    result = LoggedPresent(foreignChain, window, statistics, diagnostics, log);
    check(SUCCEEDED(result) && Statistics(statistics).drawn == before.drawn,
          "A foreign HWND cannot replace active bindings");
    Release(foreignChain);
    Release(foreignContext);
    Release(foreignDevice);
    DestroyWindow(foreign);

    snapshot(FALSE);
    result = LoggedPresent(chain, window, statistics, diagnostics, log);
    check(SUCCEEDED(result) && Statistics(statistics).boundWindow == nullptr,
          "Closing still releases the adopted renderer on its owner thread");
    snapshot(TRUE);
    result = LoggedPresent(chain, window, statistics, diagnostics, log);
    check(SUCCEEDED(result) && Diagnostics(diagnostics).initialized,
          "Reopening still draws after active adoption and closed cleanup");
    check(stop(nullptr) == ERROR_SUCCESS, "Stop still disables the adopted renderer");
    before = Statistics(statistics);
    result = LoggedPresent(chain, window, statistics, diagnostics, log);
    check(SUCCEEDED(result) && Statistics(statistics).drawn == before.drawn &&
              Statistics(statistics).boundWindow == nullptr,
          "Stopped Present still cleans up and preserves the original call");
    CheckRendererFailureRetirement(window, create, check, log);
    check(Statistics(statistics).faults == 0, "Active replacement and identity rejection produce no renderer faults");
}

} // namespace iw::overlay::tests
