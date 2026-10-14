#include "../shared/OverlayProtocol.h"
#include <array>
#include <atomic>
#include <cwchar>

namespace {
std::atomic<bool> initialized{false};

bool AcceptedProcess() noexcept {
    std::array<wchar_t, 32768> path{};
    DWORD length = GetModuleFileNameW(nullptr, path.data(), static_cast<DWORD>(path.size()));
    if (length == 0 || length >= path.size()) { return false; }
    const wchar_t* basename = std::wcsrchr(path.data(), L'\\');
    basename = basename ? basename + 1 : path.data();
#ifdef IW_BOOTSTRAP_PROBE_FIXTURE
    // Test validation cannot be initialized inside the retail game
    return _wcsicmp(basename, L"BootstrapProbeTests.exe") == 0;
#else
    return _wcsicmp(basename, L"iw7_ship.exe") == 0;
#endif
}

bool CopyValidatedConfig(void* argument, iw::overlay::BootstrapConfig& config) noexcept {
    SIZE_T copied = 0;
    // Read only the current process's caller packet and require the complete fixed size
    if (!argument || !ReadProcessMemory(GetCurrentProcess(), argument, &config, sizeof(config), &copied)
        || copied != sizeof(config)) { return false; }
    if (config.size != sizeof(config) || config.version != iw::overlay::ProtocolVersion
        || config.expectedProcessId != GetCurrentProcessId() || !IsWindow(config.gameWindow)) { return false; }
    DWORD owner = 0;
    if (!GetWindowThreadProcessId(config.gameWindow, &owner) || owner != config.expectedProcessId) { return false; }
    // The path is only validated, never opened or retained by this probe
    return std::wmemchr(config.sessionDirectory, L'\0', std::size(config.sessionDirectory)) != nullptr;
}
}

extern "C" __declspec(dllexport) DWORD WINAPI InitializeOverlay(void* argument) {
    iw::overlay::BootstrapConfig config{};
    if (!CopyValidatedConfig(argument, config)) { return ERROR_INVALID_PARAMETER; }
    if (!AcceptedProcess()) { return ERROR_BAD_EXE_FORMAT; }
    bool expected = false;
    // A successful call records one inert bootstrap without starting any worker
    if (!initialized.compare_exchange_strong(expected, true, std::memory_order_acq_rel)) {
        return ERROR_ALREADY_INITIALIZED;
    }
    return ERROR_SUCCESS;
}

extern "C" __declspec(dllexport) DWORD WINAPI StopOverlay(void*) {
    // There are no resources or workers to stop, and initialization stays single-use
    return ERROR_SUCCESS;
}

BOOL WINAPI DllMain(HINSTANCE, DWORD, LPVOID) {
    // Loader entry does not perform diagnostics, create threads, or load graphics code
    return TRUE;
}
