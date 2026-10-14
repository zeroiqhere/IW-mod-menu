#include "FixtureExternalBootstrap.h"
#include <bit>
#include <fstream>
#include <stdexcept>

namespace iw::overlay::tests {

HMODULE WaitForExternalBootstrap(HWND window, const std::filesystem::path& log,
        std::ostream& output, const std::function<void()>& pumpFrame) {
    std::filesystem::path packet = log;
    packet += L".target.json";
    {
        std::ofstream target(packet);
        // The helper receives only identities belonging to this test process
        target << "{\"processId\":" << GetCurrentProcessId() << ",\"gameWindow\":"
            << reinterpret_cast<std::uintptr_t>(window) << "}\n";
        if (!target) { throw std::runtime_error("The fixture identity packet could not be written"); }
    }
    output << "INFO Waiting up to 60 seconds for the external fixture-only bootstrap\n";
    output.flush();
    const ULONGLONG deadline = GetTickCount64() + 60000;
    while (GetTickCount64() < deadline) {
        pumpFrame();
        // Looking up a module does not load it or add a new module reference
        HMODULE payload = GetModuleHandleW(L"iw_overlay_fixture.dll");
        if (payload) {
            using ResultCall = DWORD (WINAPI*)();
            auto result = std::bit_cast<ResultCall>(GetProcAddress(payload, "ReadFixtureInitializationResult"));
            if (!result) { throw std::runtime_error("The externally loaded DLL is not the fixture payload"); }
            DWORD status = result();
            if (status == ERROR_SUCCESS) { return payload; }
            if (status != ERROR_IO_PENDING) { throw std::runtime_error("External fixture initialization failed"); }
        }
        // Wake for window messages so a bootstrap title check is not delayed by sleep
        MsgWaitForMultipleObjectsEx(0, nullptr, 5, QS_ALLINPUT, MWMO_INPUTAVAILABLE);
    }
    throw std::runtime_error("The external fixture-only bootstrap timed out");
}

}
