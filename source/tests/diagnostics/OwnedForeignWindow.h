#pragma once
#include <windows.h>

namespace iw::overlay::tests {
// This child process owns a hidden test window and exits after an event or timeout
int RunForeignWindowHost(HANDLE readyPipe, HANDLE stopEvent);
class OwnedForeignWindow final {
public:
    OwnedForeignWindow();
    ~OwnedForeignWindow();
    OwnedForeignWindow(const OwnedForeignWindow&) = delete;
    OwnedForeignWindow& operator=(const OwnedForeignWindow&) = delete;
    HWND Window() const noexcept { return window_; }
    DWORD ProcessId() const noexcept { return process_.dwProcessId; }
private:
    PROCESS_INFORMATION process_{};
    HANDLE stop_ = nullptr;
    HWND window_ = nullptr;
};
}
