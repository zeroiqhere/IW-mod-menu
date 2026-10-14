#include "ClosedTransitionChecks.h"
#include <stdexcept>

namespace iw::overlay::tests {
namespace {
template<class T> void Release(T*& value) { if (value) { value->Release(); value = nullptr; } }

void Trace(std::ostream& log, const std::string& stage) {
    // Flushing before the next native call preserves its location if the test process crashes
    log << "TRACE " << stage << '\n'; log.flush();
}

void DropDevice(IDXGISwapChain*& chain, ID3D11Device*& device, ID3D11DeviceContext*& context) {
    // Finish using fixture state before releasing its own COM references
    if (context) { context->ClearState(); context->Flush(); }
    Release(chain); Release(context); Release(device);
}

FixtureStatistics Statistics(StatisticsCall read) {
    // A failed export read must fail the test rather than provide empty counters
    FixtureStatistics value{};
    if (!read(&value)) { throw std::runtime_error("Closed transition statistics could not be read"); }
    return value;
}

bool Unbound(RendererStatusCall read) {
    // Zero feature level means no device was bound, not an unsupported device
    RendererStatus status{};
    return read(&status) && !status.initialized && status.drawnFrames == 0 && status.faults == 0
        && status.featureLevel == 0 && status.width == 0 && status.height == 0;
}

void PresentAndCheck(IDXGISwapChain* chain, StatisticsCall statistics, RendererStatusCall diagnostics,
        const TransitionCheck& check, const std::string& stage, std::ostream& log) {
    // Count the real hook call and retain the original driver's result
    auto before = Statistics(statistics);
    Trace(log, stage + ": before original Present");
    HRESULT result = chain->Present(0, 0);
    Trace(log, stage + ": after original Present");
    auto after = Statistics(statistics);
    check(SUCCEEDED(result) && after.presents == before.presents + 1,
        stage + ": original Present succeeds through the resident hook");
    check(after.drawn == 0 && after.faults == 0 && after.boundWindow == nullptr && Unbound(diagnostics),
        stage + ": a closed snapshot never binds a device or draws a menu");
}

}

void CheckClosedTransitions(HWND& window, ATOM windowClass, IDXGISwapChain*& chain,
        ID3D11Device*& device, ID3D11DeviceContext*& context, const ChainFactory& create,
        StatisticsCall statistics, RendererStatusCall diagnostics, InitializeCall stop,
        const TransitionCheck& check, std::ostream& log) {
    check(Unbound(diagnostics), "Closed transitions begin without ImGui or a retained game device");
    for (unsigned cycle = 0; cycle < 3; ++cycle) {
        // Replace only fixture resources while keeping the same window identity
        std::string stage = "Same HWND cycle " + std::to_string(cycle + 1);
        PresentAndCheck(chain, statistics, diagnostics, check, stage + " before teardown", log);
        auto before = Statistics(statistics);
        Trace(log, stage + ": before original ResizeBuffers");
        HRESULT result = chain->ResizeBuffers(0, 640 + cycle * 80, 480 + cycle * 40, DXGI_FORMAT_UNKNOWN, 0);
        Trace(log, stage + ": after original ResizeBuffers");
        check(SUCCEEDED(result) && Statistics(statistics).resizes == before.resizes + 1,
            stage + ": original ResizeBuffers succeeds with no owned render target");
        HWND unchanged = window;
        Trace(log, stage + ": before device teardown");
        DropDevice(chain, device, context);
        Trace(log, stage + ": after device teardown, before recreation");
        check(create(window, &chain, &device, &context) && window == unchanged,
            stage + ": a fresh device and swapchain use the same owned HWND");
        PresentAndCheck(chain, statistics, diagnostics, check, stage + " after recreation", log);
    }
    for (unsigned cycle = 0; cycle < 3; ++cycle) {
        // Window replacement must remain harmless before the renderer ever binds
        std::string stage = "New HWND cycle " + std::to_string(cycle + 1);
        Trace(log, stage + ": before device and window teardown");
        DropDevice(chain, device, context);
        check(DestroyWindow(window) != FALSE, stage + ": the old owned window is destroyed");
        window = nullptr;
        // The bootstrap identity remains unchanged; only a closed pass-through path is exercised
        window = CreateWindowExW(0, MAKEINTATOM(windowClass), L"IW standalone closed D3D11 transition",
            WS_OVERLAPPEDWINDOW, CW_USEDEFAULT, CW_USEDEFAULT, 960, 720,
            nullptr, nullptr, GetModuleHandleW(nullptr), nullptr);
        check(window != nullptr, stage + ": a replacement owned window is created");
        ShowWindow(window, SW_SHOWNOACTIVATE);
        Trace(log, stage + ": before device recreation");
        check(create(window, &chain, &device, &context), stage + ": a fresh device and swapchain use the replacement HWND");
        PresentAndCheck(chain, statistics, diagnostics, check, stage + " after recreation", log);
    }
    check(stop(nullptr) == ERROR_SUCCESS && !Statistics(statistics).enabled,
        "Resident shutdown disables drawing before the final transition");
    // The DLL and original-call trampolines stay resident after runtime shutdown
    Trace(log, "After runtime shutdown: before device teardown");
    DropDevice(chain, device, context);
    Trace(log, "After runtime shutdown: before device recreation");
    check(create(window, &chain, &device, &context), "A fresh owned device can be created after runtime shutdown");
    PresentAndCheck(chain, statistics, diagnostics, check, "After runtime shutdown", log);
    auto before = Statistics(statistics);
    Trace(log, "After runtime shutdown: before original ResizeBuffers");
    HRESULT result = chain->ResizeBuffers(0, 720, 540, DXGI_FORMAT_UNKNOWN, 0);
    Trace(log, "After runtime shutdown: after original ResizeBuffers");
    check(SUCCEEDED(result) && Statistics(statistics).resizes == before.resizes + 1,
        "Original ResizeBuffers still succeeds after runtime shutdown");
}

}
