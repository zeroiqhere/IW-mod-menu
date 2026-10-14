#include "OwnedForeignWindow.h"
#include <array>
#include <cstdint>
#include <memory>
#include <stdexcept>
#include <string>
#include <vector>

namespace iw::overlay::tests {
namespace {
struct Close {
    void operator()(void* handle) const noexcept { if (handle && handle != INVALID_HANDLE_VALUE) { CloseHandle(handle); } }
};
using Handle = std::unique_ptr<void, Close>;
LRESULT CALLBACK Procedure(HWND window, UINT message, WPARAM word, LPARAM data) {
    return DefWindowProcW(window, message, word, data);
}
}

int RunForeignWindowHost(HANDLE readyPipe, HANDLE stopEvent) {
    Handle pipe(readyPipe), stop(stopEvent);
    WNDCLASSW registration{};
    registration.hInstance = GetModuleHandleW(nullptr);
    registration.lpfnWndProc = Procedure;
    registration.lpszClassName = L"IWBootstrapForeignOwnedHost";
    ATOM atom = RegisterClassW(&registration);
    if (!atom) { return 2; }
    // The child window stays hidden and is never activated or used for input
    HWND window = CreateWindowExW(0, MAKEINTATOM(atom), L"IW hidden probe ownership fixture", WS_POPUP,
        0, 0, 1, 1, nullptr, nullptr, registration.hInstance, nullptr);
    DWORD written = 0;
    bool ready = window && WriteFile(pipe.get(), &window, sizeof(window), &written, nullptr) && written == sizeof(window);
    pipe.reset();
    if (ready) { WaitForSingleObject(stop.get(), 12000); }
    if (window) { DestroyWindow(window); }
    UnregisterClassW(MAKEINTATOM(atom), registration.hInstance);
    return ready ? 0 : 3;
}

OwnedForeignWindow::OwnedForeignWindow() {
    SECURITY_ATTRIBUTES attributes{sizeof(attributes), nullptr, TRUE};
    HANDLE rawRead = nullptr, rawWrite = nullptr;
    if (!CreatePipe(&rawRead, &rawWrite, &attributes, 0)) { throw std::runtime_error("The owned child pipe could not be created"); }
    Handle read(rawRead), write(rawWrite);
    if (!SetHandleInformation(read.get(), HANDLE_FLAG_INHERIT, 0)) { throw std::runtime_error("The parent pipe inheritance could not be cleared"); }
    Handle stop(CreateEventW(&attributes, TRUE, FALSE, nullptr));
    if (!stop) { throw std::runtime_error("The owned child stop event could not be created"); }
    SIZE_T size = 0;
    InitializeProcThreadAttributeList(nullptr, 1, 0, &size);
    std::vector<std::byte> storage(size);
    auto* list = reinterpret_cast<LPPROC_THREAD_ATTRIBUTE_LIST>(storage.data());
    if (!InitializeProcThreadAttributeList(list, 1, 0, &size)) { throw std::runtime_error("The child handle list could not be initialized"); }
    std::array<HANDLE, 2> inherited{write.get(), stop.get()};
    bool listed = UpdateProcThreadAttribute(list, 0, PROC_THREAD_ATTRIBUTE_HANDLE_LIST,
        inherited.data(), sizeof(inherited), nullptr, nullptr) != FALSE;
    if (!listed) { DeleteProcThreadAttributeList(list); throw std::runtime_error("The child handle list could not be restricted"); }
    std::array<wchar_t, 32768> path{};
    DWORD length = GetModuleFileNameW(nullptr, path.data(), static_cast<DWORD>(path.size()));
    if (length == 0 || length >= path.size()) { DeleteProcThreadAttributeList(list); throw std::runtime_error("The owned test executable could not be located"); }
    std::wstring command = L"\"" + std::wstring(path.data(), length) + L"\" --foreign-host "
        + std::to_wstring(reinterpret_cast<std::uintptr_t>(write.get())) + L" "
        + std::to_wstring(reinterpret_cast<std::uintptr_t>(stop.get()));
    STARTUPINFOEXW startup{};
    startup.StartupInfo.cb = sizeof(startup);
    startup.lpAttributeList = list;
    // Only the ready pipe and stop event are inherited by this specific owned child
    bool created = CreateProcessW(path.data(), command.data(), nullptr, nullptr, TRUE,
        EXTENDED_STARTUPINFO_PRESENT | CREATE_NO_WINDOW, nullptr, nullptr, &startup.StartupInfo, &process_) != FALSE;
    DeleteProcThreadAttributeList(list);
    if (!created) { throw std::runtime_error("The owned hidden child process could not be created"); }
    stop_ = stop.release();
    write.reset();
    DWORD available = 0;
    ULONGLONG deadline = GetTickCount64() + 4000;
    // Bounded polling avoids waiting forever if a test child fails before publishing its HWND
    while (GetTickCount64() < deadline && PeekNamedPipe(read.get(), nullptr, 0, nullptr, &available, nullptr)
        && available < sizeof(HWND)) { Sleep(10); }
    DWORD copied = 0;
    if (available < sizeof(HWND) || !ReadFile(read.get(), &window_, sizeof(window_), &copied, nullptr)
        || copied != sizeof(window_)) {
        SetEvent(stop_); WaitForSingleObject(process_.hProcess, 13000);
        CloseHandle(stop_); CloseHandle(process_.hThread); CloseHandle(process_.hProcess);
        stop_ = nullptr; process_ = {};
        throw std::runtime_error("The owned child did not publish a complete HWND");
    }
}

OwnedForeignWindow::~OwnedForeignWindow() {
    // Only this fixture's known child is signaled and awaited; no process is forcibly terminated
    if (stop_) { SetEvent(stop_); WaitForSingleObject(process_.hProcess, 13000); CloseHandle(stop_); }
    if (process_.hThread) { CloseHandle(process_.hThread); }
    if (process_.hProcess) { CloseHandle(process_.hProcess); }
}
}
