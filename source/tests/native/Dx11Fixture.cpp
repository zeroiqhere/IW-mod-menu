#include "FixtureApi.h"
#include "PipelineSentinel.h"
#include "FixtureExternalBootstrap.h"
#include "ClosedTransitionChecks.h"
#include "SteamOverlayFixture.h"
#include "SteamOverlayFixtureTests.h"
#include "OpenedTransitionChecks.h"
#include "ActiveTransitionChecks.h"
#include "../../native/shared/OverlayProtocol.h"

#include <array>
#include <bit>
#include <cerrno>
#include <filesystem>
#include <fstream>
#include <stdexcept>
#include <string>
#include <shellapi.h>

namespace {
using namespace iw::overlay;
using namespace iw::overlay::tests;

template<class T> void Release(T*& value) { if (value) { value->Release(); value = nullptr; } }
template<class T> T Export(HMODULE module, const char* name) {
    // Windows exports use pointer-sized addresses with the declared fixture calling convention
    return std::bit_cast<T>(GetProcAddress(module, name));
}
LRESULT CALLBACK Procedure(HWND window, UINT message, WPARAM word, LPARAM data) {
    return DefWindowProcW(window, message, word, data);
}
void Pump() {
    MSG message{};
    while (PeekMessageW(&message, nullptr, 0, 0, PM_REMOVE)) {
        TranslateMessage(&message); DispatchMessageW(&message);
    }
}

class Fixture final {
public:
    explicit Fixture(const std::filesystem::path& log, bool waitForBootstrap, DWORD holdMilliseconds, bool closedTransitions,
            std::filesystem::path steamApi, bool openedTransitions, bool activeTransitions)
        : output_(log), log_(log), external_(waitForBootstrap), holdMilliseconds_(holdMilliseconds), closedTransitions_(closedTransitions), steamApi_(std::move(steamApi)), openedTransitions_(openedTransitions), activeTransitions_(activeTransitions) {
        if (!output_) { throw std::runtime_error("The result file could not be opened"); }
    }
    ~Fixture() {
        // Trampolines remain in the DLL until process exit, even after runtime stop
        if (stop_) { stop_(nullptr); }
        if (context_) { context_->ClearState(); context_->Flush(); }
        Release(chain_); Release(context_); Release(device_);
        if (window_) { DestroyWindow(window_); }
        if (atom_) { UnregisterClassW(MAKEINTATOM(atom_), GetModuleHandleW(nullptr)); }
        steam_.Shutdown();
    }
    void Check(bool passed, const std::string& name) {
        output_ << (passed ? "PASS " : "FAIL ") << name << '\n'; output_.flush();
        if (!passed) { throw std::runtime_error(name); }
        ++checks_;
    }
    void Run() {
        // The optional documented Steam initialization precedes every owned D3D device
        if (!steamApi_.empty()) { steam_.Start(steamApi_, output_); }
        WNDCLASSW registration{};
        registration.hInstance = GetModuleHandleW(nullptr);
        registration.lpfnWndProc = Procedure;
        registration.lpszClassName = L"IWOverlayFixtureHost";
        atom_ = RegisterClassW(&registration);
        Check(atom_ != 0, "The fixture registers its own host window class");
        window_ = CreateWindowExW(0, MAKEINTATOM(atom_), L"IW standalone D3D11 fixture",
            WS_OVERLAPPEDWINDOW, CW_USEDEFAULT, CW_USEDEFAULT, 960, 720,
            nullptr, nullptr, registration.hInstance, nullptr);
        Check(window_ != nullptr, "The fixture creates its own game-like window");
        ShowWindow(window_, SW_SHOW); SetForegroundWindow(window_);
        for (int step = 0; step < 200 && GetForegroundWindow() != window_; ++step) { Pump(); Sleep(10); }
        Check(GetForegroundWindow() == window_, "Only the fixture host is made foreground");
        Check(CreateChain(window_, &chain_, &device_, &context_), "A real Direct3D 11.0 device and swapchain are available");
        if (!steamApi_.empty()) {
            Check(steam_.ObserveModule(output_), "The opt-in fixture observes Steam's overlay module");
        }
        std::array<wchar_t, 32768> path{};
        DWORD length = GetModuleFileNameW(nullptr, path.data(), static_cast<DWORD>(path.size()));
        Check(length != 0 && length < path.size(), "The fixture locates its own executable directory");
        std::filesystem::path library = std::filesystem::path(path.data()).parent_path() / L"iw_overlay_fixture.dll";
        if (external_) {
            payload_ = WaitForExternalBootstrap(window_, log_, output_, [this] {
                Pump(); chain_->Present(0, DXGI_PRESENT_TEST);
            });
            Check(payload_ != nullptr, "The external helper loads and bootstraps only the fixture payload");
            SetForegroundWindow(window_);
            // A helper launch can briefly change focus while the compositor completes activation
            output_ << "INFO Foreground after helper " << reinterpret_cast<std::uintptr_t>(GetForegroundWindow())
                << " owned target " << reinterpret_cast<std::uintptr_t>(window_)
                << " own active " << reinterpret_cast<std::uintptr_t>(GetActiveWindow()) << "\n";
            output_.flush();
            for (int step = 0; step < 6000 && GetForegroundWindow() != window_; ++step) { Pump(); Sleep(10); }
            Check(GetForegroundWindow() == window_, "The owned fixture is foreground again after the external helper completes");
        } else {
            payload_ = LoadLibraryW(library.c_str());
            Check(payload_ != nullptr, "The isolated fixture payload loads without renderer work in DllMain");
        }
        initialize_ = Export<InitializeCall>(payload_, "InitializeOverlay");
        stop_ = Export<InitializeCall>(payload_, "StopOverlay");
        statistics_ = Export<StatisticsCall>(payload_, "ReadFixtureStatistics");
        snapshot_ = Export<SnapshotCall>(payload_, "SetFixtureMenuOpen");
        unavailable_ = Export<SnapshotCall>(payload_, "SetFixtureModelUnavailable");
        rendererStatus_ = Export<RendererStatusCall>(payload_, "ReadFixtureRendererStatus");
        Check(initialize_ && stop_ && statistics_ && snapshot_ && unavailable_ && rendererStatus_, "The fixture-only API and production lifecycle exports are present");
        auto value = Statistics();
        if (!external_) { Check(!value.enabled && value.presents == 0, "Loading the DLL alone installs no hooks"); }
        BootstrapConfig config{};
        config.size = sizeof(config); config.version = ProtocolVersion;
        config.expectedProcessId = GetCurrentProcessId(); config.gameWindow = window_;
        Check(initialize_(nullptr) == ERROR_INVALID_PARAMETER, "A null bootstrap packet is rejected before hooks");
        Check(initialize_(reinterpret_cast<void*>(1)) == ERROR_INVALID_PARAMETER, "An unreadable bootstrap packet is rejected before hooks");
        auto invalid = config; invalid.version += 1;
        Check(initialize_(&invalid) == ERROR_INVALID_PARAMETER, "An incompatible packet version is rejected");
        invalid = config; invalid.gameWindow = nullptr;
        Check(initialize_(&invalid) == ERROR_INVALID_PARAMETER, "A missing target window is rejected");
        invalid = config; invalid.expectedProcessId += 1;
        Check(initialize_(&invalid) == ERROR_INVALID_PARAMETER, "A foreign process identity is rejected");
        invalid = config;
        for (auto& character : invalid.sessionDirectory) { character = L'x'; }
        Check(initialize_(&invalid) == ERROR_INVALID_PARAMETER, "An unterminated fixed-width path is rejected");
        HRESULT baseline = chain_->Present(0, DXGI_PRESENT_TEST);
        output_ << "INFO original TEST HRESULT " << std::hex << static_cast<unsigned long>(baseline) << std::dec << '\n';
        output_.flush();
        Check(SUCCEEDED(baseline), external_ ? "The externally bootstrapped host swapchain passes its driver test"
            : "The host swapchain passes a driver test before hooks are enabled");
        if (!external_) { Check(initialize_(&config) == ERROR_SUCCESS, "Bootstrap installs verified D3D11 hooks outside the loader lock"); }
        Check(initialize_(&config) == ERROR_ALREADY_INITIALIZED, "A repeated bootstrap cannot create another hook owner");
        value = Statistics();
        Check(value.enabled && value.boundWindow == nullptr, "Hook setup does not bind the hidden scratch swapchain");
        Check(!RendererDiagnostics().initialized && RendererDiagnostics().drawnFrames == 0,
            "Hook readiness does not falsely report a rendered frame");
        auto drawn = value.drawn;
        auto presented = value.presents;
        HRESULT result = chain_->Present(0, DXGI_PRESENT_TEST);
        Check(result == baseline && Statistics().drawn == drawn, "DXGI_PRESENT_TEST preserves its original result and skips menu drawing");
        Check(Statistics().presents == presented + 1, "The existing host Present enters the installed hook");
        if (closedTransitions_) {
            // Closing the synthetic model here ensures the renderer has never held a device
            snapshot_(FALSE);
            CheckClosedTransitions(window_, atom_, chain_, device_, context_,
                [this](HWND window, IDXGISwapChain** chain, ID3D11Device** device, ID3D11DeviceContext** context) {
                    return CreateChain(window, chain, device, context);
                }, statistics_, rendererStatus_, stop_, [this](bool passed, const std::string& name) { Check(passed, name); }, output_);
            output_ << "PASS " << checks_ << " closed D3D11 transition checks\n"; output_.flush();
            return;
        }
        if (activeTransitions_) {
            CheckActiveTransitions(window_, atom_, chain_, device_, context_,
                [this](HWND window, IDXGISwapChain** chain, ID3D11Device** device, ID3D11DeviceContext** context) {
                    return CreateChain(window, chain, device, context);
                }, statistics_, rendererStatus_, snapshot_, stop_,
                [this](bool passed, const std::string& name) { Check(passed, name); }, output_);
            output_ << "PASS " << checks_ << " active D3D11 transition checks\n"; output_.flush(); return;
        }
        if (openedTransitions_) {
            CheckOpenedTransitions(window_, atom_, chain_, device_, context_,
                [this](HWND window, IDXGISwapChain** chain, ID3D11Device** device, ID3D11DeviceContext** context) {
                    return CreateChain(window, chain, device, context);
                }, statistics_, rendererStatus_, snapshot_, unavailable_, stop_,
                [this](bool passed, const std::string& name) { Check(passed, name); }, output_);
            output_ << "PASS " << checks_ << " opened D3D11 transition checks\n"; output_.flush(); return;
        }
        PipelineSentinel sentinel;
        std::string error;
        Check(sentinel.Create(device_, error), "Real shader, output, resource, and UAV sentinels are created: " + error);
        sentinel.Bind(context_);
        Check(sentinel.Matches(context_, error), "Sentinel bindings are valid before rendering: " + error);
        result = chain_->Present(0, 0);
        value = Statistics();
        output_ << "INFO first Present HRESULT " << std::hex << static_cast<unsigned long>(result) << std::dec
            << " presents " << value.presents << " draws " << value.drawn << " faults " << value.faults
            << " foreground " << reinterpret_cast<std::uintptr_t>(GetForegroundWindow())
            << " target " << reinterpret_cast<std::uintptr_t>(window_) << "\n";
        output_.flush();
        Check(SUCCEEDED(result) && value.drawn == drawn + 1 && value.boundWindow == window_, "The menu draws into the exact existing foreground swapchain");
        auto diagnostics = RendererDiagnostics();
        Check(diagnostics.initialized && diagnostics.drawnFrames == value.drawn && diagnostics.faults == 0
            && diagnostics.featureLevel == D3D_FEATURE_LEVEL_11_0 && diagnostics.width == 960 && diagnostics.height == 720,
            "Atomic diagnostics identify the successfully bound device and drawable dimensions");
        Check(sentinel.Matches(context_, error), "Drawing restores the complete tested pipeline state: " + error);
        drawn = value.drawn;
        snapshot_(FALSE);
        result = chain_->Present(0, 0);
        Check(SUCCEEDED(result) && Statistics().drawn == drawn, "A closed snapshot passes through without drawing");
        snapshot_(TRUE);
        SetWindowTextW(window_, L"Changed fixture title");
        result = chain_->Present(0, 0);
        Check(SUCCEEDED(result) && Statistics().drawn == drawn, "A changed target title stops menu drawing");
        SetWindowTextW(window_, L"IW standalone D3D11 fixture");
        result = chain_->Present(0, 0);
        Check(SUCCEEDED(result) && Statistics().drawn == drawn + 1, "Restoring the exact title allows a new drawing frame");
        drawn = Statistics().drawn;
        HWND foreign = CreateWindowExW(WS_EX_NOACTIVATE, MAKEINTATOM(atom_), L"Other fixture window", WS_OVERLAPPEDWINDOW,
            0, 0, 200, 200, nullptr, nullptr, registration.hInstance, nullptr);
        IDXGISwapChain* foreignChain = nullptr; ID3D11Device* foreignDevice = nullptr; ID3D11DeviceContext* foreignContext = nullptr;
        bool foreignCreated = foreign && CreateChain(foreign, &foreignChain, &foreignDevice, &foreignContext);
        Check(foreignCreated, "A separate owned test swapchain is available for identity rejection");
        result = foreignChain->Present(0, 0);
        Check(SUCCEEDED(result) && Statistics().drawn == drawn, "A different window's Present cannot receive the menu");
        Release(foreignChain); Release(foreignContext); Release(foreignDevice); DestroyWindow(foreign);
        IDXGISwapChain* second = nullptr; ID3D11Device* secondDevice = nullptr; ID3D11DeviceContext* secondContext = nullptr;
        Check(CreateChain(window_, &second, &secondDevice, &secondContext), "A second swapchain for the same window can be tested");
        result = second->Present(0, 0);
        Check(SUCCEEDED(result) && Statistics().drawn == drawn + 1, "An active menu adopts another verified same-window swapchain");
        drawn = Statistics().drawn;
        result = chain_->Present(0, 0);
        Check(SUCCEEDED(result) && Statistics().drawn == drawn + 1, "The original swapchain can be adopted again on its owner thread");
        drawn = Statistics().drawn;
        Release(second); Release(secondContext); Release(secondDevice);
        // Clear only fixture-owned state before asking the host to replace its back buffer
        context_->ClearState(); context_->Flush();
        auto resized = Statistics().resizes;
        result = chain_->ResizeBuffers(0, 800, 600, DXGI_FORMAT_UNKNOWN, 0);
        Check(SUCCEEDED(result) && Statistics().resizes == resized + 1, "ResizeBuffers releases the overlay target before the original call");
        sentinel.Bind(context_);
        result = chain_->Present(0, 0);
        Check(SUCCEEDED(result) && Statistics().drawn == drawn + 1, "The renderer lazily rebuilds its target after resize");
        Check(sentinel.Matches(context_, error), "Pipeline restoration remains correct after resize: " + error);
        Check(RendererDiagnostics().width == 800 && RendererDiagnostics().height == 600,
            "Renderer dimensions follow a successful host resize");
        if (holdMilliseconds_ != 0) {
            output_ << "INFO Visual inspection hold " << holdMilliseconds_ << " milliseconds\n"; output_.flush();
            // Only this owned test window remains visible during a bounded screenshot check
            ULONGLONG deadline = GetTickCount64() + holdMilliseconds_;
            while (GetTickCount64() < deadline) { Pump(); chain_->Present(1, 0); Sleep(1); }
        }
        drawn = Statistics().drawn;
        Check(stop_(nullptr) == ERROR_SUCCESS && !Statistics().enabled, "Stop disables drawing without removing live trampolines");
        Check(!RendererDiagnostics().initialized, "Renderer diagnostics stop reporting active readiness after shutdown");
        result = chain_->Present(0, 0);
        Check(SUCCEEDED(result) && Statistics().drawn == drawn, "Present still passes through after runtime shutdown");
        Check(Statistics().faults == 0, "No renderer or hook faults occurred in the fixture");
        output_ << "PASS " << checks_ << " native D3D11 fixture checks\n"; output_.flush();
    }
    void Failure(const std::string& message) { output_ << "ERROR " << message << '\n'; output_.flush(); }
private:
    bool CreateChain(HWND window, IDXGISwapChain** chain, ID3D11Device** device, ID3D11DeviceContext** context) {
        DXGI_SWAP_CHAIN_DESC description{};
        description.BufferDesc.Width = 960; description.BufferDesc.Height = 720;
        description.BufferDesc.Format = DXGI_FORMAT_R8G8B8A8_UNORM;
        description.BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT;
        description.BufferCount = 1; description.SampleDesc.Count = 1;
        description.OutputWindow = window; description.Windowed = TRUE;
        description.SwapEffect = DXGI_SWAP_EFFECT_DISCARD;
        D3D_FEATURE_LEVEL requested = D3D_FEATURE_LEVEL_11_0, actual{};
        HRESULT result = D3D11CreateDeviceAndSwapChain(nullptr, D3D_DRIVER_TYPE_HARDWARE, nullptr, 0,
            &requested, 1, D3D11_SDK_VERSION, &description, chain, device, &actual, context);
        return SUCCEEDED(result) && actual == requested;
    }
    FixtureStatistics Statistics() {
        FixtureStatistics value{};
        if (!statistics_(&value)) { throw std::runtime_error("The fixture statistics packet was rejected"); }
        return value;
    }
    RendererStatus RendererDiagnostics() {
        RendererStatus value{};
        if (!rendererStatus_(&value)) { throw std::runtime_error("The renderer diagnostics packet was rejected"); }
        return value;
    }
    std::ofstream output_;
    std::filesystem::path log_;
    bool external_ = false;
    DWORD holdMilliseconds_ = 0;
    bool closedTransitions_ = false;
    std::filesystem::path steamApi_;
    bool openedTransitions_ = false;
    bool activeTransitions_ = false;
    SteamOverlayFixture steam_;
    unsigned checks_ = 0;
    ATOM atom_ = 0;
    HWND window_ = nullptr;
    IDXGISwapChain* chain_ = nullptr;
    ID3D11Device* device_ = nullptr;
    ID3D11DeviceContext* context_ = nullptr;
    HMODULE payload_ = nullptr;
    InitializeCall initialize_ = nullptr, stop_ = nullptr;
    StatisticsCall statistics_ = nullptr;
    SnapshotCall snapshot_ = nullptr;
    SnapshotCall unavailable_ = nullptr;
    RendererStatusCall rendererStatus_ = nullptr;
};
}

int WINAPI wWinMain(HINSTANCE, HINSTANCE, PWSTR, int) {
    int count = 0;
    wchar_t** arguments = CommandLineToArgvW(GetCommandLineW(), &count);
    if (!arguments || count < 2) { if (arguments) { LocalFree(arguments); } return 2; }
    std::filesystem::path log(arguments[1]);
    bool external = false;
    DWORD holdMilliseconds = 0;
    bool closedTransitions = false;
    std::filesystem::path steamApi;
    bool steamGuardsOnly = false;
    bool openedTransitions = false;
    bool activeTransitions = false;
    for (int index = 2; index < count; ++index) {
        std::wstring option(arguments[index]);
        if (option == L"--wait-for-bootstrap" && !external) { external = true; }
        else if (option == L"--closed-transitions" && !closedTransitions) { closedTransitions = true; }
        else if (option == L"--opened-transitions" && !openedTransitions) { openedTransitions = true; }
        else if (option == L"--active-transitions" && !activeTransitions) { activeTransitions = true; }
        else if (option == L"--steam-api" && steamApi.empty() && ++index < count) { steamApi = arguments[index]; }
        else if (option == L"--steam-guards-only" && !steamGuardsOnly) { steamGuardsOnly = true; }
        else if (option == L"--hold-ms" && holdMilliseconds == 0 && ++index < count) {
            wchar_t* end = nullptr;
            errno = 0;
            unsigned long parsed = std::wcstoul(arguments[index], &end, 10);
            if (arguments[index][0] == L'-' || !end || end == arguments[index] || *end != L'\0'
                || errno != 0 || parsed == 0 || parsed > 60000) { LocalFree(arguments); return 2; }
            holdMilliseconds = static_cast<DWORD>(parsed);
        } else { LocalFree(arguments); return 2; }
    }
    LocalFree(arguments);
    if (steamGuardsOnly) {
        if (external || holdMilliseconds || closedTransitions || openedTransitions || activeTransitions || !steamApi.empty()) { return 2; }
        return RunSteamOverlayGuards(log);
    }
    try {
        if (static_cast<int>(closedTransitions) + static_cast<int>(openedTransitions) + static_cast<int>(activeTransitions) > 1
            || (activeTransitions && (external || holdMilliseconds || !steamApi.empty()))) { return 2; }
        Fixture fixture(log, external, holdMilliseconds, closedTransitions, std::move(steamApi), openedTransitions, activeTransitions);
        try { fixture.Run(); return 0; }
        catch (const std::exception& failure) { fixture.Failure(failure.what()); return 1; }
    } catch (...) { return 3; }
}
