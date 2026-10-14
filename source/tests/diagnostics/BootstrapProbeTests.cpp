#include "../../native/shared/OverlayProtocol.h"
#include "OwnedForeignWindow.h"
#include <array>
#include <atomic>
#include <bit>
#include <cerrno>
#include <filesystem>
#include <fstream>
#include <stdexcept>
#include <string>
#include <thread>
#include <vector>
#include <shellapi.h>

namespace {
using iw::overlay::BootstrapConfig;
using Call = DWORD (WINAPI*)(void*);
LRESULT CALLBACK Procedure(HWND window, UINT message, WPARAM word, LPARAM data) {
    return DefWindowProcW(window, message, word, data);
}
struct Api { Call initialize = nullptr, stop = nullptr; };

class Tests final {
public:
    explicit Tests(const std::filesystem::path& log) : output_(log) {
        if (!output_) { throw std::runtime_error("The probe test result file could not be opened"); }
    }
    ~Tests() {
        // Only this fixture's hidden window is destroyed; loaded inert modules stay until exit
        if (window_) { DestroyWindow(window_); }
        if (atom_) { UnregisterClassW(MAKEINTATOM(atom_), GetModuleHandleW(nullptr)); }
    }
    void Check(bool passed, const std::string& name) {
        output_ << (passed ? "PASS " : "FAIL ") << name << '\n'; output_.flush();
        if (!passed) { throw std::runtime_error(name); }
        ++checks_;
    }
    Api Load(const std::filesystem::path& path) {
        HMODULE module = LoadLibraryExW(path.c_str(), nullptr, LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
        Check(module != nullptr, "The requested inert probe DLL loads in the owned process");
        Api api{std::bit_cast<Call>(GetProcAddress(module, "InitializeOverlay")),
            std::bit_cast<Call>(GetProcAddress(module, "StopOverlay"))};
        Check(api.initialize && api.stop, "The probe exposes the checked bootstrap and inert stop contract");
        return api;
    }
    void Run() {
        WNDCLASSW registration{};
        registration.hInstance = GetModuleHandleW(nullptr);
        registration.lpfnWndProc = Procedure;
        registration.lpszClassName = L"IWBootstrapProbeOwnedHost";
        atom_ = RegisterClassW(&registration);
        Check(atom_ != 0, "The owned invisible host window class is registered");
        window_ = CreateWindowExW(0, MAKEINTATOM(atom_), L"IW hidden bootstrap probe fixture", WS_POPUP,
            0, 0, 1, 1, nullptr, nullptr, registration.hInstance, nullptr);
        Check(window_ && !IsWindowVisible(window_), "The owned host window exists and remains invisible");
        std::array<wchar_t, 32768> path{};
        DWORD length = GetModuleFileNameW(nullptr, path.data(), static_cast<DWORD>(path.size()));
        Check(length != 0 && length < path.size(), "The fixture locates its own sibling DLL directory");
        auto directory = std::filesystem::path(path.data()).parent_path();
        Api fixture = Load(directory / L"iw_bootstrap_probe_fixture.dll");
        Api production = Load(directory / L"iw_apparition_bootstrap_probe.dll");
        BootstrapConfig config{};
        config.size = sizeof(config); config.version = iw::overlay::ProtocolVersion;
        config.expectedProcessId = GetCurrentProcessId(); config.gameWindow = window_;
        Check(fixture.stop(nullptr) == ERROR_SUCCESS, "An inert Stop before initialization succeeds without creating state");
        Check(fixture.initialize(nullptr) == ERROR_INVALID_PARAMETER, "A null local config pointer rejects normally");
        Check(fixture.initialize(reinterpret_cast<void*>(1)) == ERROR_INVALID_PARAMETER, "An unreadable local config pointer rejects normally");
        SYSTEM_INFO system{}; GetSystemInfo(&system);
        void* pages = VirtualAlloc(nullptr, system.dwPageSize * 2, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
        Check(pages != nullptr, "The fixture owns two pages for the partial-packet read regression");
        DWORD oldProtection = 0;
        bool protectedPage = VirtualProtect(static_cast<std::byte*>(pages) + system.dwPageSize,
            system.dwPageSize, PAGE_NOACCESS, &oldProtection) != FALSE;
        DWORD partialResult = protectedPage ? fixture.initialize(static_cast<std::byte*>(pages) + system.dwPageSize - 16) : ERROR_SUCCESS;
        VirtualFree(pages, 0, MEM_RELEASE);
        Check(protectedPage && partialResult == ERROR_INVALID_PARAMETER, "A packet crossing into an unreadable page rejects before any initialization");
        auto invalid = config; invalid.size -= 1;
        Check(fixture.initialize(&invalid) == ERROR_INVALID_PARAMETER, "An incorrect packet size rejects");
        invalid = config; invalid.version += 1;
        Check(fixture.initialize(&invalid) == ERROR_INVALID_PARAMETER, "An incompatible protocol version rejects");
        invalid = config; invalid.expectedProcessId += 1;
        Check(fixture.initialize(&invalid) == ERROR_INVALID_PARAMETER, "A mismatched process identifier rejects");
        invalid = config; invalid.gameWindow = nullptr;
        Check(fixture.initialize(&invalid) == ERROR_INVALID_PARAMETER, "A missing target HWND rejects");
        invalid = config; invalid.gameWindow = reinterpret_cast<HWND>(1);
        Check(fixture.initialize(&invalid) == ERROR_INVALID_PARAMETER, "An invalid target HWND rejects");
        {
            iw::overlay::tests::OwnedForeignWindow foreign;
            DWORD owner = 0; GetWindowThreadProcessId(foreign.Window(), &owner);
            Check(owner == foreign.ProcessId() && owner != GetCurrentProcessId(), "A second owned process provides a verified foreign HWND");
            invalid = config; invalid.gameWindow = foreign.Window();
            Check(fixture.initialize(&invalid) == ERROR_INVALID_PARAMETER, "A window owned by the test child process rejects");
        }
        invalid = config;
        for (auto& character : invalid.sessionDirectory) { character = L'x'; }
        Check(fixture.initialize(&invalid) == ERROR_INVALID_PARAMETER, "An unterminated path rejects even though the probe performs no file access");
        config.sessionDirectory[0] = L'x'; config.sessionDirectory[1] = L'\0';
        Check(production.initialize(&config) == ERROR_BAD_EXE_FORMAT, "The production probe rejects the fixture executable basename");
        Check(production.stop(nullptr) == ERROR_SUCCESS, "A rejected production probe remains inert when stopped");
        Check(fixture.initialize(&config) == ERROR_SUCCESS, "A complete valid local config records one successful inert bootstrap");
        Check(fixture.initialize(&config) == ERROR_ALREADY_INITIALIZED, "A second valid bootstrap rejects as a duplicate");
        std::atomic<unsigned> rejected{0};
        std::vector<std::thread> threads;
        // Concurrent duplicate requests must never create another successful bootstrap
        for (unsigned i = 0; i < 8; ++i) {
            threads.emplace_back([&] { if (fixture.initialize(&config) == ERROR_ALREADY_INITIALIZED) { ++rejected; } });
        }
        for (auto& thread : threads) { thread.join(); }
        Check(rejected == threads.size(), "All concurrent duplicate bootstrap calls reject");
        Check(fixture.initialize(nullptr) == ERROR_INVALID_PARAMETER, "Invalid requests remain validated after successful initialization");
        Check(fixture.stop(nullptr) == ERROR_SUCCESS && fixture.stop(nullptr) == ERROR_SUCCESS, "Repeated inert Stop calls succeed");
        Check(fixture.initialize(&config) == ERROR_ALREADY_INITIALIZED, "Stopping never resets single-use initialization");
        Check(!GetModuleHandleW(L"d3d11.dll") && !GetModuleHandleW(L"dxgi.dll") && !GetModuleHandleW(L"d3d12.dll"),
            "Probe validation never loads graphics modules in this fixture");
        output_ << "PASS " << checks_ << " inert bootstrap probe checks\n"; output_.flush();
    }
    void Failure(const char* message) { output_ << "ERROR " << message << '\n'; output_.flush(); }
private:
    std::ofstream output_;
    HWND window_ = nullptr;
    ATOM atom_ = 0;
    unsigned checks_ = 0;
};

HANDLE ParseHandle(const wchar_t* value) {
    wchar_t* end = nullptr; errno = 0;
    unsigned long long parsed = std::wcstoull(value, &end, 10);
    if (!value[0] || value[0] == L'-' || !end || *end || errno || parsed == 0) { return nullptr; }
    return reinterpret_cast<HANDLE>(static_cast<std::uintptr_t>(parsed));
}
}

int WINAPI wWinMain(HINSTANCE, HINSTANCE, PWSTR, int) {
    int count = 0;
    wchar_t** arguments = CommandLineToArgvW(GetCommandLineW(), &count);
    if (!arguments) { return 2; }
    if (count == 4 && std::wstring_view(arguments[1]) == L"--foreign-host") {
        HANDLE pipe = ParseHandle(arguments[2]), stop = ParseHandle(arguments[3]);
        LocalFree(arguments);
        return pipe && stop ? iw::overlay::tests::RunForeignWindowHost(pipe, stop) : 2;
    }
    if (count != 2) { LocalFree(arguments); return 2; }
    std::filesystem::path log(arguments[1]); LocalFree(arguments);
    try {
        Tests tests(log);
        try { tests.Run(); return 0; }
        catch (const std::exception& failure) { tests.Failure(failure.what()); return 1; }
    } catch (...) { return 3; }
}
