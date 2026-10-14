#include "OpenedTransitionChecks.h"
#include <atomic>
#include <stdexcept>
#include <thread>

namespace iw::overlay::tests {
namespace {
template<class T> void Release(T*& value) { if (value) { value->Release(); value = nullptr; } }
FixtureStatistics Statistics(StatisticsCall read) {
    FixtureStatistics value{};
    if (!read(&value)) { throw std::runtime_error("Opened transition statistics could not be read"); }
    return value;
}
RendererStatus Diagnostics(RendererStatusCall read) {
    RendererStatus value{};
    if (!read(&value)) { throw std::runtime_error("Opened transition diagnostics could not be read"); }
    return value;
}
void Trace(std::ostream& log, const std::string& value) { log << "TRACE " << value << '\n'; log.flush(); }
void Pump() {
    MSG message{};
    while (PeekMessageW(&message, nullptr, 0, 0, PM_REMOVE)) { TranslateMessage(&message); DispatchMessageW(&message); }
}
void Drop(IDXGISwapChain*& chain, ID3D11Device*& device, ID3D11DeviceContext*& context) {
    // Clear only this fixture's immediate context before releasing fixture references
    if (context) { context->ClearState(); context->Flush(); }
    Release(chain); Release(context); Release(device);
}
HRESULT OtherThreadPresent(IDXGISwapChain* chain, std::ostream& log) {
    std::atomic<bool> done{false};
    HRESULT result = E_PENDING;
    Trace(log, "Before Present on a different owned thread");
    std::thread worker([&] { result = chain->Present(0, 0); done.store(true, std::memory_order_release); });
    ULONGLONG deadline = GetTickCount64() + 5000;
    // DXGI may wait on the window thread, so keep this fixture's message pump alive
    while (!done.load(std::memory_order_acquire)) {
        Pump(); Sleep(1);
        if (GetTickCount64() >= deadline) {
            Trace(log, "FAIL Different-thread Present exceeded the bounded fixture wait");
            ExitProcess(4);
        }
    }
    worker.join();
    Trace(log, "After Present on a different owned thread");
    return result;
}
}

void CheckOpenedTransitions(HWND window, ATOM windowClass, IDXGISwapChain*& chain,
        ID3D11Device*& device, ID3D11DeviceContext*& context, const ChainFactory& create,
        StatisticsCall statistics, RendererStatusCall diagnostics, SnapshotCall snapshot, SnapshotCall unavailable,
        InitializeCall stop, const TransitionCheck& check, std::ostream& log) {
    HRESULT result = chain->Present(0, 0);
    auto state = Statistics(statistics);
    check(SUCCEEDED(result) && state.drawn == 1 && state.boundWindow == window && Diagnostics(diagnostics).initialized,
        "The transition starts with a real menu draw on its owner thread");
    result = OtherThreadPresent(chain, log);
    check(SUCCEEDED(result) && Statistics(statistics).drawn == state.drawn,
        "A different presenting thread cannot draw into the bound renderer");
    snapshot(FALSE);
    result = OtherThreadPresent(chain, log);
    check(SUCCEEDED(result) && Statistics(statistics).boundWindow == window,
        "A closed frame on another thread cannot destroy the owner's renderer state");
    Trace(log, "Before owner-thread closed Present cleanup");
    result = chain->Present(0, 0);
    Trace(log, "After owner-thread closed Present cleanup");
    auto closed = Diagnostics(diagnostics);
    check(SUCCEEDED(result) && Statistics(statistics).boundWindow == nullptr && !closed.initialized
        && closed.featureLevel == 0 && closed.width == 0 && closed.height == 0,
        "A closed snapshot releases owned renderer bindings on the presenting thread");

    for (unsigned cycle = 0; cycle < 3; ++cycle) {
        Trace(log, "Before closed owned device teardown and recreation " + std::to_string(cycle + 1));
        Drop(chain, device, context);
        check(create(window, &chain, &device, &context), "A fresh owned device uses the same target HWND");
        snapshot(TRUE);
        auto before = Statistics(statistics).drawn;
        result = OtherThreadPresent(chain, log);
        check(SUCCEEDED(result) && Statistics(statistics).drawn == before && Statistics(statistics).boundWindow == nullptr,
            "A different thread cannot claim an unbound renderer after cleanup");
        result = chain->Present(0, 0);
        check(SUCCEEDED(result) && Statistics(statistics).drawn == before + 1 && Diagnostics(diagnostics).initialized,
            "Reopening binds and draws the recreated swapchain on its original owner thread");
        snapshot(FALSE);
        result = chain->Present(0, 0);
        check(SUCCEEDED(result) && Statistics(statistics).boundWindow == nullptr && !Diagnostics(diagnostics).initialized,
            "Every closed owner-thread frame releases the recreated renderer bindings");
    }
    // A new owned HWND is still outside the immutable bootstrap identity
    HWND foreign = CreateWindowExW(WS_EX_NOACTIVATE, MAKEINTATOM(windowClass), L"Other owned transition window",
        WS_OVERLAPPEDWINDOW, 0, 0, 200, 200, nullptr, nullptr, GetModuleHandleW(nullptr), nullptr);
    IDXGISwapChain* otherChain = nullptr; ID3D11Device* otherDevice = nullptr; ID3D11DeviceContext* otherContext = nullptr;
    check(foreign && create(foreign, &otherChain, &otherDevice, &otherContext), "A second owned HWND has its own test swapchain");
    snapshot(TRUE);
    auto before = Statistics(statistics).drawn;
    result = otherChain->Present(0, 0);
    check(SUCCEEDED(result) && Statistics(statistics).drawn == before && Statistics(statistics).boundWindow == nullptr,
        "A wrong HWND cannot bind after closed cleanup");
    Drop(otherChain, otherDevice, otherContext); DestroyWindow(foreign);
    result = chain->Present(0, 0);
    check(SUCCEEDED(result) && Statistics(statistics).drawn == before + 1, "The exact target can reopen after a rejected HWND");
    unavailable(TRUE);
    result = OtherThreadPresent(chain, log);
    check(SUCCEEDED(result) && Statistics(statistics).boundWindow == window,
        "A missing model cannot trigger renderer destruction on a different thread");
    result = chain->Present(0, 0);
    check(SUCCEEDED(result) && Statistics(statistics).boundWindow == nullptr && !Diagnostics(diagnostics).initialized,
        "A missing producer model releases renderer resources on the owner thread");
    unavailable(FALSE);
    before = Statistics(statistics).drawn;
    result = chain->Present(0, 0);
    check(SUCCEEDED(result) && Statistics(statistics).drawn == before + 1,
        "A restored producer model can bind again on the original thread");
    check(stop(nullptr) == ERROR_SUCCESS, "Stop disables drawing without cross-thread renderer destruction");
    before = Statistics(statistics).drawn;
    result = chain->Present(0, 0);
    check(SUCCEEDED(result) && Statistics(statistics).drawn == before && Statistics(statistics).boundWindow == nullptr,
        "The next owner-thread Present releases resources after Stop and preserves its original call");
    auto resizes = Statistics(statistics).resizes;
    context->ClearState(); context->Flush();
    result = chain->ResizeBuffers(0, 800, 600, DXGI_FORMAT_UNKNOWN, 0);
    check(SUCCEEDED(result) && Statistics(statistics).resizes == resizes + 1,
        "Original ResizeBuffers remains safe after stopped renderer cleanup");
    check(Statistics(statistics).faults == 0 && Diagnostics(diagnostics).faults == 0,
        "Opened and closed recreation produces no renderer faults");
}
}
