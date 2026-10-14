#include "shared/OverlayProtocol.h"
#include "render/Dx11Hooks.h"

#include <array>
#include <atomic>
#include <cwchar>
#ifdef IW_OVERLAY_TEST_BUILD
#include "../tests/native/FixtureApi.h"
#endif

namespace {
std::atomic<bool> started{false};
#ifdef IW_OVERLAY_TEST_BUILD
std::atomic<DWORD> initializationResult{ERROR_IO_PENDING};
#endif

DWORD StartupResult(DWORD result) {
#ifdef IW_OVERLAY_TEST_BUILD
    initializationResult.store(result, std::memory_order_release);
#endif
    return result;
}

bool AcceptedProcess() {
    std::array<wchar_t, 32768> path{};
    DWORD length = GetModuleFileNameW(nullptr, path.data(), static_cast<DWORD>(path.size()));
    if (length == 0 || length >= path.size()) { return false; }
    const wchar_t* name = std::wcsrchr(path.data(), L'\\');
    name = name ? name + 1 : path.data();
#ifdef IW_OVERLAY_TEST_BUILD
    // The fixture payload cannot bootstrap in a retail game process
    return _wcsicmp(name, L"Dx11Fixture.exe") == 0;
#else
    return _wcsicmp(name, L"iw7_ship.exe") == 0;
#endif
}

bool CopyConfig(void* argument, iw::overlay::BootstrapConfig& copy) {
    SIZE_T read = 0;
    // A checked local read turns an invalid bootstrap pointer into a normal rejection
    if (!argument || !ReadProcessMemory(GetCurrentProcess(), argument, &copy, sizeof(copy), &read)
        || read != sizeof(copy)) { return false; }
    if (copy.size != sizeof(copy) || copy.version != iw::overlay::ProtocolVersion
        || copy.expectedProcessId != GetCurrentProcessId() || !IsWindow(copy.gameWindow)) { return false; }
    DWORD owner = 0;
    if (!GetWindowThreadProcessId(copy.gameWindow, &owner) || owner != copy.expectedProcessId) { return false; }
    // A fixed packet must include its own string terminator
    return std::wmemchr(copy.sessionDirectory, L'\0', std::size(copy.sessionDirectory)) != nullptr;
}
}

extern "C" __declspec(dllexport) DWORD WINAPI InitializeOverlay(void* argument) {
    try {
        iw::overlay::BootstrapConfig config{};
        if (!CopyConfig(argument, config)) { return ERROR_INVALID_PARAMETER; }
        if (!AcceptedProcess()) { return ERROR_BAD_EXE_FORMAT; }
        bool expected = false;
        if (!started.compare_exchange_strong(expected, true)) { return ERROR_ALREADY_INITIALIZED; }
        std::string error;
        // Hook setup and runtime work happen here, outside the loader lock
        if (!iw::overlay::StartDx11Hooks(config, error)) { return StartupResult(ERROR_DLL_INIT_FAILED); }
        if (!iw::overlay::RuntimeStart(config, error)) {
            iw::overlay::StopDx11Hooks();
            return StartupResult(ERROR_DLL_INIT_FAILED);
        }
        return StartupResult(ERROR_SUCCESS);
    } catch (...) {
        iw::overlay::StopDx11Hooks();
        return StartupResult(ERROR_DLL_INIT_FAILED);
    }
}

extern "C" __declspec(dllexport) DWORD WINAPI StopOverlay(void*) {
    try {
        // Disable drawing before stopping the model producer; trampolines stay resident
        iw::overlay::StopDx11Hooks();
        iw::overlay::RuntimeStop();
        return ERROR_SUCCESS;
    } catch (...) { return ERROR_DLL_INIT_FAILED; }
}

#ifdef IW_OVERLAY_TEST_BUILD
extern "C" __declspec(dllexport) DWORD WINAPI ReadFixtureInitializationResult() {
    return initializationResult.load(std::memory_order_acquire);
}
extern "C" __declspec(dllexport) BOOL WINAPI ReadFixtureStatistics(iw::overlay::tests::FixtureStatistics* output) {
    if (!output || output->size != sizeof(*output)) { return FALSE; }
    auto value = iw::overlay::ReadHookStatistics();
    output->presents = value.presents;
    output->drawn = value.drawn;
    output->skipped = value.skipped;
    output->resizes = value.resizes;
    output->faults = value.faults;
    output->boundWindow = value.boundWindow;
    output->enabled = value.enabled;
    return TRUE;
}
extern "C" __declspec(dllexport) BOOL WINAPI ReadFixtureRendererStatus(iw::overlay::RendererStatus* output) {
    if (!output) { return FALSE; }
    *output = iw::overlay::ReadRendererStatus();
    return TRUE;
}
#endif

BOOL WINAPI DllMain(HINSTANCE, DWORD, LPVOID) {
    // No renderer, hook, thread, or cleanup runs while Windows holds the loader lock
    return TRUE;
}
